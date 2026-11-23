using TMPro;
using UnityEngine;
using UnityEngine.Audio;

// Presentación del rayo del boss (BossAttack.LaserSweep): solo pinta y suena. La lógica (cuándo, por dónde y si
// alcanza al jugador) está en BossController.LaserRoutine; aquí se lee LaserStage / LaserPoint / LaserProgress
// cada frame, como hacen MusicManager o WeaponVisual con su estado.
//
//   Aviso (stage 1): luz roja que crece entre los ojos y una línea fina, quieta, a la altura del rayo delante del
//                    jugador: dice exactamente por dónde va a pasar. Cartel «¡AGÁCHATE! (C)».
//   Barrido (stage 2): rayo grueso de los ojos al punto del barrido, que sigue más allá del jugador.
//
// Va en el objeto del Boss. Las piezas (LineRenderer, luz, cartel) se crean por código: no hay nada que montar en
// la escena salvo el material, la fuente, los clips y el grupo del mixer.
[RequireComponent(typeof(BossController))]
public class BossLaser : MonoBehaviour
{
    [Tooltip("Material aditivo para el rayo (p. ej. MuzzleFlash_Additive)")]
    [SerializeField] private Material beamMaterial;
    [SerializeField] private Color beamColor = new Color(1f, 0.12f, 0.08f);
    [Tooltip("Ancho del rayo al disparar (m)")]
    [SerializeField] private float beamWidth = 0.14f;
    [Tooltip("Ancho de la línea de aviso (m)")]
    [SerializeField] private float aimWidth = 0.012f;
    [Tooltip("Distancia por delante del jugador a la que se dibuja la línea de aviso (m): delante del mostrador")]
    [SerializeField] private float aimLineDistance = 1.6f;
    [Tooltip("Metros que sigue el rayo más allá del jugador")]
    [SerializeField] private float beamOvershoot = 6f;
    [SerializeField] private float chargeLightIntensity = 6f;

    [Header("Cartel")]
    [SerializeField] private TMP_FontAsset hintFont;
    [SerializeField] private string hintText = "¡AGÁCHATE! (C)";

    [Header("Sonido")]
    [SerializeField] private AudioClip chargeClip;
    [SerializeField] private AudioClip beamClip;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;
    [SerializeField] private AudioMixerGroup sfxGroup;

    private BossController boss;
    private Transform[] eyes;
    private LineRenderer beam, aim;
    private Light chargeLight;
    private AudioSource audioSource;
    private TMP_Text hint;
    private PlayerCrouch crouch;
    private int lastStage;

    private void Awake()
    {
        boss = GetComponent<BossController>();

        // Origen del rayo: entre los dos ojos (los puntos débiles «Eye»); si no hay, la raíz del boss
        var found = new System.Collections.Generic.List<Transform>();
        foreach (BossWeakPoint point in GetComponentsInChildren<BossWeakPoint>(true))
            if (point.Id == "Eye") found.Add(point.transform);
        eyes = found.ToArray();

        beam = NewLine("LaserBeam", beamWidth);
        aim = NewLine("LaserAim", aimWidth);

        var lightGo = new GameObject("LaserChargeLight");
        lightGo.transform.SetParent(transform, false);
        chargeLight = lightGo.AddComponent<Light>();
        chargeLight.type = LightType.Point;
        chargeLight.color = beamColor;
        chargeLight.range = 6f;
        chargeLight.shadows = LightShadows.None;
        chargeLight.intensity = 0f;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;   // como los demás efectos del boss: se oye igual desde la caseta
        audioSource.outputAudioMixerGroup = sfxGroup;

        hint = NewHint();
        Hide();
    }

    private LineRenderer NewLine(string name, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = width;
        line.numCapVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        if (beamMaterial != null) line.sharedMaterial = beamMaterial;
        line.startColor = line.endColor = beamColor;
        line.enabled = false;
        return line;
    }

    // Lienzo propio por encima del HUD: así no hay que tocar el Canvas de la escena
    private TMP_Text NewHint()
    {
        var canvasGo = new GameObject("LaserHintCanvas");
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var textGo = new GameObject("Hint");
        textGo.transform.SetParent(canvasGo.transform, false);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        if (hintFont != null) text.font = hintFont;
        text.text = hintText;
        text.fontSize = 72;
        text.alignment = TextAlignmentOptions.Center;
        text.color = beamColor;
        text.outlineWidth = 0.25f;
        text.outlineColor = Color.black;
        text.raycastTarget = false;
        RectTransform rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -220f);
        rt.sizeDelta = new Vector2(1200f, 120f);
        return text;
    }

    private void LateUpdate()
    {
        int stage = boss.LaserStage;
        if (stage != lastStage)
        {
            if (stage == 1) Play(chargeClip);
            else if (stage == 2) Play(beamClip);
            else if (audioSource.isPlaying) audioSource.Stop();   // cortado (fase vaciada, fin de partida)
            lastStage = stage;
        }

        if (stage == 0) { Hide(); return; }

        Vector3 origin = EyeOrigin();
        Vector3 point = boss.LaserPoint;
        if (crouch == null) crouch = SceneCache<PlayerCrouch>.Get();
        Vector3 player = crouch != null ? crouch.transform.position : point;

        if (stage == 1)
        {
            // Línea de aviso de lado a lado, a la altura exacta del rayo, un poco por delante del jugador.
            // Parpadea más rápido cuanto menos falta
            float z = player.z + aimLineDistance;
            aim.enabled = true;
            aim.SetPosition(0, new Vector3(player.x - 4f, point.y, z));
            aim.SetPosition(1, new Vector3(player.x + 4f, point.y, z));
            float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 30f, boss.LaserProgress));
            SetAlpha(aim, Mathf.Lerp(0.35f, 1f, blink));
            beam.enabled = false;
            chargeLight.intensity = chargeLightIntensity * boss.LaserProgress;
        }
        else
        {
            // Del origen al punto del barrido y más allá: así cruza la caseta en vez de acabar en la cara
            Vector3 dir = (point - origin).normalized;
            beam.enabled = true;
            beam.SetPosition(0, origin);
            beam.SetPosition(1, point + dir * beamOvershoot);
            SetAlpha(beam, 1f);
            aim.enabled = false;
            chargeLight.intensity = chargeLightIntensity;
        }
        chargeLight.transform.position = origin;

        // El cartel solo mientras haga falta: desaparece en cuanto el jugador está agachado (y en pausa, para no
        // quedar encima del menú)
        GameManager gm = SceneCache<GameManager>.Get();
        bool paused = gm != null && gm.IsPaused;
        bool needed = !paused && (crouch == null || !PlayerCrouch.IsBelowBeam(crouch.EyeHeight, point.y, 0.1f));
        hint.enabled = needed;
        if (needed) hint.alpha = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
    }

    private Vector3 EyeOrigin()
    {
        if (eyes == null || eyes.Length == 0) return transform.position + Vector3.up * 4f;
        Vector3 sum = Vector3.zero;
        foreach (Transform eye in eyes) sum += eye.position;
        return sum / eyes.Length;
    }

    private void SetAlpha(LineRenderer line, float alpha)
    {
        Color c = beamColor; c.a = alpha;
        line.startColor = line.endColor = c;
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
        if (aim != null) aim.enabled = false;
        if (chargeLight != null) chargeLight.intensity = 0f;
        if (hint != null) hint.enabled = false;
    }

    private void OnDisable() => Hide();
}
