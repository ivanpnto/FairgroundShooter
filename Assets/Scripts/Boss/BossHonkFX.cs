using TMPro;
using UnityEngine;

// Presentación del bocinazo (BossAttack.ChargedHonk): solo pinta. La lógica está en BossController.HonkRoutine;
// aquí se lee HonkStage / HonkProgress / HonkCounterProgress cada frame, como hace BossWaterJet con el chorro de agua.
//
// Problema que resuelve: el bocinazo quitaba 2 corazones sin que pasara nada en pantalla. Ahora cada tramo se lee
// sin explicación:
//   1. Toma de aire: el aire entra en la boca (partículas que se meten dentro). Se entiende «está cogiendo aire».
//   2. Ventana (boca abierta): anillos rojos que laten en la boca cada vez más deprisa (va a soltarlo) y, alrededor
//      de la boca, un aro cian que se llena con el daño a la mandíbula: cuando se completa, se le corta el
//      bocinazo. Cian = punto débil abierto (§14). En los primeros bocinazos de cada combate, un cartel lo dice.
//   3. Si no se ha cortado: tres ondas de sonido salen de la boca y vienen hacia la caseta creciendo; el golpe
//      llega con ellas (BossConfig.honkWaveTravelTime).
//   Contrarrestado: el aro estalla en chispas cian y la boca suelta una bocanada de humo, como un claxon roto.
//
// Va en el objeto del Boss. Todo se crea por código: solo hay que asignar el material y la fuente.
[RequireComponent(typeof(BossController))]
public class BossHonkFX : MonoBehaviour
{
    [Tooltip("Material aditivo (p. ej. MuzzleFlash_Additive): anillos, ondas y partículas")]
    [SerializeField] private Material additiveMaterial;
    [SerializeField] private TMP_FontAsset hintFont;

    [Header("Colores (reglas de lectura, §14)")]
    [SerializeField] private Color dangerColor = new Color(1f, 0.25f, 0.12f);
    [SerializeField] private Color counterColor = new Color(0.25f, 0.95f, 1f);
    [SerializeField] private Color airColor = new Color(0.9f, 0.95f, 1f, 0.8f);
    [SerializeField] private Color smokeColor = new Color(0.55f, 0.55f, 0.6f, 0.7f);

    [Header("Tamaños (m)")]
    [SerializeField] private float meterRadius = 0.85f;
    [SerializeField] private float waveStartRadius = 0.5f;
    [SerializeField] private float waveEndRadius = 2.6f;
    [Tooltip("La onda se para a esta distancia de la cámara (m): si llegara a ella se saldría de la pantalla")]
    [SerializeField] private float waveStopDistance = 2.2f;
    [Tooltip("Cuánto por delante de la boca (hacia la cámara) se dibujan aros y partículas: la nariz y la cara " +
             "sobresalen, y a ras de la boca quedaban tapados por la cabeza")]
    [SerializeField] private float frontOffset = 0.7f;

    [Header("Cartel")]
    [SerializeField] private string hintText = "¡DISPARA A LA BOCA!";
    [Tooltip("Bocinazos de cada combate en los que sale el cartel")]
    [SerializeField] private int hintHonks = 2;

    private const int WaveCount = 3;
    private const int PulseCount = 3;

    private BossController boss;
    private Transform mouth;
    private Camera view;
    private LineRenderer meterBack, meterFill;
    private readonly LineRenderer[] waves = new LineRenderer[WaveCount];
    private readonly LineRenderer[] pulses = new LineRenderer[PulseCount];
    private ParticleSystem air, burst;
    private Light mouthLight;
    private TMP_Text hint;
    private int lastStage;

