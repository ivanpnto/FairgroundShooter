using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Efectos de las mejoras caras (J6.2): la explosión de la bala explosiva y las trazas del disparo doble.
/// Solo presentación: escucha los eventos de PlayerShoot (Exploded, RayTraced) y no toca la lógica del
/// disparo, igual que WeaponEffects con OnHit/ImpactResolved. Va en Player.
///
/// Mismo enfoque que los impactos de WeaponEffects: UNA instancia del efecto para toda la partida,
/// que se mueve al punto y emite con Emit(). Las partículas simulan en espacio World, así que las de
/// una explosión anterior siguen donde nacieron aunque el sistema se mueva a la siguiente (con disparo
/// doble + explosiva puede haber varias en el mismo frame). Nada de Instantiate/Destroy por disparo.
/// </summary>
public class UpgradeEffects : MonoBehaviour
{
    [Header("Explosión")]
    [Tooltip("Prefab con los sistemas de la explosión (Assets/Prefabs/VFX/Explosion): raíz = destello; hijos Sparks, Smoke, Confetti")]
    [SerializeField] private ParticleSystem explosionPrefab;
    [Tooltip("Radio con el que está diseñado el prefab: con otro radio se escala en proporción")]
    [SerializeField, Min(0.05f)] private float designRadius = 0.6f;
    [SerializeField] private int flashCount = 3;
    [SerializeField] private int sparksCount = 26;
    [SerializeField] private int smokeCount = 10;
    [SerializeField] private int confettiCount = 30;
    [Tooltip("Luz breve en el centro: ilumina el decorado de alrededor un instante")]
    [SerializeField] private Color lightColor = new Color(1f, 0.65f, 0.3f);
    [SerializeField] private float lightIntensity = 5f;
    [SerializeField] private float lightRangePerRadius = 6f;
    [SerializeField] private float lightDuration = 0.09f;
    [Tooltip("Sonido de la explosión (opcional: vacío = no suena)")]
    [SerializeField] private AudioClip explosionSound;
    [Range(0f, 1f)] [SerializeField] private float explosionVolume = 0.8f;
    [Tooltip("Grupo SFX del mixer (el mismo que usa WeaponEffects)")]
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("Trazas del disparo doble")]
    [SerializeField] private Material tracerMaterial;
    [SerializeField] private Color tracerColor = new Color(1f, 0.85f, 0.55f, 0.9f);
    [SerializeField] private float tracerWidth = 0.012f;   // en la boca del cañón (a ~0,4 m del ojo): más ancho parece un láser
    [SerializeField] private float tracerDuration = 0.07f;
    [Tooltip("Solo con varios rayos por disparo (disparo doble). Con un rayo no hay traza: el disparo normal no la tenía")]
    [SerializeField] private bool onlyWithMultipleRays = true;

    private const int TracerPool = 6;   // 2 rayos por disparo y la MP5 dispara cada 77 ms: sobra

    private PlayerShoot playerShoot;
    private WeaponController weaponController;
    private PlayerScope playerScope;
    private Camera playerCamera;

    private ParticleSystem flash, sparks, smoke, confetti;
    private Light blastLight;
    private float blastLightOffTime;
    private AudioSource audioSource;

    private LineRenderer[] tracers;
    private float[] tracerEnd;   // Time.time en que se apaga cada traza
    private int nextTracer;

    private void Awake()
    {
        playerShoot = GetComponent<PlayerShoot>();
        weaponController = GetComponent<WeaponController>();
        playerScope = GetComponent<PlayerScope>();
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null) playerCamera = Camera.main;

        if (explosionPrefab != null)
        {
            // Sin padre: si colgara de Player se movería con la cámara
            flash = Instantiate(explosionPrefab);
            flash.name = "Explosion (pool)";
            sparks = Child(flash, "Sparks");
            smoke = Child(flash, "Smoke");
            confetti = Child(flash, "Confetti");

            GameObject lightGO = new GameObject("ExplosionLight");
            lightGO.transform.SetParent(flash.transform, false);
            blastLight = lightGO.AddComponent<Light>();
            blastLight.type = LightType.Point;
            blastLight.color = lightColor;
            blastLight.shadows = LightShadows.None;   // una luz que parpadea en cada explosión no puede ser cara
            blastLight.enabled = false;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.outputAudioMixerGroup = sfxGroup;

        tracers = new LineRenderer[TracerPool];
        tracerEnd = new float[TracerPool];
        for (int i = 0; i < TracerPool; i++)
        {
            GameObject go = new GameObject($"Tracer_{i}");
            go.transform.SetParent(transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.sharedMaterial = tracerMaterial;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            tracers[i] = line;
        }
    }

    private static ParticleSystem Child(ParticleSystem root, string name)
    {
        Transform t = root.transform.Find(name);
        return t != null ? t.GetComponent<ParticleSystem>() : null;
    }

    private void OnDestroy()
    {
        if (flash != null) Destroy(flash.gameObject);
    }

    private void OnEnable()
    {
        if (playerShoot == null) return;
        playerShoot.Exploded += HandleExplosion;
        playerShoot.RayTraced += HandleRay;
    }

    private void OnDisable()
    {
        if (playerShoot == null) return;
        playerShoot.Exploded -= HandleExplosion;
        playerShoot.RayTraced -= HandleRay;
    }

    // ---------- Explosión ----------
    private void HandleExplosion(Vector3 center, float radius)
    {
        if (flash != null)
        {
            // El prefab usa Scaling Mode = Hierarchy: la escala de la raíz agranda forma y tamaño de todo
            float scale = radius / designRadius;
            flash.transform.SetPositionAndRotation(center, Quaternion.identity);
            flash.transform.localScale = Vector3.one * scale;

            flash.Emit(flashCount);
            if (sparks != null) sparks.Emit(sparksCount);
            if (smoke != null) smoke.Emit(smokeCount);
            if (confetti != null) confetti.Emit(confettiCount);

            blastLight.range = radius * lightRangePerRadius;
            blastLight.intensity = lightIntensity;
            blastLight.enabled = true;
            blastLightOffTime = Time.time + lightDuration;
        }

        if (explosionSound != null)
            audioSource.PlayOneShot(explosionSound, explosionVolume);
    }

    // ---------- Trazas ----------
    private void HandleRay(Vector3 origin, Vector3 end)
    {
        if (onlyWithMultipleRays && (weaponController == null || weaponController.CurrentStats.Rays < 2))
            return;

        // El rayo sale del centro de la cámara: dibujado desde ahí no se vería (apunta al ojo).
        // Se dibuja desde la boca del cañón; con la mira puesta el arma no se ve, y sale un poco por debajo del ojo
        Vector3 start = origin + (playerCamera != null ? -playerCamera.transform.up * 0.08f : Vector3.zero);
        WeaponModel model = weaponController != null ? weaponController.CurrentModel : null;
        bool scoped = playerScope != null && playerScope.IsOverlayVisible;
        if (!scoped && model != null && model.Muzzle != null)
            start = model.Muzzle.position;

        LineRenderer line = tracers[nextTracer];
        tracerEnd[nextTracer] = Time.time + tracerDuration;
        nextTracer = (nextTracer + 1) % tracers.Length;

        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.enabled = true;
        SetTracerAlpha(line, 1f);
    }

    private void SetTracerAlpha(LineRenderer line, float alpha)
    {
        // Más ancha y opaca cerca del arma, fina y transparente al final: se lee como una bala que se aleja
        Color a = tracerColor; a.a *= alpha;
        Color b = tracerColor; b.a = 0f;
        line.startColor = a;
        line.endColor = b;
        line.startWidth = tracerWidth;
        line.endWidth = tracerWidth * 0.3f;
    }

    private void Update()
    {
        if (blastLight != null && blastLight.enabled && Time.time >= blastLightOffTime)
            blastLight.enabled = false;

        for (int i = 0; i < tracers.Length; i++)
        {
            if (!tracers[i].enabled) continue;
            float left = tracerEnd[i] - Time.time;
            if (left <= 0f) { tracers[i].enabled = false; continue; }
            SetTracerAlpha(tracers[i], left / tracerDuration);   // se desvanece en ~4 frames
        }
    }
}
