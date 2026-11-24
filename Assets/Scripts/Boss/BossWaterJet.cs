using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

// Presentación del chorro de agua del boss (BossAttack.LaserSweep): solo pinta y suena. La lógica (cuándo, por dónde
// y si alcanza al jugador) está en BossController.LaserRoutine; aquí se lee LaserStage / LaserPoint / LaserProgress /
// LaserFrom / LaserTo cada frame, como hacen BossHonkFX o MusicManager con su estado.
//
// 24/11/2026: sustituye al rayo que salía de los ojos (BossLaser), que no encajaba con un payaso de feria. Ahora es la
// broma clásica del payaso: la flor del ojal (Boss_Flower, en Boss.fbx) suelta un chorro de agua a presión que barre la
// caseta a la altura de la cabeza. La mecánica no cambia: solo se esquiva agachándose (C o Ctrl) y no se corta
// disparando. El enum y los nombres del controlador siguen siendo «Laser»: son nombres internos y cambiarlos tocaría
// datos guardados (BossConfig) y al bot de balance sin ganar nada.
//
//   Aviso (stage 1): la flor gira cada vez más deprisa y se hincha, gotea, y al final suelta chorritos de prueba;
//                    delante del jugador, la línea roja a la altura del chorro con flechas en el sentido del barrido
//                    (rojo = «va a atacar», §14). Cartel «¡AGÁCHATE! (C)» en los primeros chorros del combate.
//   Barrido (stage 2): chorro de agua de dos capas que cae un poco en arco, se abre al alejarse y suelta gotas; donde
//                    choca con el decorado, salpica. Al pasar por encima de un jugador agachado, la cámara tiembla un
//                    poco y caen unas gotitas en la pantalla. Si te da, la pantalla queda mojada (gotas que escurren).
//
// Va en el objeto del Boss. Las piezas se crean por código: solo hay que asignar materiales, fuente, clips y mixer.
[RequireComponent(typeof(BossController))]
public class BossWaterJet : MonoBehaviour
{
    [Tooltip("Material aditivo para la línea de aviso y las flechas (p. ej. MuzzleFlash_Additive)")]
    [SerializeField] private Material beamMaterial;
    [Tooltip("Material transparente (no aditivo) para el agua: el chorro, las gotas y la salpicadura")]
    [SerializeField] private Material waterMaterial;
    [SerializeField] private Color beamColor = new Color(1f, 0.12f, 0.08f);
    [SerializeField] private Color waterColor = new Color(0.7f, 0.9f, 1f, 0.85f);
    [SerializeField] private Color waterEdgeColor = new Color(0.45f, 0.75f, 1f, 0.45f);
    [Tooltip("Ancho del chorro en la boquilla y al final (m)")]
    [SerializeField] private Vector2 jetWidth = new Vector2(0.1f, 0.35f);
    [Tooltip("Cuánto cae el chorro en arco a mitad de camino (m)")]
    [SerializeField] private float jetDroop = 0.35f;
    [Tooltip("Ancho de la línea de aviso (m)")]
    [SerializeField] private float aimWidth = 0.035f;
    [Tooltip("Distancia por delante del jugador a la que se dibuja la línea de aviso (m): delante del mostrador")]
    [SerializeField] private float aimLineDistance = 1.6f;
    [Tooltip("Metros que sigue el chorro más allá del jugador")]
    [SerializeField] private float beamOvershoot = 6f;
    [Tooltip("Cuánto por delante de la flor (hacia la cámara) sale el agua (m)")]
    [SerializeField] private float nozzleOffset = 0.35f;

    [Header("Cartel")]
    [SerializeField] private TMP_FontAsset hintFont;
    [SerializeField] private string hintText = "¡AGÁCHATE! (C)";
    [Tooltip("Chorros de cada combate en los que sale el cartel (después basta la línea de aviso)")]
    [SerializeField] private int hintLasers = 3;

    [Header("Pantalla mojada")]
    [SerializeField] private int wetDrops = 16;
    [SerializeField] private float wetSeconds = 1.6f;

    [Header("Sonido")]
    [SerializeField] private AudioClip chargeClip;
    [SerializeField] private AudioClip beamClip;
    [SerializeField] private AudioClip splashClip;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;
    [SerializeField] private AudioMixerGroup sfxGroup;

    private const int ChevronCount = 5;
    private const int JetPoints = 16;

