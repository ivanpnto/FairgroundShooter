using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Revelación del boss (I7.4, §27.1): «la caseta se vuelve contra ti». Animación por código y física.
// I7.6: el nivel 4 empieza como uno normal («El Circo»: telón abierto, dianas, cuenta atrás) y a los
// LevelConfig.bossRevealDelay s llega la revelación, unos 5 s sin control del jugador (pausa de escena):
//
//   0,0 s  Las luces parpadean y se apagan. El circo deja de sacar dianas.
//   1,2 s  ¡PUM! La pared del fondo tiembla.
//   1,9 s  ¡PUM! La pared se agrieta y por las grietas se cuela luz roja.
//   2,4 s  (Solo si el telón estaba cerrado, con retardo 0) el telón se abre de golpe.
//   3,2 s  ¡CRASH! Pared, techo y decorado del circo revientan y el boss sube desde el suelo con rebote.
//   4,2 s  Rugido: boca abierta y luz roja, sin daño.
//   5,0 s  Empieza el combate (BossController.StartFight).
//
// Cada momento lanza un evento para que Jaime enganche sonido y sacudida de cámara (J7.2–J7.3).
// Al empezar cualquier otro nivel (partida nueva) lo deja todo como al principio.
public class BossReveal : MonoBehaviour
{
    [Header("Piezas")]
    [SerializeField] private BossController boss;
    [Tooltip("Lo que sube desde el suelo (normalmente el objeto del BossController)")]
    [SerializeField] private Transform bossRoot;
    [SerializeField] private BoothBreak booth;
    [SerializeField] private CurtainController curtain;
    [Tooltip("Luces que parpadean y se apagan (vacío = las del entorno nocturno activo, Lighting_Night)")]
    [SerializeField] private Light[] stageLights;
    [Tooltip("Luz roja detrás de la pared: se cuela por las grietas")]
    [SerializeField] private Light crackLight;
    [Tooltip("Focos que apuntan al boss y se encienden al aparecer")]
    [SerializeField] private Light[] bossSpots;
    [Tooltip("Capas del decorado del nivel falso (el circo) que salen volando con la pared")]
    [SerializeField] private Transform[] decorToLaunch;
    [SerializeField] private float decorSpeed = 7f;
    [SerializeField] private float decorLifetime = 1.4f;

    [Header("Tiempos (s)")]
    [SerializeField] private float flickerTime = 1.2f;
    [SerializeField] private float firstHitTime = 1.2f;
    [SerializeField] private float crackTime = 1.9f;
    [SerializeField] private float curtainTime = 2.4f;
    [SerializeField] private float burstTime = 3.2f;
    [SerializeField] private float roarTime = 4.2f;
    [SerializeField] private float endTime = 5.0f;

    [Header("Movimiento")]
    [Tooltip("Desde cuánto por debajo de su sitio sube el boss (m)")]
    [SerializeField] private float riseDepth = 7f;
    [SerializeField] private float riseDuration = 0.7f;
    [Tooltip("Rebote al llegar (0 = ninguno; 1,7 = bastante)")]
    [SerializeField] private float riseOvershoot = 1.7f;
    [Tooltip("Luz que queda en el escenario durante el combate (× la original)")]
    [SerializeField, Range(0f, 1f)] private float fightLightLevel = 0.35f;
    [SerializeField] private float curtainSnapSpeed = 3f;

    public bool IsPlaying { get; private set; }

    public event System.Action RevealStarted;
    public event System.Action<int> WallHit;      // 1, 2 (los golpes desde detrás)
    public event System.Action CurtainSnapped;
    public event System.Action BoothBurst;
    public event System.Action BossRoared;
    public event System.Action RevealFinished;

