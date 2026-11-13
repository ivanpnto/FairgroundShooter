using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Barra de vida del boss (J7.1, bloque 2), arriba en el centro del HUD.
//
// - Solo se ve durante el combate: aparece al salir de Inactive y se va al volver a Inactive.
// - Relleno + «barra fantasma»: al recibir daño el relleno baja al momento y una segunda barra, más clara, le
//   sigue con retraso. Es el recurso típico de los juegos de lucha: se ve de un vistazo CUÁNTO daño acaba de
//   hacer el último disparo, sin números.
// - Marcas de fase: una raya donde empieza cada fase (66 % y 33 %), calculadas con los datos del boss, no fijas.
// - Aviso del bocinazo: durante la toma de aire y la ventana aparece «¡A LA BOCA!» con su propio progreso
//   (el daño que lleva la mandíbula frente al que hace falta para aturdirlo).
//
// Solo escucha eventos de BossController (HealthChanged, StateChanged, HonkCounterChanged): no toca la lógica.
public class BossHealthBarUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [Tooltip("Relleno: se escala con anchorMax.x (sprite en 9-slice, no se deforma)")]
    [SerializeField] private RectTransform fill;
    [SerializeField] private RectTransform ghost;
    [SerializeField] private Image fillImage;
    [SerializeField] private RectTransform phaseMarkTemplate;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text phaseText;

    [Header("Bocinazo")]
    [SerializeField] private CanvasGroup honkGroup;
    [SerializeField] private RectTransform honkFill;

    [Header("Ajustes")]
    [SerializeField] private float fadeSpeed = 4f;
    [SerializeField] private float ghostDelay = 0.45f;
    [SerializeField] private float ghostSpeed = 0.6f;   // fracción de la barra por segundo
    [SerializeField] private Color fillColor = new Color(0.86f, 0.16f, 0.16f);
    [SerializeField] private Color stunnedColor = new Color(0.30f, 0.85f, 0.95f);   // cian = punto débil abierto (§14)

    private BossController boss;
    private float shown, target01 = 1f, ghost01 = 1f, ghostWait, punch;
    private bool visible, marksBuilt;

    private void Awake()
    {
        boss = SceneCache<BossController>.Get();
        if (group != null) group.alpha = 0f;
        if (honkGroup != null) honkGroup.alpha = 0f;
        if (phaseMarkTemplate != null) phaseMarkTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (boss == null) return;
        boss.HealthChanged += HandleHealth;
        boss.StateChanged += HandleState;
        boss.PhaseStarted += HandlePhase;
        boss.HonkCounterChanged += HandleHonk;
        // El HUD se activa y desactiva entre niveles: al volver, la barra debe reflejar el estado actual
        visible = boss.State != BossController.BossState.Inactive;
        if (boss.MaxHealth > 0f) target01 = ghost01 = boss.Health / boss.MaxHealth;
        Apply();
    }

    private void OnDisable()
    {
        if (boss == null) return;
        boss.HealthChanged -= HandleHealth;
        boss.StateChanged -= HandleState;
        boss.PhaseStarted -= HandlePhase;
        boss.HonkCounterChanged -= HandleHonk;
    }

    private void HandleHealth(float health, float max)
    {
        float value = max > 0f ? Mathf.Clamp01(health / max) : 0f;
        if (value < target01) { ghostWait = ghostDelay; punch = 1f; }
        else ghost01 = value;   // al llenarse (nuevo combate) la fantasma no se queda atrás
        target01 = value;
        BuildMarks();
    }

    private void HandleState(BossController.BossState state)
    {
        visible = state != BossController.BossState.Inactive;
        if (fillImage != null)
            fillImage.color = state == BossController.BossState.Stunned ? stunnedColor : fillColor;
    }

    private void HandlePhase(int index)
    {
        if (phaseText != null) phaseText.text = $"FASE {index + 1}/{boss.PhaseCount}";
    }

    private void HandleHonk(float progress)
    {
        if (honkFill != null) honkFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
    }

    // Una marca donde empieza cada fase (salvo la primera, que es la barra llena)
    private void BuildMarks()
    {
        if (marksBuilt || phaseMarkTemplate == null || boss.MaxHealth <= 0f) return;
        for (int i = 1; i < boss.PhaseCount; i++)
        {
            float x = boss.HealthAtPhaseStart(i) / boss.MaxHealth;
            RectTransform mark = Instantiate(phaseMarkTemplate, phaseMarkTemplate.parent);
            mark.gameObject.SetActive(true);
            mark.anchorMin = mark.anchorMax = new Vector2(x, 0.5f);
            mark.anchoredPosition = Vector2.zero;
        }
        marksBuilt = true;
    }

    private void Update()
    {
        if (boss == null) return;

        shown = Mathf.MoveTowards(shown, visible ? 1f : 0f, Time.deltaTime * fadeSpeed);

        // Fantasma: espera un momento y luego alcanza al relleno
        if (ghostWait > 0f) ghostWait -= Time.deltaTime;
        else ghost01 = Mathf.MoveTowards(ghost01, target01, ghostSpeed * Time.deltaTime);

        punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 5f);

        // Aviso del bocinazo: mientras dura su aviso (toma de aire + ventana)
        bool honk = boss.State == BossController.BossState.Telegraph && boss.CurrentAttack == BossAttack.ChargedHonk;
        if (honkGroup != null)
            honkGroup.alpha = Mathf.MoveTowards(honkGroup.alpha, honk ? 1f : 0f, Time.deltaTime * 8f);

        Apply();
    }

    private void Apply()
    {
        if (group != null) group.alpha = shown;
        if (fill != null) fill.anchorMax = new Vector2(target01, 1f);
        if (ghost != null) ghost.anchorMax = new Vector2(Mathf.Max(ghost01, target01), 1f);
        // Golpecito de escala al recibir daño: el ojo nota el cambio aunque esté mirando al boss
        transform.localScale = Vector3.one * (1f + 0.04f * punch);
    }
}
