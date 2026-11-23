using UnityEngine;

// Mecánica exclusiva del Fondo marino: una almeja en la arena se abre cada pocos segundos y deja ver una perla
// pequeña que vale muchos puntos. Hay que esperar el momento y acertar a algo diminuto: premia apuntar con la
// mira (desde la cadera la dispersión es mayor que la perla), al revés que los animales asustadizos del Bosque.
//
// Ciclo: cerrada (closedTime al azar) → tiembla (aviso) → se abre → abierta openTime s con la perla → se cierra.
// La perla es una diana normal (Target): se instancia al abrirse con los puntos del nivel, como haría el spawner
// (pero sin contar para sus topes) y se retira al cerrarse si nadie le ha dado.
//
// Va en un objeto raíz propio y no dentro de Env_Sea, porque Tools > Setup Sea Level reconstruye ese prefab desde
// cero. Por eso se muestra él solo cuando el nivel en curso es el de su entorno (environmentId), como hace
// LevelManager con los entornos. Solo avanza en Playing.
//
// Sitios (spots): si hay varios, después de cada apertura la almeja se entierra, cambia de sitio y vuelve a salir
// de la arena, para que no se aprenda dónde mirar. Al abrirse, la perla brilla (luz y destello) para atraer la vista.
// El modelo (conchas con estrías y contorno oscuro, estilo «chapa») lo genera Tools > Fairground > Build Sea Clam.
public class ClamTarget : MonoBehaviour
{
    [Tooltip("Entorno del nivel en el que aparece (LevelConfig.environmentId)")]
    [SerializeField] private string environmentId = "Sea";
    [Tooltip("Lo que se ve (conchas): se oculta fuera de su nivel")]
    [SerializeField] private GameObject visual;
    [Tooltip("Bisagra de la concha de arriba: gira en X para abrirse")]
    [SerializeField] private Transform hinge;
    [SerializeField] private Transform pearlAnchor;
    [SerializeField] private Target pearlPrefab;

    [Header("Ritmo (s)")]
    [SerializeField] private Vector2 closedTime = new Vector2(4f, 7f);
    [SerializeField] private float shakeTime = 0.45f;
    [SerializeField] private float openingTime = 0.2f;
    [SerializeField] private float openTime = 1.2f;
    [SerializeField] private float closingTime = 0.15f;

    [Header("Movimiento")]
    [Tooltip("Grados que se levanta la concha de arriba")]
    [SerializeField] private float openAngle = -105f;
    [SerializeField] private float shakeAmount = 0.015f;

    [Header("Sitios")]
    [Tooltip("Posiciones (mundo) entre las que cambia tras cada apertura. Vacío = se queda donde está")]
    [SerializeField] private Vector3[] spots;
    [Tooltip("Segundos en enterrarse y en volver a salir al cambiar de sitio")]
    [SerializeField] private float digTime = 0.3f;
    [Tooltip("Metros que se hunde en la arena al enterrarse")]
    [SerializeField] private float digDepth = 0.3f;

    [Header("Brillo de la perla")]
    [Tooltip("Material aditivo para el destello en cruz (vacío = sin destello, solo luz)")]
    [SerializeField] private Material glintMaterial;
    [SerializeField] private Color glintColor = new Color(1f, 0.92f, 0.97f);

    private enum Phase { Closed, Shake, Opening, Open, Closing, DigOut, DigIn }

