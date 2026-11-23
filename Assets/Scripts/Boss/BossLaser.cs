using TMPro;
using UnityEngine;
using UnityEngine.Audio;

// Presentación del rayo del boss (BossAttack.LaserSweep): solo pinta y suena. La lógica (cuándo, por dónde y si
// alcanza al jugador) está en BossController.LaserRoutine; aquí se lee LaserStage / LaserPoint / LaserProgress /
// LaserFrom / LaserTo cada frame, como hacen MusicManager o BossHonkFX con su estado.
//
//   Aviso (stage 1): el brillo rojo de los ojos crece mientras partículas de energía se meten en ellos; un rayo
//                    fino de mira sale de los ojos hacia donde empezará el barrido, y delante del jugador una
//                    línea a la altura exacta del rayo, con flechas que corren en el sentido del barrido: dice por
//                    dónde y hacia dónde va a pasar. Cartel «¡AGÁCHATE! (C)» en los primeros rayos del combate.
//   Barrido (stage 2): rayo de dos capas (núcleo blanco y halo rojo que vibra) de los ojos al punto del barrido y
//                    más allá. Donde choca con el decorado saltan chispas con luz. Al pasarle por encima a un
//                    jugador agachado, la cámara tiembla un poco: se nota que se ha librado por poco.
//
// Va en el objeto del Boss. Las piezas se crean por código: solo hay que asignar material, fuente, clips y mixer.
[RequireComponent(typeof(BossController))]
public class BossLaser : MonoBehaviour
{
    [Tooltip("Material aditivo para el rayo (p. ej. MuzzleFlash_Additive)")]
    [SerializeField] private Material beamMaterial;
    [SerializeField] private Color beamColor = new Color(1f, 0.12f, 0.08f);
    [Tooltip("Ancho del halo del rayo al disparar (m); el núcleo blanco es un tercio")]
    [SerializeField] private float beamWidth = 0.3f;
    [Tooltip("Ancho de la línea de aviso (m)")]
    [SerializeField] private float aimWidth = 0.035f;
    [Tooltip("Distancia por delante del jugador a la que se dibuja la línea de aviso (m): delante del mostrador")]
    [SerializeField] private float aimLineDistance = 1.6f;
    [Tooltip("Metros que sigue el rayo más allá del jugador")]
    [SerializeField] private float beamOvershoot = 6f;
    [SerializeField] private float chargeLightIntensity = 6f;

    [Header("Cartel")]
    [SerializeField] private TMP_FontAsset hintFont;
    [SerializeField] private string hintText = "¡AGÁCHATE! (C)";
    [Tooltip("Rayos de cada combate en los que sale el cartel (después basta la línea de aviso)")]
    [SerializeField] private int hintLasers = 3;

    [Header("Sonido")]
    [SerializeField] private AudioClip chargeClip;
    [SerializeField] private AudioClip beamClip;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;
    [SerializeField] private AudioMixerGroup sfxGroup;

    private const int ChevronCount = 5;

    private BossController boss;
    private Transform[] eyes;
    private LineRenderer beam, core, aim, sight;
    private readonly LineRenderer[] chevrons = new LineRenderer[ChevronCount];
    private ParticleSystem charge, sparks;
    private Light chargeLight, impactLight;
    private AudioSource audioSource;
    private TMP_Text hint;
    private PlayerCrouch crouch;
    private CameraShake cameraShake;
    private int lastStage;
    private bool passedOver;
    private int sceneryMask;