    private void Awake()
    {
        boss = GetComponent<BossController>();
        Material particles = FxUtil.ParticleMaterial(additiveMaterial);

        meterBack = FxUtil.NewLine(transform, "HonkMeterBack", additiveMaterial, 0.09f, true);
        meterFill = FxUtil.NewLine(transform, "HonkMeterFill", additiveMaterial, 0.16f);
        for (int i = 0; i < WaveCount; i++) waves[i] = FxUtil.NewLine(transform, "HonkWave" + i, additiveMaterial, 0.1f, true);
        for (int i = 0; i < PulseCount; i++) pulses[i] = FxUtil.NewLine(transform, "HonkPulse" + i, additiveMaterial, 0.08f, true);

        // Aire que entra: nace en una esfera alrededor de la boca y viaja hacia su centro (velocidad radial negativa)
        air = FxUtil.NewParticles(transform, "HonkAir", particles, 300);
        ParticleSystem.MainModule main = air.main;
        main.startLifetime = 0.45f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        main.startColor = airColor;
        ParticleSystem.ShapeModule shape = air.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.8f;
        shape.radiusThickness = 0.15f;
        ParticleSystem.VelocityOverLifetimeModule velocity = air.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.radial = -3.8f;
        ParticleSystem.ColorOverLifetimeModule fade = air.colorOverLifetime;
        fade.enabled = true;
        fade.color = FadeInOut();
        var airRenderer = air.GetComponent<ParticleSystemRenderer>();
        airRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        airRenderer.velocityScale = 0.12f;
        airRenderer.lengthScale = 2.5f;

        // Chispas y humo del bocinazo cortado (Emit a mano)
        burst = FxUtil.NewParticles(transform, "HonkBurst", particles, 200);
        ParticleSystem.MainModule burstMain = burst.main;
        burstMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        burstMain.gravityModifier = 0.15f;
        ParticleSystem.ColorOverLifetimeModule burstFade = burst.colorOverLifetime;
        burstFade.enabled = true;
        burstFade.color = FadeOut();
        ParticleSystem.SizeOverLifetimeModule grow = burst.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

        var lightGo = new GameObject("HonkMouthLight");
        lightGo.transform.SetParent(transform, false);
        mouthLight = lightGo.AddComponent<Light>();
        mouthLight.type = LightType.Point;
        mouthLight.range = 5f;
        mouthLight.shadows = LightShadows.None;
        mouthLight.intensity = 0f;

        hint = FxUtil.NewScreenHint(transform, "HonkHint", hintFont, counterColor, -220f, 68f);
        hint.text = hintText;
        HideAll();
    }

    private void Start()
    {
        // La boca: el punto débil que contrarresta el bocinazo (la rejilla de la mandíbula, que baja con ella)
        foreach (BossWeakPoint point in GetComponentsInChildren<BossWeakPoint>(true))
            if (point.Id == "Jaw") mouth = point.transform;
        view = Camera.main;
    }