    private Phase phase = Phase.Closed;
    private float timer;
    private Target pearl;
    private GameManager gameManager;
    private LevelManager levelManager;
    private Quaternion hingeClosed;
    private Vector3 visualRest;
    private int spotIndex = -1;
    private Light glow;
    private LineRenderer glintA, glintB;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        levelManager = FindFirstObjectByType<LevelManager>();
        if (hinge != null) hingeClosed = hinge.localRotation;
        if (visual != null) visualRest = visual.transform.localPosition;
        BuildGlint();
        MoveToSpot(Random.Range(0, Mathf.Max(1, SpotCount)));
        Close(Random.Range(closedTime.x, closedTime.y));
    }

    private int SpotCount => spots != null ? spots.Length : 0;

    private void Update()
    {
        LevelConfig level = levelManager != null ? levelManager.CurrentLevel : null;
        bool inRun = gameManager != null && gameManager.IsInRun;
        bool here = inRun && level != null && level.environmentId == environmentId;
        if (visual != null && visual.activeSelf != here) visual.SetActive(here);
        if (!here)
        {
            // Fuera de su nivel (otro nivel, menús, partida nueva): cerrada y sin perla
            if (phase != Phase.Closed || pearl != null) Close(Random.Range(closedTime.x, closedTime.y));
            return;
        }
        UpdateGlint();
        if (gameManager.CurrentState != GameManager.GameState.Playing) return;

        timer -= Time.deltaTime;
        switch (phase)
        {
            case Phase.Closed:
                if (timer <= 0f) { phase = Phase.Shake; timer = shakeTime; }
                break;

            case Phase.Shake:
                // Aviso: tiembla un momento antes de abrirse, para que dé tiempo a apuntar
                if (visual != null) visual.transform.localPosition = visualRest + Random.insideUnitSphere * shakeAmount;
                if (timer <= 0f)
                {
                    if (visual != null) visual.transform.localPosition = visualRest;
                    phase = Phase.Opening; timer = openingTime;
                    SpawnPearl(level);
                }
                break;

            case Phase.Opening:
                SetOpen(1f - Mathf.Max(timer, 0f) / openingTime);
                if (timer <= 0f) { phase = Phase.Open; timer = openTime; }
                break;

            case Phase.Open:
                SetOpen(1f);
                if (timer <= 0f) { phase = Phase.Closing; timer = closingTime; }
                break;

            case Phase.Closing:
                RemovePearl();   // la que nadie ha derribado se va en cuanto empieza a cerrarse
                SetOpen(Mathf.Max(timer, 0f) / closingTime);
                if (timer <= 0f)
                {
                    if (SpotCount > 1) { SetOpen(0f); phase = Phase.DigOut; timer = digTime; }
                    else Close(Random.Range(closedTime.x, closedTime.y));
                }
                break;

            case Phase.DigOut:
                // Se entierra: baja en la arena...
                SetDig(1f - Mathf.Max(timer, 0f) / digTime);
                if (timer <= 0f)
                {
                    int next = Random.Range(0, SpotCount - 1);
                    if (next >= spotIndex) next++;   // nunca el mismo sitio
                    MoveToSpot(next);
                    phase = Phase.DigIn; timer = digTime;
                }
                break;

            case Phase.DigIn:
                // ...y sale en el sitio nuevo
                SetDig(Mathf.Max(timer, 0f) / digTime);
                if (timer <= 0f) Close(Random.Range(closedTime.x, closedTime.y));
                break;
        }
    }

    private void SpawnPearl(LevelConfig level)
    {
        if (pearlPrefab == null || pearlAnchor == null) return;
        pearl = Instantiate(pearlPrefab, pearlAnchor.position, pearlAnchor.rotation);
        // Como TargetSpawner.SpawnOne, antes de su Start: puntos del nivel. Vida siempre 1 (una perla diminuta no
        // aguanta dos disparos) y el tiempo de vida lo marca la almeja, que la retira al cerrarse
        pearl.ApplyLevel(level.pointsMultiplier, 100f, 1f);
        pearl.Killed += _ => GameLog.Info("Fondo marino: ¡perla!");
    }

    private void Close(float wait)
    {
        phase = Phase.Closed;
        timer = wait;
        SetOpen(0f);
        if (visual != null) visual.transform.localPosition = visualRest;
        RemovePearl();
        if (glow != null) glow.intensity = 0f;
        if (glintA != null) glintA.enabled = glintB.enabled = false;
    }

    // La perla que nadie ha derribado se va con la almeja (si ya cayó, se está retirando sola)
    private void RemovePearl()
    {
        if (pearl != null && pearl.CanBeHit) Destroy(pearl.gameObject);
        pearl = null;
    }

    private void MoveToSpot(int index)
    {
        if (SpotCount == 0) return;
        spotIndex = Mathf.Clamp(index, 0, SpotCount - 1);
        transform.position = spots[spotIndex];
    }

    // 0 = en su sitio, 1 = enterrada del todo
    private void SetDig(float amount)
    {
        if (visual != null) visual.transform.localPosition = visualRest + Vector3.down * (digDepth * Mathf.SmoothStep(0f, 1f, amount));
    }

    // Luz suave y destello en cruz que gira sobre la perla mientras está a la vista
    private void BuildGlint()
    {
        if (pearlAnchor == null) return;
        var lightGo = new GameObject("PearlGlow");
        lightGo.transform.SetParent(pearlAnchor, false);
        glow = lightGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = glintColor;
        glow.range = 0.9f;
        glow.shadows = LightShadows.None;
        glow.intensity = 0f;
        if (glintMaterial == null) return;
        glintA = FxUtil.NewLine(pearlAnchor, "PearlGlintA", glintMaterial, 0.02f);
        glintB = FxUtil.NewLine(pearlAnchor, "PearlGlintB", glintMaterial, 0.02f);
    }

    private void UpdateGlint()
    {
        bool shown = pearl != null && pearl.CanBeHit && (phase == Phase.Open || phase == Phase.Opening);
        if (glow != null) glow.intensity = Mathf.MoveTowards(glow.intensity, shown ? 0.9f : 0f, Time.deltaTime * 12f);
        if (glintA == null) return;
        glintA.enabled = glintB.enabled = shown;
        if (!shown) return;
        Camera cam = Camera.main;
        Vector3 c = pearl.transform.position;
        Vector3 toCam = cam != null ? (cam.transform.position - c).normalized : Vector3.back;
        Quaternion face = Quaternion.LookRotation(toCam) * Quaternion.Euler(0f, 0f, Time.time * 90f);
        float size = 0.16f + 0.06f * Mathf.Sin(Time.time * 9f);
        Vector3 offset = toCam * 0.12f;   // por delante de la perla, para que no la tape
        glintA.SetPosition(0, c + offset + face * Vector3.up * size);
        glintA.SetPosition(1, c + offset - face * Vector3.up * size);
        glintB.SetPosition(0, c + offset + face * Vector3.right * size);
        glintB.SetPosition(1, c + offset - face * Vector3.right * size);
        Color col = glintColor; col.a = 0.6f + 0.4f * Mathf.Sin(Time.time * 9f);
        FxUtil.SetColor(glintA, col);
        FxUtil.SetColor(glintB, col);
    }

    private void SetOpen(float amount)
    {
        if (hinge != null) hinge.localRotation = hingeClosed * Quaternion.Euler(openAngle * Mathf.SmoothStep(0f, 1f, amount), 0f, 0f);
    }

    private void OnDisable() => Close(Random.Range(closedTime.x, closedTime.y));
}