    private GameManager gameManager;
    private LevelManager levelManager;
    private TargetSpawner spawner;
    private GameTimer timer;
    private struct DecorPose { public Vector3 position; public Quaternion rotation; public Vector3 scale; }
    private readonly Dictionary<Transform, DecorPose> decorPoses = new Dictionary<Transform, DecorPose>();
    private Vector3 bossRestPosition;
    private readonly Dictionary<Light, float> baseIntensity = new Dictionary<Light, float>();
    private Coroutine routine;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        levelManager = FindFirstObjectByType<LevelManager>();
        spawner = FindFirstObjectByType<TargetSpawner>();
        timer = FindFirstObjectByType<GameTimer>();
        if (decorToLaunch != null)
            foreach (Transform d in decorToLaunch)
                if (d != null) decorPoses[d] = new DecorPose { position = d.localPosition, rotation = d.localRotation, scale = d.localScale };
        if (bossRoot == null && boss != null) bossRoot = boss.transform;
        if (bossRoot != null) bossRestPosition = bossRoot.localPosition;
        if (stageLights == null || stageLights.Length == 0)
        {
            GameObject night = GameObject.Find("Lighting_Night");
            if (night != null) stageLights = night.GetComponentsInChildren<Light>(true);
        }
        foreach (Light l in AllLights()) if (l != null && !baseIntensity.ContainsKey(l)) baseIntensity[l] = l.intensity;
        HideBoss();
    }

    private void OnEnable()
    {
        if (levelManager != null) levelManager.LevelStarted += HandleLevelStarted;
    }

    private void OnDisable()
    {
        if (levelManager != null) levelManager.LevelStarted -= HandleLevelStarted;
    }

    private IEnumerable<Light> AllLights()
    {
        if (stageLights != null) foreach (Light l in stageLights) yield return l;
        if (crackLight != null) yield return crackLight;
        if (bossSpots != null) foreach (Light l in bossSpots) yield return l;
    }

    private void HandleLevelStarted(LevelConfig level, int index)
    {
        if (level != null && level.isBossLevel)
        {
            ResetAll();   // el circo y la caseta, enteros: el nivel empieza como uno normal
            routine = StartCoroutine(IntroThenReveal(DebugMode.RevealDelay(level.bossRevealDelay)));
        }
        else
        {
            ResetAll();
        }
    }

    // Partida nueva o cualquier nivel normal: caseta entera, boss escondido, luces como estaban
    public void ResetAll()
    {
        StopAllCoroutines();   // la revelación y lo que lanzó (subida, temblor, piezas encogiendo)
        routine = null;
        IsPlaying = false;
        if (booth != null) booth.ResetBooth();
        if (curtain != null) { curtain.OpenOverride = null; curtain.Speed = 1f; }
        foreach (KeyValuePair<Light, float> entry in baseIntensity)
            if (entry.Key != null) entry.Key.intensity = IsBossOnly(entry.Key) ? 0f : entry.Value;
        RestoreDecor();
        HideBoss();
    }

    // Nivel «normal» durante delay s de juego (no cuenta la pausa), y entonces la revelación
    private IEnumerator IntroThenReveal(float delay)
    {
        // Un frame: GameManager pone Playing justo después de empezar el nivel
        yield return null;
        for (float t = 0f; t < delay; )
        {
            if (gameManager == null || gameManager.CurrentState == GameManager.GameState.Playing) t += Time.deltaTime;
            else if (gameManager.CurrentState == GameManager.GameState.GameOver) yield break;
            yield return null;
        }
        yield return RevealRoutine();
    }

    private bool IsBossOnly(Light l) =>
        l == crackLight || (bossSpots != null && System.Array.IndexOf(bossSpots, l) >= 0);

    private void HideBoss()
    {
        if (crackLight != null) crackLight.intensity = 0f;
        if (bossSpots != null) foreach (Light l in bossSpots) if (l != null) l.intensity = 0f;
        if (bossRoot != null) bossRoot.localPosition = bossRestPosition + Vector3.down * riseDepth;
    }

    private IEnumerator RevealRoutine()
    {
        // Si la partida ya no está en juego (p. ej. ha acabado), no hay revelación
        if (gameManager != null && !gameManager.PauseForCutscene()) { routine = null; yield break; }
        IsPlaying = true;
        if (boss != null) boss.ResetVisuals();
        HideBoss();
        if (spawner != null) spawner.StopSpawning();   // el circo se acaba: no salen más dianas
        // Con el nivel ya jugándose el telón está abierto, y se queda abierto: fuera de Playing se cerraría solo
        bool curtainOpen = curtain != null && curtain.IsOpen;
        if (curtain != null) curtain.OpenOverride = curtainOpen;
        RevealStarted?.Invoke();
        GameLog.Info("BossReveal: empieza la revelación");

        float t = 0f;
        bool hit1 = false, cracked = false, snapped = curtainOpen, burst = false, roared = false;
        // Punto del golpe: donde quedará el pecho del boss (no donde está ahora, bajo tierra)
        Vector3 restWorld = bossRoot == null ? transform.position
            : (bossRoot.parent != null ? bossRoot.parent.TransformPoint(bossRestPosition) : bossRestPosition);
        Vector3 impact = restWorld + Vector3.up * 2.5f;

        while (t < endTime)
        {
            t += Time.deltaTime;

            // Luces: parpadeo irregular y apagón; tras el reventón, penumbra con focos rojos
            float level;
            if (t < flickerTime) level = Mathf.PerlinNoise(t * 9f, 0.3f) > 0.45f ? 1f : 0.05f;
            else if (!burst) level = 0.05f;
            else level = fightLightLevel;
            SetStageLights(level);

            if (!hit1 && t >= firstHitTime) { hit1 = true; WallHit?.Invoke(1); StartCoroutine(Shake(0.25f, 0.06f)); }
            if (!cracked && t >= crackTime)
            {
                cracked = true;
                WallHit?.Invoke(2);
                if (booth != null) booth.Crack(impact);
                BuckleDecor();   // antes del temblor: Shake guarda la pose de reposo al empezar y la restaura al acabar
                StartCoroutine(Shake(0.3f, 0.09f));
                if (crackLight != null) crackLight.intensity = baseIntensity.TryGetValue(crackLight, out float ci) ? ci : 6f;
            }
            if (!snapped && t >= curtainTime)
            {
                snapped = true;
                if (curtain != null) { curtain.Speed = curtainSnapSpeed; curtain.OpenOverride = true; }
                CurtainSnapped?.Invoke();
            }
            if (!burst && t >= burstTime)
            {
                burst = true;
                if (booth != null) booth.BurstOut(impact);
                if (spawner != null) spawner.ResetSpawner();   // las dianas del circo desaparecen con el reventón
                LaunchDecor(impact);
                if (crackLight != null) crackLight.intensity = 0f;
                if (bossSpots != null)
                    foreach (Light l in bossSpots) if (l != null && baseIntensity.TryGetValue(l, out float si)) l.intensity = si;
                StartCoroutine(Rise());
                BoothBurst?.Invoke();
            }
            if (!roared && t >= roarTime)
            {
                roared = true;
                if (boss != null) boss.PlayRoar(endTime - roarTime);
                BossRoared?.Invoke();
            }
            yield return null;
        }

        if (curtain != null) { curtain.OpenOverride = null; curtain.Speed = 1f; }
        if (bossRoot != null) bossRoot.localPosition = bossRestPosition;
        IsPlaying = false;
        routine = null;
        RevealFinished?.Invoke();
        GameLog.Info("BossReveal: fin; empieza el combate");
        if (gameManager != null) gameManager.ResumeFromCutscene();
        if (timer != null) timer.StartStopwatch();   // desde aquí no hay cuenta atrás (§27.5)
        if (boss != null) boss.StartFight();
    }

    // Las capas del decorado salen despedidas enteras, alejándose del punto del golpe, y encogen al final
    private void LaunchDecor(Vector3 impact)
    {
        if (decorToLaunch == null) return;
        foreach (Transform d in decorToLaunch)
        {
            if (d == null || !d.gameObject.activeInHierarchy) continue;
            // Sin collider mientras vuela: no para balas, y un MeshCollider cóncavo no admite ser trigger ni ir
            // con un Rigidbody dinámico (Unity da error)
            foreach (Collider c in d.GetComponentsInChildren<Collider>()) c.enabled = false;
            Rigidbody rb = d.GetComponent<Rigidbody>();
            if (rb == null) rb = d.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 2f;
            Vector3 away = d.position - impact;
            away.y = Mathf.Abs(away.y) + 1.5f;
            rb.linearVelocity = away.normalized * decorSpeed * Random.Range(0.8f, 1.2f) + Vector3.back * 2f;
            rb.angularVelocity = Random.insideUnitSphere * 4f;
            StartCoroutine(ShrinkAndHide(d, decorLifetime));
        }
    }

    private IEnumerator ShrinkAndHide(Transform d, float lifetime)
    {
        Vector3 from = d.localScale;
        float shrink = 0.35f;
        yield return new WaitForSeconds(Mathf.Max(0f, lifetime - shrink));
        for (float t = 0f; t < shrink && d != null; t += Time.deltaTime)
        {
            d.localScale = from * (1f - t / shrink);
            yield return null;
        }
        if (d != null) d.gameObject.SetActive(false);
    }

    // Segundo golpe: el decorado (que tapa la pared agrietada) se tuerce y se abomba hacia el jugador.
    // Lo deshace RestoreDecor
    private void BuckleDecor()
    {
        if (decorToLaunch == null) return;
        foreach (Transform d in decorToLaunch)
        {
            if (d == null || !d.gameObject.activeInHierarchy) continue;
            d.localRotation *= Quaternion.Euler(Random.Range(-3f, 3f), 0f, Random.Range(-2.5f, 2.5f));
            d.position += Vector3.back * Random.Range(0.04f, 0.1f) + Vector3.up * Random.Range(-0.03f, 0.05f);
        }
    }

    private void RestoreDecor()
    {
        foreach (KeyValuePair<Transform, DecorPose> entry in decorPoses)
        {
            Transform d = entry.Key;
            if (d == null) continue;
            Rigidbody rb = d.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);
            foreach (Collider c in d.GetComponentsInChildren<Collider>(true)) c.enabled = true;
            d.localPosition = entry.Value.position;
            d.localRotation = entry.Value.rotation;
            d.localScale = entry.Value.scale;
            d.gameObject.SetActive(true);
        }
    }

    private void SetStageLights(float level)
    {
        if (stageLights == null) return;
        foreach (Light l in stageLights)
            if (l != null && baseIntensity.TryGetValue(l, out float b)) l.intensity = b * level;
    }

    // Subida desde el suelo con rebote (curva "ease out back")
    private IEnumerator Rise()
    {
        if (bossRoot == null) yield break;
        Vector3 from = bossRestPosition + Vector3.down * riseDepth;
        for (float t = 0f; t < riseDuration; t += Time.deltaTime)
        {
            float x = t / riseDuration - 1f;
            float k = 1f + (riseOvershoot + 1f) * x * x * x + riseOvershoot * x * x;
            bossRoot.localPosition = Vector3.LerpUnclamped(from, bossRestPosition, k);
            yield return null;
        }
        bossRoot.localPosition = bossRestPosition;
    }

    // Temblor de la pared en el primer golpe (aún entera). La sacudida de cámara es de Jaime (WallHit)
    private IEnumerator Shake(float seconds, float amount)
    {
        if (booth == null) yield break;
        var targets = new List<Transform>();
        foreach (Transform child in booth.GetComponentsInChildren<Transform>())
            if (child.name == "Booth_BackWall") targets.Add(child);
        // El decorado del nivel falso tapa la pared: tiembla con ella para que se note el golpe desde detrás
        if (decorToLaunch != null)
            foreach (Transform d in decorToLaunch) if (d != null && d.gameObject.activeInHierarchy) targets.Add(d);
        var rest = new Dictionary<Transform, Vector3>();
        foreach (Transform tr in targets) rest[tr] = tr.localPosition;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = amount * (1f - t / seconds);
            foreach (Transform tr in targets) tr.localPosition = rest[tr] + Random.insideUnitSphere * k;
            yield return null;
        }
        foreach (KeyValuePair<Transform, Vector3> entry in rest) entry.Key.localPosition = entry.Value;
    }
}