    private void LateUpdate()
    {
        int stage = boss.HonkStage;
        if (stage != lastStage)
        {
            if (lastStage == 2 && stage == 0 && boss.State == BossController.BossState.Stunned) Countered();
            lastStage = stage;
            air.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
        if (view == null) view = Camera.main;
        if (stage == 0 || mouth == null || view == null) { HideAll(); return; }

        Vector3 toPlayer = (view.transform.position - mouth.position).normalized;
        Vector3 center = mouth.position + toPlayer * frontOffset;
        air.transform.position = center;
        mouthLight.transform.position = center;

        GameManager gm = SceneCache<GameManager>.Get();
        bool paused = gm != null && gm.IsPaused;

        switch (stage)
        {
            case 1: Windup(center); break;
            case 2: Window(center, toPlayer, paused); break;
            case 3: Wave(center); break;
        }
        if (stage != 2) { hint.enabled = false; }
        if (stage != 2) { meterBack.enabled = meterFill.enabled = false; SetPulses(false); }
        if (stage != 3) foreach (LineRenderer w in waves) w.enabled = false;
    }

    // 1) Toma de aire: el aire entra cada vez más deprisa
    private void Windup(Vector3 center)
    {
        if (!air.isEmitting) air.Play();
        ParticleSystem.EmissionModule emission = air.emission;
        emission.rateOverTime = Mathf.Lerp(60f, 200f, boss.HonkProgress);
        mouthLight.color = dangerColor;
        mouthLight.intensity = 1.5f * boss.HonkProgress;
    }

    // 2) Ventana: anillos rojos que laten (va a soltarlo) y aro cian que se llena con el daño a la mandíbula
    private void Window(Vector3 center, Vector3 toPlayer, bool paused)
    {
        float p = boss.HonkProgress;
        float rate = Mathf.Lerp(1.6f, 4.5f, p);   // latidos por segundo: se acelera hasta soltarlo
        for (int i = 0; i < PulseCount; i++)
        {
            float k = Mathf.Repeat(Time.time * rate + i / (float)PulseCount, 1f);
            LineRenderer pulse = pulses[i];
            pulse.enabled = true;
            FxUtil.SetArc(pulse, center, toPlayer, Mathf.Lerp(0.35f, meterRadius * 0.95f, k), 1f, 32);
            Color c = dangerColor; c.a = (1f - k) * 0.9f;
            FxUtil.SetColor(pulse, c);
        }

        float fill = boss.HonkCounterProgress;
        meterBack.enabled = true;
        FxUtil.SetArc(meterBack, center + toPlayer * 0.03f, toPlayer, meterRadius, 1f);
        Color back = counterColor; back.a = 0.25f;
        FxUtil.SetColor(meterBack, back);
        meterFill.enabled = fill > 0.001f;
        if (meterFill.enabled)
        {
            FxUtil.SetArc(meterFill, center + toPlayer * 0.06f, toPlayer, meterRadius, fill);
            Color c = Color.Lerp(counterColor, Color.white, fill * 0.5f);
            FxUtil.SetColor(meterFill, c);
        }

        mouthLight.color = dangerColor;
        mouthLight.intensity = Mathf.Lerp(1.5f, 4f, p) * (0.75f + 0.25f * Mathf.Sin(Time.time * rate * Mathf.PI * 2f));

        hint.enabled = !paused && boss.HonksThisFight <= hintHonks;
        if (hint.enabled) hint.alpha = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
    }

    // 3) Ondas que salen de la boca hacia la caseta, creciendo. Llegan cuando llega el golpe
    private void Wave(Vector3 center)
    {
        Vector3 eye = view.transform.position;
        float total = Vector3.Distance(center, eye) - waveStopDistance;
        Vector3 dir = (eye - center).normalized;
        float p = boss.HonkProgress;
        for (int i = 0; i < WaveCount; i++)
        {
            // Cada onda sale un poco después de la anterior y todas llegan a la vez al final
            float k = Mathf.Clamp01((p - i * 0.12f) / (1f - i * 0.12f));
            LineRenderer w = waves[i];
            w.enabled = k > 0f;
            if (!w.enabled) continue;
            float eased = k * k;   // acelera: sale despacio de la boca y llega de golpe
            Vector3 pos = center + dir * (total * eased);
            FxUtil.SetArc(w, pos, dir, Mathf.Lerp(waveStartRadius, waveEndRadius, eased), 1f, 40);
            w.widthMultiplier = Mathf.Lerp(0.08f, 0.22f, eased);
            Color c = Color.Lerp(dangerColor, new Color(1f, 0.85f, 0.4f), i * 0.3f);
            c.a = Mathf.Lerp(1f, 0.55f, eased);
            FxUtil.SetColor(w, c);
        }
        mouthLight.color = dangerColor;
        mouthLight.intensity = 5f * (1f - p);
    }

    // Bocinazo cortado: el aro estalla en chispas cian y la boca suelta humo
    private void Countered()
    {
        if (mouth == null || view == null) return;
        Vector3 toPlayer = (view.transform.position - mouth.position).normalized;
        Vector3 center = mouth.position + toPlayer * frontOffset;
        var emit = new ParticleSystem.EmitParams();
        for (int i = 0; i < 40; i++)
        {
            float a = i / 40f * Mathf.PI * 2f;
            Vector3 radial = Quaternion.LookRotation(toPlayer) * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            emit.position = center + radial * meterRadius;
            emit.velocity = radial * Random.Range(2f, 4f) + toPlayer * Random.Range(0f, 1f);
            emit.startColor = counterColor;
            emit.startSize = Random.Range(0.06f, 0.12f);
            emit.startLifetime = Random.Range(0.4f, 0.8f);
            burst.Emit(emit, 1);
        }
        for (int i = 0; i < 14; i++)
        {
            emit.position = center + Random.insideUnitSphere * 0.2f;
            emit.velocity = (toPlayer * 0.8f + Random.insideUnitSphere * 0.6f + Vector3.up * 0.6f) * Random.Range(0.8f, 1.6f);
            emit.startColor = smokeColor;
            emit.startSize = Random.Range(0.35f, 0.6f);
            emit.startLifetime = Random.Range(0.8f, 1.2f);
            burst.Emit(emit, 1);
        }
    }

    private void SetPulses(bool on)
    {
        foreach (LineRenderer pulse in pulses) pulse.enabled = on;
    }

    private void HideAll()
    {
        if (meterBack != null) meterBack.enabled = false;
        if (meterFill != null) meterFill.enabled = false;
        SetPulses(false);
        foreach (LineRenderer w in waves) if (w != null) w.enabled = false;
        if (air != null) air.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        if (mouthLight != null) mouthLight.intensity = 0f;
        if (hint != null) hint.enabled = false;
    }

    private void OnDisable() => HideAll();

    private static ParticleSystem.MinMaxGradient FadeInOut()
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    private static ParticleSystem.MinMaxGradient FadeOut()
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }
}