    private BossController boss;
    private Transform flower, fallbackOrigin;
    private Quaternion flowerRest;
    private Vector3 flowerScale;
    private float flowerSpin;
    private LineRenderer jet, jetEdge, aim, testSquirt;
    private readonly LineRenderer[] chevrons = new LineRenderer[ChevronCount];
    private ParticleSystem drips, spray, splash;
    private AudioSource audioSource;
    private TMP_Text hint;
    private PlayerCrouch crouch;
    private CameraShake cameraShake;
    private int lastStage;
    private bool passedOver;
    private int sceneryMask;

    // Pantalla mojada: gotas UI que escurren
    private RectTransform wetRoot;
    private Image[] drops;
    private Vector2[] dropVelocity;
    private float[] dropLife;
    private Image wetTint;
    private float wetAge = -1f;

    private void Awake()
    {
        boss = GetComponent<BossController>();
        sceneryMask = LayerMask.GetMask("Scenery");

        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Boss_Flower") flower = t;
            if (fallbackOrigin == null && t.name == "Boss_Torso") fallbackOrigin = t;
        }
        if (flower != null) { flowerRest = flower.localRotation; flowerScale = flower.localScale; }

        jetEdge = FxUtil.NewLine(transform, "WaterJetEdge", waterMaterial, 1f);
        jet = FxUtil.NewLine(transform, "WaterJet", waterMaterial, 1f);
        testSquirt = FxUtil.NewLine(transform, "WaterTestSquirt", waterMaterial, 0.06f);
        aim = FxUtil.NewLine(transform, "WaterAim", beamMaterial, aimWidth);
        for (int i = 0; i < ChevronCount; i++)
        {
            chevrons[i] = FxUtil.NewLine(transform, "WaterChevron" + i, beamMaterial, aimWidth * 1.5f);
            chevrons[i].positionCount = 3;
        }
        // Ancho variable a lo largo del chorro: fino en la boquilla, se abre al alejarse
        var widthCurve = new AnimationCurve(new Keyframe(0f, jetWidth.x), new Keyframe(1f, jetWidth.y));
        jet.widthCurve = widthCurve;
        jetEdge.widthCurve = new AnimationCurve(new Keyframe(0f, jetWidth.x * 1.8f), new Keyframe(1f, jetWidth.y * 1.4f));
        jet.positionCount = jetEdge.positionCount = JetPoints;

        Material particles = FxUtil.ParticleMaterial(waterMaterial);

        // Goteo de la flor durante el aviso
        drips = FxUtil.NewParticles(transform, "WaterDrips", particles, 120);
        ParticleSystem.MainModule dm = drips.main;
        dm.startLifetime = 0.9f;
        dm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        dm.startColor = waterColor;
        dm.gravityModifier = 1.2f;
        ParticleSystem.ShapeModule ds = drips.shape;
        ds.enabled = true;
        ds.shapeType = ParticleSystemShapeType.Sphere;
        ds.radius = 0.12f;

        // Gotas que se desprenden a lo largo del chorro (Emit a mano)
        spray = FxUtil.NewParticles(transform, "WaterSpray", particles, 600);
        ParticleSystem.MainModule sm = spray.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        sm.gravityModifier = 1.4f;
        ParticleSystem.ColorOverLifetimeModule sf = spray.colorOverLifetime;
        sf.enabled = true;
        sf.color = FadeOut();

        // Salpicadura donde el chorro toca el decorado
        splash = FxUtil.NewParticles(transform, "WaterSplash", particles, 400);
        ParticleSystem.MainModule pm = splash.main;
        pm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        pm.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        pm.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        pm.startColor = waterColor;
        pm.gravityModifier = 1.5f;
        ParticleSystem.ShapeModule ps = splash.shape;
        ps.enabled = true;
        ps.shapeType = ParticleSystemShapeType.Hemisphere;
        ps.radius = 0.1f;
        ParticleSystem.ColorOverLifetimeModule pf = splash.colorOverLifetime;
        pf.enabled = true;
        pf.color = FadeOut();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;   // como los demás efectos del boss: se oye igual desde la caseta
        audioSource.outputAudioMixerGroup = sfxGroup;

