using TMPro;
using UnityEngine;

// Mecánica exclusiva del Bosque: animales asustadizos. Si el jugador tiene la diana en la mira más de dwellTime
// segundos seguidos, se asusta: el búho se esconde tras su arbusto y el conejo huye (TargetMovement.Startle).
// Premia el disparo rápido desde la cadera frente a apuntar con calma, al revés que el resto del juego.
//
// «En la mira» = el rayo del centro de la cámara atraviesa los límites de alguno de sus colliders activos (sin
// mirar si hay algo delante: un animal escondido tras un arbusto también nota que lo apuntan).
// Aviso: en cuanto nota la mira le sale un «!» encima, que late cada vez más deprisa y pasa de amarillo a rojo;
// al asustarse, el respingo (estira el modelo) y se va. Sin el aviso el susto no se entendía: el bot, que dispara
// rápido, solo asustó a uno en todo un nivel, y una persona no sabía por qué se le escapaban. Tras asustarse tiene
// calmTime segundos de calma, para que no sea imposible de cazar.
//
// Va en la raíz de la diana, junto a Target y su TargetMovement (prefabs OwlTarget y RabbitTarget).
[RequireComponent(typeof(Target))]
public class ShyTarget : MonoBehaviour
{
    [Tooltip("Segundos seguidos en la mira antes de asustarse")]
    [Min(0.05f)] [SerializeField] private float dwellTime = 0.45f;
    [Tooltip("Segundos tras un susto en los que no se vuelve a asustar")]
    [Min(0f)] [SerializeField] private float calmTime = 2.5f;
    [Tooltip("Respingo: cuánto se estira el modelo en vertical al darse cuenta (0 = nada)")]
    [Min(0f)] [SerializeField] private float jolt = 0.25f;
    [Min(0.01f)] [SerializeField] private float joltTime = 0.18f;

    [Header("Aviso «!»")]
    [Tooltip("Vacío = la fuente por defecto de TextMesh Pro")]
    [SerializeField] private TMP_FontAsset alertFont;
    [SerializeField] private Color alertCalm = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color alertScared = new Color(1f, 0.2f, 0.1f);
    [Tooltip("Altura del «!» sobre el borde de arriba del collider (m)")]
    [SerializeField] private float alertHeight = 0.18f;
    [SerializeField] private float alertSize = 7f;

    private Target target;
    private TargetMovement movement;
    private Collider[] colliders;
    private Transform model;
    private Transform view;
    private GameManager gameManager;

    private float aimed;       // segundos seguidos en la mira
    private float calmLeft;
    private float joltLeft;
    private float modelScaleY;
    private TextMeshPro alert;
    private float alertShow;   // 0..1, para que el «!» aparezca y se vaya con un fundido corto

    private void Awake()
    {
        target = GetComponent<Target>();
        movement = GetComponent<TargetMovement>();
        colliders = GetComponents<Collider>();
        model = transform.childCount > 0 ? transform.GetChild(0) : null;
        if (model != null) modelScaleY = model.localScale.y;
        alert = NewAlert();
    }

    private void Start()
    {
        gameManager = SceneCache<GameManager>.Get();
        PlayerShoot shoot = SceneCache<PlayerShoot>.Get();
        view = shoot != null ? shoot.transform : (Camera.main != null ? Camera.main.transform : null);
    }

    private void Update()
    {
        UpdateJolt();

        bool playing = gameManager != null && gameManager.CurrentState == GameManager.GameState.Playing;
        if (!playing || movement == null || view == null || !target.CanBeHit)
        {
            if (playing || target.IsDead) aimed = 0f;
            UpdateAlert(false);
            return;
        }

        calmLeft = Mathf.Max(0f, calmLeft - Time.deltaTime);
        aimed = IsAimedAt() ? aimed + Time.deltaTime : 0f;

        if (aimed >= dwellTime && calmLeft <= 0f && movement.Startle())
        {
            aimed = 0f;
            calmLeft = calmTime;
            joltLeft = joltTime;
            alertShow = 1f;   // el «!» se queda un instante en rojo mientras se va
            GameLog.Info($"Bosque: {name} se asusta");
        }
        UpdateAlert(aimed > 0.05f && calmLeft <= 0f);
    }

    // «!» encima del animal: late más deprisa y enrojece según se acerca el susto; mira siempre a la cámara
    private void UpdateAlert(bool aimedNow)
    {
        if (alert == null) return;
        alertShow = Mathf.MoveTowards(alertShow, aimedNow ? 1f : 0f, Time.deltaTime * (aimedNow ? 12f : 4f));
        bool visible = alertShow > 0.01f;
        if (alert.enabled != visible) alert.enabled = visible;
        if (!visible || view == null) return;

        float danger = Mathf.Clamp01(aimed / dwellTime);
        if (!aimedNow) danger = 1f;   // ya asustado (o se aparta la mira): se va en rojo
        Bounds bounds = BoundsNow();
        Vector3 above = new Vector3(bounds.center.x, bounds.max.y + alertHeight, bounds.center.z);
        // Un poco hacia la cámara: a la misma profundidad que el animal se mezclaba con él (y con la valla)
        alert.transform.position = above + (view.position - above).normalized * 0.25f;
        alert.transform.rotation = Quaternion.LookRotation(alert.transform.position - view.position);
        float beat = 1f + 0.2f * Mathf.Abs(Mathf.Sin(Time.time * Mathf.Lerp(8f, 26f, danger)));
        alert.transform.localScale = Vector3.one * (alertShow * beat);
        Color c = Color.Lerp(alertCalm, alertScared, danger);
        c.a = alertShow;
        alert.color = c;
    }

    private Bounds BoundsNow()
    {
        foreach (Collider col in colliders)
            if (col != null && col.enabled) return col.bounds;
        return new Bounds(transform.position, Vector3.one * 0.5f);
    }

    // Texto 3D suelto (no hijo del modelo: el modelo se voltea con FaceDirection y se estira con el respingo)
    private TextMeshPro NewAlert()
    {
        var go = new GameObject("ShyAlert");
        go.transform.SetParent(transform, false);
        TextMeshPro text = go.AddComponent<TextMeshPro>();
        TMP_FontAsset font = alertFont != null ? alertFont : TMP_Settings.defaultFontAsset;
        if (font != null) text.font = font;
        text.text = "!";
        text.fontSize = alertSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Bottom;
        text.outlineWidth = 0.3f;
        text.outlineColor = new Color32(30, 15, 5, 255);
        text.rectTransform.sizeDelta = new Vector2(1f, 1f);
        text.rectTransform.pivot = new Vector2(0.5f, 0f);   // el «!» crece hacia arriba desde su posición, no tapa al animal
        text.enabled = false;
        return text;
    }

    private bool IsAimedAt()
    {
        Ray ray = new Ray(view.position, view.forward);
        foreach (Collider col in colliders)
            if (col != null && col.enabled && col.bounds.IntersectRay(ray))
                return true;
        return false;
    }

    // Solo la Y (FaceDirection escribe el signo de la X del mismo modelo) y solo mientras dura el respingo: el último
    // paso la deja como estaba y fuera de él no se toca, por si otro script anima la escala
    private void UpdateJolt()
    {
        if (model == null || jolt <= 0f || joltLeft <= 0f) return;
        joltLeft = Mathf.Max(0f, joltLeft - Time.deltaTime);
        float k = Mathf.Sin(Mathf.PI * (1f - joltLeft / joltTime));   // 0 → 1 → 0: al acabar queda en 0
        Vector3 scale = model.localScale;
        scale.y = modelScaleY * (1f + jolt * k);
        model.localScale = scale;
    }
}
