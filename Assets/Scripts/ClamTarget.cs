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
    [SerializeField] private float openAngle = -60f;
    [SerializeField] private float shakeAmount = 0.015f;

    private enum Phase { Closed, Shake, Opening, Open, Closing }

    private Phase phase = Phase.Closed;
    private float timer;
    private Target pearl;
    private GameManager gameManager;
    private LevelManager levelManager;
    private Quaternion hingeClosed;
    private Vector3 visualRest;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        levelManager = FindFirstObjectByType<LevelManager>();
        if (hinge != null) hingeClosed = hinge.localRotation;
        if (visual != null) visualRest = visual.transform.localPosition;
        Close(Random.Range(closedTime.x, closedTime.y));
    }

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
                SetOpen(Mathf.Max(timer, 0f) / closingTime);
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
        // La perla que nadie ha derribado se va con la almeja (si ya cayó, se está retirando sola)
        if (pearl != null && pearl.CanBeHit) Destroy(pearl.gameObject);
        pearl = null;
    }

    private void SetOpen(float amount)
    {
        if (hinge != null) hinge.localRotation = hingeClosed * Quaternion.Euler(openAngle * Mathf.SmoothStep(0f, 1f, amount), 0f, 0f);
    }

    private void OnDisable() => Close(Random.Range(closedTime.x, closedTime.y));
}
