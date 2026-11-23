using UnityEngine;
using UnityEngine.Audio;

// Mecánica exclusiva del Cielo: ráfagas de viento. Cada pocos segundos sopla una ráfaga hacia un lado y empuja
// TODAS las dianas: hay que corregir la puntería en el momento.
//
// Cómo se ve (antes era un cartel «VIENTO >>>» y solo se movían las dianas, así que costaba entender qué pasaba):
//   · Aviso: empiezan a cruzar la caseta unas pocas rachas (trazos blancos) en el sentido en que va a soplar, y
//     suena el soplido. Ya se sabe hacia dónde antes de que empuje.
//   · Ráfaga: muchas más rachas y jirones de nube, rápidos; los banderines de la caseta se inclinan y aletean
//     (variable global _FairgroundGust que lee el shader Fairground/Sway).
// Fuerza: cada ráfaga tiene su propia fuerza y duración al azar alrededor de una media, y dentro de la ráfaga hay
// rachas (ruido de Perlin): no se puede compensar siempre igual. El empujón sube y baja suave, nada da saltos.
// Límite: el viento nunca saca una diana de la zona de juego (|x| ≤ playLimit, por dentro de las paredes laterales
// a ±6 m): si está dentro, como mucho la deja en el borde; si aún está entrando desde detrás de una pared, no la
// empuja hacia fuera (si no, una diana podía quedarse escondida tras el decorado sin poder dispararle).
//
// Va en un objeto raíz propio (SkyWind) y solo actúa cuando el nivel en curso es el de su entorno. Empuja con
// TargetMovement.Push. Solo avanza en Playing. Las partículas se crean por código.
public class WindGusts : MonoBehaviour
{
    [Tooltip("Entorno del nivel en el que sopla (LevelConfig.environmentId)")]
    [SerializeField] private string environmentId = "Sky";

    [Header("Ritmo (s)")]
    [SerializeField] private Vector2 calmTime = new Vector2(8f, 12f);
    [SerializeField] private float warningTime = 1.1f;
    [Tooltip("Duración de cada ráfaga, al azar entre x e y")]
    [SerializeField] private Vector2 gustTime = new Vector2(1.8f, 2.8f);

    [Header("Fuerza")]
    [Tooltip("Velocidad máxima media del empujón (m/s) en el centro de la ráfaga. Con 2 s y 2 m/s, unos 2,5 m")]
    [SerializeField] private float meanSpeed = 2f;
    [Tooltip("Variación de la fuerza entre ráfagas (± m/s)")]
    [SerializeField] private float speedJitter = 0.6f;
    [Tooltip("Rachas dentro de la ráfaga: cuánto sube y baja la fuerza (0 = constante, 0,4 = ±40 %)")]
    [Range(0f, 0.8f)] [SerializeField] private float turbulence = 0.35f;
    [SerializeField] private float turbulenceFrequency = 2.5f;
    [Tooltip("El viento no empuja hacia fuera a una diana que esté más allá de esta x (m, en valor absoluto)")]
    [SerializeField] private float playLimit = 5.3f;

    [Header("Rachas visibles")]
    [Tooltip("Material aditivo o de partículas (p. ej. Tracer_Additive). Se le pone una textura de punto suave")]
    [SerializeField] private Material streakMaterial;
    [Tooltip("Caja en la que nacen las rachas (centro y tamaño, en el mundo): el interior de la caseta")]
    [SerializeField] private Vector3 streakAreaCenter = new Vector3(0f, 2.3f, 2.6f);
    [SerializeField] private Vector3 streakAreaSize = new Vector3(13f, 3.8f, 4.5f);
    [SerializeField] private Color streakColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color wispColor = new Color(1f, 0.95f, 0.95f, 0.16f);

    [Header("Sonido")]
    [Tooltip("Soplido de aviso + ráfaga")]
    [SerializeField] private AudioClip gustClip;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.7f;
    [SerializeField] private AudioMixerGroup sfxGroup;

    private static readonly int GustId = Shader.PropertyToID("_FairgroundGust");

    private enum Phase { Calm, Warning, Gust }

    private Phase phase = Phase.Calm;
    private float timer, phaseLength;
    private float direction = 1f;
    private float gustSpeed;
    private float noiseSeed;
    private float shaderGust;   // lo que ven los banderines, suavizado
    private GameManager gameManager;
    private LevelManager levelManager;
    private AudioSource audioSource;
    private ParticleSystem streaks, wisps;

    // -1 izquierda, 1 derecha, 0 sin viento. Strength: 0..1 de la fuerza actual respecto a la máxima de la ráfaga
    public float CurrentWind => phase == Phase.Gust ? direction : 0f;
    public float Strength { get; private set; }

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        levelManager = FindFirstObjectByType<LevelManager>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.outputAudioMixerGroup = sfxGroup;

