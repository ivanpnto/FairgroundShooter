using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Objetivo de puntos del nivel en pantalla (24/11):
// - Contador bajo la racha, «OBJETIVO 420 / 650»: gris mientras no llegas, verde con ✓ cuando lo superas. Si una
//   diana prohibida te baja de la meta, vuelve a gris: el contador dice siempre si ahora mismo pasarías.
// - Rótulo central breve: «NIVEL 1 · OBJETIVO: 650 PUNTOS» al empezar la partida (los demás niveles lo anuncian en
//   el cartel del telón) y «¡OBJETIVO SUPERADO!» la primera vez que llegas. Sin confeti: el confeti solo es acierto.
// Va en un objeto propio siempre activo (no dentro del HUD, que se apaga entre niveles), así recibe LevelStarted
// siempre; se ve solo cuando el HUD se ve.
public class ObjectiveUI : MonoBehaviour
{
    [SerializeField] private GameObject hud;
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private Image checkIcon;
    [SerializeField] private TMP_Text bannerText;

    [SerializeField] private Color pendingColor = Color.white;   // como el resto del HUD: el gris se perdía sobre las bombillas
    [SerializeField] private Color reachedColor = new Color(0.45f, 0.95f, 0.45f);
    [SerializeField, Min(0.1f)] private float introSeconds = 2.2f;
    [SerializeField, Min(0.1f)] private float reachedSeconds = 1.6f;

    private LevelManager levels;
    private ScoreManager score;
    private GameManager gameManager;

    private int target;
    private bool announcedReached;
    private float bannerTime = -1f, bannerDuration;
    private int lastShownScore = int.MinValue;

    private void Awake()
    {
        levels = FindFirstObjectByType<LevelManager>();
        score = FindFirstObjectByType<ScoreManager>();
        gameManager = FindFirstObjectByType<GameManager>();
        SetVisible(false);
        if (bannerText != null) bannerText.gameObject.SetActive(false);
    }

    private void OnEnable() { if (levels != null) levels.LevelStarted += HandleLevelStarted; }
    private void OnDisable() { if (levels != null) levels.LevelStarted -= HandleLevelStarted; }

    private void HandleLevelStarted(LevelConfig level, int index)
    {
        target = levels.TargetFor(level);
        announcedReached = false;
        lastShownScore = int.MinValue;
        HideBanner();
        if (index == 0 && target > 0)
            ShowBanner($"NIVEL 1  ·  OBJETIVO: {target} PUNTOS", introSeconds);
    }

    private void Update()
    {
        bool inRun = gameManager != null && gameManager.IsInRun;
        bool visible = inRun && target > 0 && (hud == null || hud.activeInHierarchy);
        SetVisible(visible);
        if (!inRun) HideBanner();

        if (visible && score != null)
        {
            int points = score.LevelScore;
            if (points != lastShownScore) Refresh(points);

            // Solo jugando: al acabar el tiempo el bonus de precisión sube LevelScore y no debe «superarse» entonces
            if (!announcedReached && LevelConfig.ObjectiveMet(points, target)
                && gameManager.CurrentState == GameManager.GameState.Playing)
            {
                announcedReached = true;
                ShowBanner("¡OBJETIVO SUPERADO!", reachedSeconds);
            }
        }

        AnimateBanner(visible);
    }

    private void Refresh(int points)
    {
        lastShownScore = points;
        bool met = LevelConfig.ObjectiveMet(points, target);
        counterText.text = $"OBJETIVO {points} / {target}";
        counterText.color = met ? reachedColor : pendingColor;
        if (checkIcon != null)
        {
            checkIcon.enabled = met;
            checkIcon.color = reachedColor;
            // El ✓ va justo detrás del texto (el ancho cambia con los dígitos). Las fuentes no tienen el carácter
            RectTransform rt = checkIcon.rectTransform;
            rt.anchoredPosition = new Vector2(counterText.preferredWidth + 14f, rt.anchoredPosition.y);
        }
    }

    private void SetVisible(bool visible)
    {
        if (counterText != null && counterText.gameObject.activeSelf != visible)
            counterText.gameObject.SetActive(visible);
    }

    private void ShowBanner(string text, float seconds)
    {
        if (bannerText == null) return;
        bannerText.text = text;
        bannerTime = 0f;
        bannerDuration = seconds;
        bannerText.gameObject.SetActive(true);
    }

    private void HideBanner()
    {
        bannerTime = -1f;
        if (bannerText != null && bannerText.gameObject.activeSelf) bannerText.gameObject.SetActive(false);
    }

    // Entra con un pequeño golpe de escala, se queda y se desvanece. Se congela en pausa (Time.deltaTime = 0)
    private void AnimateBanner(bool hudVisible)
    {
        if (bannerText == null || bannerTime < 0f) return;
        if (!hudVisible) { HideBanner(); return; }

        bannerTime += Time.deltaTime;
        if (bannerTime >= bannerDuration) { HideBanner(); return; }

        const float fadeIn = 0.15f, fadeOut = 0.4f;
        float alpha = Mathf.Min(1f, bannerTime / fadeIn, (bannerDuration - bannerTime) / fadeOut);
        float punch = 1f + 0.18f * Mathf.Max(0f, 1f - bannerTime / 0.25f);
        Color c = bannerText.color;
        c.a = Mathf.Clamp01(alpha);
        bannerText.color = c;
        bannerText.rectTransform.localScale = Vector3.one * punch;
    }
}