        hint = FxUtil.NewScreenHint(transform, "WaterHint", hintFont, beamColor, -220f, 72f);
        hint.text = hintText;
        BuildWetScreen();
        Hide();
    }

    private void OnEnable() { if (boss != null) boss.LaserResolved += HandleResolved; }
    private void OnDisable() { if (boss != null) boss.LaserResolved -= HandleResolved; Hide(); ClearWet(); }

    private void HandleResolved(bool dodged)
    {
        if (dodged) return;
        Wet(wetDrops, true);
        if (splashClip != null) audioSource.PlayOneShot(splashClip, volume);
    }

    private void LateUpdate()
    {
        UpdateWet();

        int stage = boss.LaserStage;
        if (stage != lastStage)
        {
            if (stage == 1) Play(chargeClip);
            else if (stage == 2) { Play(beamClip); passedOver = false; }
            else if (audioSource.isPlaying) audioSource.Stop();   // cortado (fase vaciada, fin de partida)
            lastStage = stage;
            drips.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            splash.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        if (stage == 0) { Hide(); return; }

        if (crouch == null) crouch = SceneCache<PlayerCrouch>.Get();
        if (cameraShake == null) cameraShake = SceneCache<CameraShake>.Get();
        Camera cam = Camera.main;
        Vector3 nozzle = Nozzle(cam);
        Vector3 player = crouch != null ? crouch.transform.position : boss.LaserPoint;
        drips.transform.position = nozzle;

        if (stage == 1) Telegraph(nozzle, player);
        else Sweep(nozzle, player);

        // El cartel solo mientras haga falta: en los primeros chorros, hasta que el jugador se agacha (y en pausa no,
        // para no quedar encima del menú)
        GameManager gm = SceneCache<GameManager>.Get();
        bool paused = gm != null && gm.IsPaused;
        bool needed = !paused && boss.LasersThisFight <= hintLasers &&
                      (crouch == null || !PlayerCrouch.IsBelowBeam(crouch.EyeHeight, boss.LaserPoint.y, 0.1f));
        hint.enabled = needed;
        if (needed) hint.alpha = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
    }

    private Vector3 Nozzle(Camera cam)
    {
        Transform from = flower != null ? flower : fallbackOrigin != null ? fallbackOrigin : transform;
        Vector3 p = from.position;
        Vector3 toCam = cam != null ? (cam.transform.position - p).normalized : Vector3.back;
        return p + toCam * nozzleOffset;
    }

    // La flor gira (en su plano) y se hincha; spin y escala, sobre su pose de reposo
    private void AnimateFlower(float spinSpeed, float swell)
    {
        if (flower == null) return;
        flowerSpin += spinSpeed * Time.deltaTime;
        // Eje local Y del modelo = profundidad del mundo (ejes del Boss.fbx, §6): girar en Y la hace rodar de cara
        flower.localRotation = flowerRest * Quaternion.AngleAxis(flowerSpin, Vector3.up);
        flower.localScale = flowerScale * swell;
    }

    private void Telegraph(Vector3 nozzle, Vector3 player)
    {
        float p = boss.LaserProgress;
        jet.enabled = jetEdge.enabled = false;

        AnimateFlower(Mathf.Lerp(90f, 900f, p * p), 1f + 0.18f * p + 0.04f * Mathf.Sin(Time.time * 30f) * p);
        if (!drips.isEmitting) drips.Play();
        ParticleSystem.EmissionModule emission = drips.emission;
        emission.rateOverTime = Mathf.Lerp(4f, 40f, p);

        // Chorritos de prueba al final del aviso: un instante cada poco, cortos, hacia donde empezará
        bool squirt = p > 0.6f && Mathf.Repeat(Time.time * 3f, 1f) < 0.25f;
        testSquirt.enabled = squirt;
        if (squirt)
        {
            Vector3 dir = (boss.LaserFrom - nozzle).normalized;
            testSquirt.SetPosition(0, nozzle);
            testSquirt.SetPosition(1, nozzle + dir * 0.8f + Vector3.down * 0.15f);
            FxUtil.SetColor(testSquirt, waterColor);
        }

        // Línea a la altura del chorro, de lado a lado del barrido, delante del jugador. Parpadea más rápido cuanto
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
        Vector3 along = (b - a).normalized;
        float length = Vector3.Distance(a, b);
        for (int i = 0; i < ChevronCount; i++)
        {
            float k = Mathf.Repeat(Time.time * 0.9f + i / (float)ChevronCount, 1f);
            Vector3 tip = a + along * (length * k);
            LineRenderer c = chevrons[i];
            c.enabled = true;
            c.SetPosition(0, tip - along * 0.18f + Vector3.up * 0.14f);
            c.SetPosition(1, tip);
            c.SetPosition(2, tip - along * 0.18f - Vector3.up * 0.14f);
            Color cc = beamColor; cc.a = Mathf.Sin(k * Mathf.PI) * ac.a;
            FxUtil.SetColor(c, cc);
        }
    }

    private void Sweep(Vector3 nozzle, Vector3 player)
    {
        aim.enabled = testSquirt.enabled = false;
        foreach (LineRenderer c in chevrons) c.enabled = false;
        drips.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        AnimateFlower(900f, 1.1f);

        // Del origen al punto del barrido y más allá, cayendo un poco en arco (el agua pesa)
        Vector3 point = boss.LaserPoint;
        Vector3 dir = (point - nozzle).normalized;
        Vector3 end = point + dir * beamOvershoot;

        // Choque con el decorado más allá del jugador: ahí acaba el chorro y salpica
        bool hit = false;
        Vector3 hitPoint = end, hitNormal = -dir;
        float playerDistance = Vector3.Distance(nozzle, point);
        if (Physics.Raycast(point, dir, out RaycastHit info, beamOvershoot, sceneryMask, QueryTriggerInteraction.Ignore))
        {
            hit = true; hitPoint = info.point; hitNormal = info.normal; end = hitPoint;
        }
        else if (Physics.Raycast(nozzle, dir, out info, playerDistance - 0.5f, sceneryMask, QueryTriggerInteraction.Ignore)
                 && Vector3.Distance(info.point, nozzle) > 2f)
        {
            hit = true; hitPoint = info.point; hitNormal = info.normal;
        }

        jet.enabled = jetEdge.enabled = true;
        float total = Vector3.Distance(nozzle, end);
        Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
        for (int i = 0; i < JetPoints; i++)
        {
            float k = i / (float)(JetPoints - 1);
            Vector3 pos = Vector3.Lerp(nozzle, end, k) + Vector3.down * (4f * jetDroop * k * (1f - k));
            // Temblor del chorro: un poco de ondulación lateral que avanza con él
            pos += side * (Mathf.Sin(Time.time * 25f - k * 12f) * 0.03f * k);
            jet.SetPosition(i, pos);
            jetEdge.SetPosition(i, pos);
        }
        FxUtil.SetColor(jet, waterColor);
        FxUtil.SetColor(jetEdge, waterEdgeColor);

        // Gotas que se desprenden por el camino
        var emit = new ParticleSystem.EmitParams();
        int count = Mathf.CeilToInt(Time.deltaTime * 260f);
        for (int i = 0; i < count; i++)
        {
            float k = Random.value;
            emit.position = jet.GetPosition(Mathf.Clamp(Mathf.RoundToInt(k * (JetPoints - 1)), 0, JetPoints - 1)) + Random.insideUnitSphere * 0.08f * (0.3f + k);
            emit.velocity = dir * Random.Range(2f, 6f) + Random.insideUnitSphere * 1.2f;
            emit.startColor = waterColor;
            emit.startSize = Random.Range(0.04f, 0.1f) * (0.5f + k);
            spray.Emit(emit, 1);
        }

        if (hit)
        {
            splash.transform.position = hitPoint;
            splash.transform.rotation = Quaternion.LookRotation(hitNormal);
            if (!splash.isEmitting) splash.Play();
            ParticleSystem.EmissionModule emission = splash.emission;
            emission.rateOverTime = 260f;
        }
        else splash.Stop(false, ParticleSystemStopBehavior.StopEmitting);

        // Le pasa por encima al jugador agachado: un temblor corto y unas gotitas en la pantalla, una sola vez
        if (!passedOver && crouch != null && Mathf.Abs(point.x - player.x) < 0.4f)
        {
            passedOver = true;
            if (PlayerCrouch.IsBelowBeam(crouch.EyeHeight, point.y, 0.1f))
            {
                if (cameraShake != null) cameraShake.AddTrauma(0.2f);
                Wet(4, false);
            }
        }
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
        if (jet != null) jet.enabled = false;
        if (jetEdge != null) jetEdge.enabled = false;
        if (aim != null) aim.enabled = false;
        if (testSquirt != null) testSquirt.enabled = false;
        foreach (LineRenderer c in chevrons) if (c != null) c.enabled = false;
        if (drips != null) drips.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        if (splash != null) splash.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        if (hint != null) hint.enabled = false;
        if (flower != null) { flower.localRotation = flowerRest; flower.localScale = flowerScale; flowerSpin = 0f; }
    }

    // ---------------------------------------------------------------- pantalla mojada

    // Lienzo propio por encima del HUD con gotas (Image con una textura de gota generada) y un velo azul
    private void BuildWetScreen()
    {
        var canvasGo = new GameObject("WetScreenCanvas");
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        wetRoot = canvasGo.GetComponent<RectTransform>();

        var tintGo = new GameObject("WetTint", typeof(RectTransform));
        tintGo.transform.SetParent(canvasGo.transform, false);
        wetTint = tintGo.AddComponent<Image>();
        wetTint.color = new Color(0.55f, 0.75f, 1f, 0f);
        wetTint.raycastTarget = false;
        RectTransform tr = wetTint.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;

        Sprite dropSprite = DropSprite();
        int max = Mathf.Max(wetDrops, 4);
        drops = new Image[max];
        dropVelocity = new Vector2[max];
        dropLife = new float[max];
        for (int i = 0; i < max; i++)
        {
            var go = new GameObject("Drop" + i, typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            Image img = go.AddComponent<Image>();
            img.sprite = dropSprite;
            img.raycastTarget = false;
            img.enabled = false;
            drops[i] = img;
        }
        canvasGo.SetActive(false);
    }

    // Gota: borde más oscuro y opaco, centro casi transparente y un brillo arriba a la izquierda
    private static Sprite DropSprite()
    {
        const int size = 96;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "WetDrop", wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half, dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = d > 1f ? 0f : Mathf.Lerp(0.06f, 0.85f, Mathf.SmoothStep(0.6f, 0.93f, d)) * (1f - Mathf.SmoothStep(0.95f, 1f, d));
                float shade = Mathf.Lerp(0.95f, 0.4f, Mathf.SmoothStep(0.6f, 1f, d));
                float hx = dx + 0.35f, hy = dy - 0.35f;
                float highlight = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / 0.22f);
                a = Mathf.Max(a, highlight * 0.9f * (d < 1f ? 1f : 0f));
                float c = Mathf.Clamp01(shade + highlight);
                px[y * size + x] = new Color32((byte)(c * 255), (byte)(c * 255), (byte)(c * 255), (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // count gotas repartidas por la pantalla (más hacia el centro y arriba); full = además el velo azul
    private void Wet(int count, bool full)
    {
        if (drops == null) return;
        wetRoot.gameObject.SetActive(true);
        int placed = 0;
        for (int i = 0; i < drops.Length && placed < count; i++)
        {
            if (drops[i].enabled && !full) continue;
            Image d = drops[i];
            float size = full ? Random.Range(110f, 320f) : Random.Range(50f, 110f);
            d.rectTransform.sizeDelta = new Vector2(size, size * Random.Range(1f, 1.25f));
            d.rectTransform.anchoredPosition = new Vector2(Random.Range(-760f, 760f), Random.Range(-300f, 420f));
            d.color = new Color(0.75f, 0.9f, 1f, 1f);
            d.enabled = true;
            dropVelocity[i] = new Vector2(Random.Range(-8f, 8f), -Random.Range(40f, 160f));
            dropLife[i] = wetSeconds * Random.Range(0.75f, 1.2f);
            placed++;
        }
        if (full) wetAge = 0f;
    }

    private void UpdateWet()
    {
        if (drops == null || !wetRoot.gameObject.activeSelf) return;
        bool any = false;
        for (int i = 0; i < drops.Length; i++)
        {
            Image d = drops[i];
            if (!d.enabled) continue;
            dropLife[i] -= Time.deltaTime;
            if (dropLife[i] <= 0f) { d.enabled = false; continue; }
            any = true;
            // Escurre: baja acelerando y se estira; se desvanece en el último medio segundo
            dropVelocity[i] += Vector2.down * (120f * Time.deltaTime);
            RectTransform rt = d.rectTransform;
            rt.anchoredPosition += dropVelocity[i] * Time.deltaTime;
            rt.localScale = new Vector3(1f, 1f + 0.15f * Mathf.Clamp01(-dropVelocity[i].y / 300f), 1f);
            Color c = d.color; c.a = Mathf.Clamp01(dropLife[i] / 0.5f); d.color = c;
        }
        if (wetAge >= 0f)
        {
            wetAge += Time.deltaTime;
            float a = Mathf.Lerp(0.18f, 0f, wetAge / 0.8f);
            wetTint.color = new Color(wetTint.color.r, wetTint.color.g, wetTint.color.b, Mathf.Max(0f, a));
            if (wetAge > 0.8f) wetAge = -1f;
            any = true;
        }
        if (!any) wetRoot.gameObject.SetActive(false);
    }

    private void ClearWet()
    {
        if (drops == null) return;
        foreach (Image d in drops) if (d != null) d.enabled = false;
        wetAge = -1f;
        if (wetRoot != null) wetRoot.gameObject.SetActive(false);
    }

    private static ParticleSystem.MinMaxGradient FadeOut()
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }
}