        Material particles = FxUtil.ParticleMaterial(streakMaterial);
        streaks = NewStreaks("WindStreaks", particles, 400, new ParticleSystem.MinMaxCurve(0.025f, 0.05f), 0.45f, 0.9f);
        var streakRenderer = streaks.GetComponent<ParticleSystemRenderer>();
        streakRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        streakRenderer.velocityScale = 0.12f;
        streakRenderer.lengthScale = 2f;
        wisps = NewStreaks("WindWisps", particles, 80, new ParticleSystem.MinMaxCurve(0.5f, 1.1f), 0.9f, 1.6f);
        Calm();
    }

    private ParticleSystem NewStreaks(string name, Material material, int max, ParticleSystem.MinMaxCurve size, float minLife, float maxLife)
    {
        ParticleSystem ps = FxUtil.NewParticles(transform, name, material, max);
        ps.transform.position = streakAreaCenter;
        ParticleSystem.MainModule main = ps.main;
        main.startSize = size;
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = streakAreaSize;
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = new ParticleSystem.MinMaxGradient(g);
        return ps;
    }

    private void Update()
    {
        LevelConfig level = levelManager != null ? levelManager.CurrentLevel : null;
        bool here = gameManager != null && gameManager.IsInRun && level != null && level.environmentId == environmentId;
        if (!here) { if (phase != Phase.Calm) Calm(); SetShaderGust(0f, true); return; }

        bool playing = gameManager.CurrentState == GameManager.GameState.Playing;
        if (!playing)
        {
            // En pausa el sonido se calla solo (AudioListener.pause); las partículas se congelan con timeScale
            return;
        }

        timer -= Time.deltaTime;
        switch (phase)
        {
            case Phase.Calm:
                if (timer <= 0f) BeginWarning();
                break;

            case Phase.Warning:
                // Unas pocas rachas lentas ya en el sentido del viento: es el aviso
                SetEmission(Mathf.Lerp(6f, 25f, 1f - timer / warningTime), 0f, 3.5f);
                if (timer <= 0f) BeginGust();
                break;

            case Phase.Gust:
                // Seno de 0 a π: empieza y acaba en 0; por encima, rachas de Perlin alrededor de 1
                float t = 1f - Mathf.Clamp01(timer / phaseLength);
                float envelope = Mathf.Sin(Mathf.PI * t);
                float gusts = 1f + turbulence * (Mathf.PerlinNoise(noiseSeed, Time.time * turbulenceFrequency) * 2f - 1f);
                float speed = gustSpeed * envelope * Mathf.Max(0f, gusts);
                Strength = meanSpeed > 0f ? Mathf.Clamp01(speed / (meanSpeed + speedJitter)) : 0f;
                Push(direction * speed * Time.deltaTime);
                SetEmission(Mathf.Lerp(20f, 160f, Strength), Mathf.Lerp(2f, 14f, Strength), Mathf.Lerp(5f, 11f, Strength));
                if (timer <= 0f) Calm();
                break;
        }
        SetShaderGust(phase == Phase.Gust ? direction * Strength : 0f, false);
    }

    private void BeginWarning()
    {
        phase = Phase.Warning;
        timer = phaseLength = warningTime;
        direction = Random.value < 0.5f ? -1f : 1f;
        if (gustClip != null) { audioSource.clip = gustClip; audioSource.volume = volume; audioSource.Play(); }
    }

    private void BeginGust()
    {
        phase = Phase.Gust;
        timer = phaseLength = Random.Range(gustTime.x, gustTime.y);
        gustSpeed = Mathf.Max(0.2f, meanSpeed + Random.Range(-speedJitter, speedJitter));
        noiseSeed = Random.Range(0f, 100f);
        GameLog.Info($"Cielo: ráfaga hacia la {(direction > 0f ? "derecha" : "izquierda")} ({gustSpeed:0.0} m/s, {phaseLength:0.0} s)");
    }

    private void Calm()
    {
        phase = Phase.Calm;
        timer = Random.Range(calmTime.x, calmTime.y);
        Strength = 0f;
        SetEmission(0f, 0f, 0f);
        if (audioSource != null && audioSource.isPlaying && (gameManager == null || !gameManager.IsInRun)) audioSource.Stop();
    }

    // Empuja a todas las dianas que se mueven, sin sacarlas de la zona de juego
    private void Push(float dx)
    {
        foreach (TargetMovement movement in FindObjectsByType<TargetMovement>(FindObjectsSortMode.None))
        {
            if (!movement.enabled) continue;   // derribados y expirando tienen el movimiento apagado
            float allowed = LimitPush(movement.transform.position.x, dx, playLimit);
            if (allowed != 0f) movement.Push(new Vector3(allowed, 0f, 0f));
        }
    }

    // Función pura (con tests): cuánto se puede empujar a una diana en x con el límite ±limit.
    // Hacia dentro, siempre todo. Hacia fuera, solo hasta el borde, y nada si ya está fuera
    public static float LimitPush(float x, float dx, float limit)
    {
        if (dx == 0f) return 0f;
        bool outward = Mathf.Sign(dx) == Mathf.Sign(x) && x != 0f;
        if (!outward) return dx;
        float room = limit - Mathf.Abs(x);
        if (room <= 0f) return 0f;
        return Mathf.Sign(dx) * Mathf.Min(Mathf.Abs(dx), room);
    }

    private void SetEmission(float streakRate, float wispRate, float speed)
    {
        Emission(streaks, streakRate, speed, streakColor);
        Emission(wisps, wispRate, speed * 0.7f, wispColor);
    }

    private void Emission(ParticleSystem ps, float rate, float speed, Color color)
    {
        if (ps == null) return;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = rate;
        ParticleSystem.MainModule main = ps.main;
        main.startColor = color;
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(direction * speed * 0.8f, direction * speed * 1.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    // Los banderines (Fairground/Sway) se inclinan con el viento. Suavizado para que no den tirones
    private void SetShaderGust(float target, bool instant)
    {
        shaderGust = instant ? target : Mathf.MoveTowards(shaderGust, target, Time.deltaTime * 2.5f);
        Shader.SetGlobalFloat(GustId, shaderGust);
    }

    private void OnDisable() => Shader.SetGlobalFloat(GustId, 0f);
}