    private void Awake()
    {
        boss = GetComponent<BossController>();
        sceneryMask = LayerMask.GetMask("Scenery");

        // Origen del rayo: entre los dos ojos (los puntos débiles «Eye»); si no hay, la raíz del boss
        var found = new System.Collections.Generic.List<Transform>();
        foreach (BossWeakPoint point in GetComponentsInChildren<BossWeakPoint>(true))
            if (point.Id == "Eye") found.Add(point.transform);
        eyes = found.ToArray();

        beam = FxUtil.NewLine(transform, "LaserBeam", beamMaterial, beamWidth);
        core = FxUtil.NewLine(transform, "LaserCore", beamMaterial, beamWidth / 3f);
        aim = FxUtil.NewLine(transform, "LaserAim", beamMaterial, aimWidth);
        sight = FxUtil.NewLine(transform, "LaserSight", beamMaterial, aimWidth * 0.75f);
        for (int i = 0; i < ChevronCount; i++)
        {
            chevrons[i] = FxUtil.NewLine(transform, "LaserChevron" + i, beamMaterial, aimWidth * 1.5f);
            chevrons[i].positionCount = 3;
        }

        Material particles = FxUtil.ParticleMaterial(beamMaterial);

        // Energía que entra en los ojos durante el aviso (como el aire del bocinazo, pero roja y más rápida)
        charge = FxUtil.NewParticles(transform, "LaserCharge", particles, 200);
        ParticleSystem.MainModule main = charge.main;
        main.startLifetime = 0.35f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.startColor = beamColor;
        ParticleSystem.ShapeModule shape = charge.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.2f;
        shape.radiusThickness = 0.1f;
        ParticleSystem.VelocityOverLifetimeModule velocity = charge.velocityOverLifetime;
        velocity.enabled = true;
        velocity.radial = -3.4f;
        var chargeRenderer = charge.GetComponent<ParticleSystemRenderer>();
        chargeRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        chargeRenderer.velocityScale = 0.1f;

        // Chispas donde el rayo toca el decorado
        sparks = FxUtil.NewParticles(transform, "LaserSparks", particles, 300);
        ParticleSystem.MainModule sparkMain = sparks.main;
        sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        sparkMain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), beamColor);
        sparkMain.gravityModifier = 1.2f;
        ParticleSystem.ShapeModule sparkShape = sparks.shape;
        sparkShape.enabled = true;
        sparkShape.shapeType = ParticleSystemShapeType.Hemisphere;
        sparkShape.radius = 0.05f;
        var sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        sparkRenderer.velocityScale = 0.05f;

        chargeLight = NewLight("LaserChargeLight", 6f);
        impactLight = NewLight("LaserImpactLight", 3f);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;   // como los demás efectos del boss: se oye igual desde la caseta
        audioSource.outputAudioMixerGroup = sfxGroup;

        hint = FxUtil.NewScreenHint(transform, "LaserHint", hintFont, beamColor, -220f, 72f);
        hint.text = hintText;
        Hide();
    }

    private Light NewLight(string name, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = beamColor;
        light.range = range;
        light.shadows = LightShadows.None;
        light.intensity = 0f;
        return light;
    }

    private void LateUpdate()
    {
        int stage = boss.LaserStage;
        if (stage != lastStage)
        {
            if (stage == 1) Play(chargeClip);
            else if (stage == 2) { Play(beamClip); passedOver = false; }
            else if (audioSource.isPlaying) audioSource.Stop();   // cortado (fase vaciada, fin de partida)
            lastStage = stage;
            charge.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            sparks.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        if (stage == 0) { Hide(); return; }

        Vector3 origin = EyeOrigin();
        if (crouch == null) crouch = SceneCache<PlayerCrouch>.Get();
        if (cameraShake == null) cameraShake = SceneCache<CameraShake>.Get();
        Vector3 player = crouch != null ? crouch.transform.position : boss.LaserPoint;
        chargeLight.transform.position = origin;
        // Las partículas, algo por delante de los ojos: a ras de la cara las tapaba la propia cabeza
        Camera cam = Camera.main;
        charge.transform.position = cam != null ? origin + (cam.transform.position - origin).normalized * 0.5f : origin;

        if (stage == 1) Telegraph(origin, player);
        else Sweep(origin, player);

        // El cartel solo mientras haga falta: en los primeros rayos, hasta que el jugador se agacha (y en pausa no,
        // para no quedar encima del menú)
        GameManager gm = SceneCache<GameManager>.Get();
        bool paused = gm != null && gm.IsPaused;
        bool needed = !paused && boss.LasersThisFight <= hintLasers &&
                      (crouch == null || !PlayerCrouch.IsBelowBeam(crouch.EyeHeight, boss.LaserPoint.y, 0.1f));
        hint.enabled = needed;
        if (needed) hint.alpha = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
    }

    private void Telegraph(Vector3 origin, Vector3 player)
    {
        float p = boss.LaserProgress;
        beam.enabled = core.enabled = false;
        impactLight.intensity = 0f;

        if (!charge.isEmitting) charge.Play();
        ParticleSystem.EmissionModule emission = charge.emission;
        emission.rateOverTime = Mathf.Lerp(30f, 160f, p);
        chargeLight.intensity = chargeLightIntensity * p * (0.85f + 0.15f * Mathf.Sin(Time.time * 40f));

        // Rayo de mira: de los ojos a donde empezará el barrido. Fino y temblando, cada vez más visible
        Vector3 start = boss.LaserFrom;
        sight.enabled = true;
        sight.SetPosition(0, origin);
        sight.SetPosition(1, start + Random.insideUnitSphere * 0.02f);
        Color sc = beamColor; sc.a = Mathf.Lerp(0.15f, 0.7f, p);
        FxUtil.SetColor(sight, sc);

        // Línea a la altura del rayo, de lado a lado del barrido, delante del jugador. Parpadea más rápido cuanto
        // menos falta
        float z = player.z + aimLineDistance;
        float y = boss.LaserPoint.y;
        Vector3 a = new Vector3(boss.LaserFrom.x, y, z);
        Vector3 b = new Vector3(boss.LaserTo.x, y, z);
        aim.enabled = true;
        aim.SetPosition(0, a);
        aim.SetPosition(1, b);
        float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 30f, p));
        Color ac = beamColor; ac.a = Mathf.Lerp(0.35f, 1f, blink);
        FxUtil.SetColor(aim, ac);

        // Flechas que corren por la línea en el sentido del barrido
        Vector3 dir = (b - a).normalized;
        float length = Vector3.Distance(a, b);
        for (int i = 0; i < ChevronCount; i++)
        {
            float k = Mathf.Repeat(Time.time * 0.9f + i / (float)ChevronCount, 1f);
            Vector3 tip = a + dir * (length * k);
            LineRenderer c = chevrons[i];
            c.enabled = true;
            c.SetPosition(0, tip - dir * 0.18f + Vector3.up * 0.14f);
            c.SetPosition(1, tip);
            c.SetPosition(2, tip - dir * 0.18f - Vector3.up * 0.14f);
            Color cc = beamColor; cc.a = Mathf.Sin(k * Mathf.PI) * ac.a;
            FxUtil.SetColor(c, cc);
        }
    }

    private void Sweep(Vector3 origin, Vector3 player)
    {
        aim.enabled = sight.enabled = false;
        foreach (LineRenderer c in chevrons) c.enabled = false;

        // Del origen al punto del barrido y más allá: así cruza la caseta en vez de acabar en la cara
        Vector3 point = boss.LaserPoint;
        Vector3 dir = (point - origin).normalized;
        Vector3 end = point + dir * beamOvershoot;

        // Choque con el decorado más allá del jugador (pilares, paredes): ahí acaba el rayo y saltan chispas
        float distance = Vector3.Distance(origin, end);
        bool hit = false;
        Vector3 hitPoint = end, hitNormal = -dir;
        float playerDistance = Vector3.Distance(origin, point);
        if (Physics.Raycast(point, dir, out RaycastHit info, beamOvershoot, sceneryMask, QueryTriggerInteraction.Ignore))
        {
            hit = true; hitPoint = info.point; hitNormal = info.normal; end = hitPoint;
        }
        else if (Physics.Raycast(origin, dir, out info, playerDistance - 0.5f, sceneryMask, QueryTriggerInteraction.Ignore)
                 && Vector3.Distance(info.point, origin) > 2f)
        {
            // Algo del decorado entre el boss y el jugador (el marco de la caseta al barrer los extremos)
            hit = true; hitPoint = info.point; hitNormal = info.normal;
        }

        float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 55f) + Random.Range(-0.05f, 0.05f);
        beam.enabled = core.enabled = true;
        beam.SetPosition(0, origin); beam.SetPosition(1, end);
        core.SetPosition(0, origin); core.SetPosition(1, end);
        beam.widthMultiplier = beamWidth * flicker;
        core.widthMultiplier = beamWidth / 3f;
        FxUtil.SetColor(beam, beamColor);
        FxUtil.SetColor(core, new Color(1f, 0.92f, 0.85f));
        chargeLight.intensity = chargeLightIntensity * flicker;

        if (hit)
        {
            sparks.transform.position = hitPoint;
            sparks.transform.rotation = Quaternion.LookRotation(hitNormal);
            if (!sparks.isEmitting) sparks.Play();
            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 220f;
            impactLight.transform.position = hitPoint + hitNormal * 0.2f;
            impactLight.intensity = 3f * flicker;
        }
        else
        {
            sparks.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            impactLight.intensity = 0f;
        }

        // Le pasa por encima al jugador: un temblor corto, una sola vez por barrido
        if (!passedOver && crouch != null && Mathf.Abs(point.x - player.x) < 0.4f)
        {
            passedOver = true;
            if (cameraShake != null) cameraShake.AddTrauma(0.2f);
        }
    }

    private Vector3 EyeOrigin()
    {
        if (eyes == null || eyes.Length == 0) return transform.position + Vector3.up * 4f;
        Vector3 sum = Vector3.zero;
        foreach (Transform eye in eyes) sum += eye.position;
        return sum / eyes.Length;
    }

    private void Play(AudioClip clip)
    {
        if (clip == null) return;
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.Play();
    }

    private void Hide()
    {
        if (beam != null) beam.enabled = false;
        if (core != null) core.enabled = false;
        if (aim != null) aim.enabled = false;
        if (sight != null) sight.enabled = false;
        foreach (LineRenderer c in chevrons) if (c != null) c.enabled = false;
        if (charge != null) charge.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        if (sparks != null) sparks.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        if (chargeLight != null) chargeLight.intensity = 0f;
        if (impactLight != null) impactLight.intensity = 0f;
        if (hint != null) hint.enabled = false;
    }

    private void OnDisable() => Hide();
}
