using TMPro;
using UnityEngine;
using UnityEngine.Audio;

// Mecánica exclusiva del Cielo: ráfagas de viento. Cada pocos segundos un cartel avisa de hacia dónde va a soplar
// y, al acabar el aviso, el viento empuja TODAS las dianas hacia ese lado durante gustTime s: hay que corregir la
// puntería en el momento. El empujón sube y baja suave (seno), así que nada da saltos.
//
// Va en un objeto raíz propio (como ClamTarget) y solo actúa cuando el nivel en curso es el de su entorno.
// Empuja con TargetMovement.Push. Solo avanza en Playing. El cartel va en un lienzo propio creado por código.
public class WindGusts : MonoBehaviour
{
    [Tooltip("Entorno del nivel en el que sopla (LevelConfig.environmentId)")]
    [SerializeField] private string environmentId = "Sky";

    [Header("Ritmo (s)")]
    [SerializeField] private Vector2 calmTime = new Vector2(10f, 14f);
    [SerializeField] private float warningTime = 1.2f;
    [SerializeField] private float gustTime = 2f;

    [Header("Fuerza")]
    [Tooltip("Velocidad máxima del empujón (m/s), en el centro de la ráfaga. Con 2 s, unos 1,6 m en total")]
    [SerializeField] private float maxSpeed = 1.25f;

    [Header("Cartel y sonido")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Color color = new Color(0.85f, 0.95f, 1f);
    [Tooltip("Sopla de aviso + ráfaga (dura warningTime + gustTime)")]
    [SerializeField] private AudioClip gustClip;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.7f;
    [SerializeField] private AudioMixerGroup sfxGroup;

    private enum Phase { Calm, Warning, Gust }

    private Phase phase = Phase.Calm;
    private float timer;
    private float direction = 1f;
    private GameManager gameManager;
    private LevelManager levelManager;
    private TMP_Text banner;
    private AudioSource audioSource;

    // Para quien quiera reaccionar (y para probarlo): -1 izquierda, 1 derecha, 0 sin viento
    public float CurrentWind => phase == Phase.Gust ? direction : 0f;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        levelManager = FindFirstObjectByType<LevelManager>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.outputAudioMixerGroup = sfxGroup;
        banner = NewBanner();
        Calm();
    }

    private void Update()
    {
        LevelConfig level = levelManager != null ? levelManager.CurrentLevel : null;
        bool here = gameManager != null && gameManager.IsInRun && level != null && level.environmentId == environmentId;
        if (!here) { if (phase != Phase.Calm) Calm(); return; }

        bool playing = gameManager.CurrentState == GameManager.GameState.Playing;
        // En pausa el sonido se calla solo (AudioListener.pause); el cartel, fuera para no tapar el menú
        banner.enabled = phase == Phase.Warning && playing;
        if (!playing) return;

        timer -= Time.deltaTime;
        switch (phase)
        {
            case Phase.Calm:
                if (timer <= 0f)
                {
                    phase = Phase.Warning;
                    timer = warningTime;
                    direction = Random.value < 0.5f ? -1f : 1f;
                    banner.text = direction > 0f ? "VIENTO  >>>" : "<<<  VIENTO";
                    if (gustClip != null) { audioSource.clip = gustClip; audioSource.volume = volume; audioSource.Play(); }
                }
                break;

            case Phase.Warning:
                banner.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 7f));
                if (timer <= 0f)
                {
                    phase = Phase.Gust;
                    timer = gustTime;
                    GameLog.Info($"Cielo: ráfaga hacia la {(direction > 0f ? "derecha" : "izquierda")}");
                }
                break;

            case Phase.Gust:
                // Seno de 0 a π: empieza y acaba en 0. Su media es 2/π de maxSpeed
                float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(1f - timer / gustTime));
                Vector3 delta = Vector3.right * (direction * maxSpeed * k * Time.deltaTime);
                foreach (TargetMovement movement in FindObjectsByType<TargetMovement>(FindObjectsSortMode.None))
                    if (movement.enabled) movement.Push(delta);   // derribados y expirando tienen el movimiento apagado
                if (timer <= 0f) Calm();
                break;
        }
    }

    private void Calm()
    {
        phase = Phase.Calm;
        timer = Random.Range(calmTime.x, calmTime.y);
        if (banner != null) banner.enabled = false;
        if (audioSource != null && audioSource.isPlaying && (gameManager == null || !gameManager.IsInRun)) audioSource.Stop();
    }

    private TMP_Text NewBanner()
    {
        var canvasGo = new GameObject("WindBannerCanvas");
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;
        var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var textGo = new GameObject("Banner");
        textGo.transform.SetParent(canvasGo.transform, false);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = 64;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color(0.05f, 0.1f, 0.2f);
        text.raycastTarget = false;
        RectTransform rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 260f);
        rt.sizeDelta = new Vector2(1200f, 110f);
        text.enabled = false;
        return text;
    }
}
