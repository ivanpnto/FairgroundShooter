using TMPro;
using UnityEngine;

// Multiplicador de racha en el HUD («×2»). Solo presentación: escucha ScoreManager.StreakChanged.
// Al subir, un golpe de escala; al romperse, se tiñe de rojo y tiembla un instante. Con ×1 se queda
// atenuado para no distraer, pero visible: así el jugador sabe que existe.
[RequireComponent(typeof(TMP_Text))]
public class StreakUI : MonoBehaviour
{
    [SerializeField] private Color activeColor = new Color(1f, 0.82f, 0.29f);
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color brokenColor = new Color(1f, 0.25f, 0.2f);
    [Tooltip("Escala máxima del golpe al subir el multiplicador")]
    [SerializeField] private float punchScale = 1.6f;
    [SerializeField] private float animationTime = 0.35f;
    [Tooltip("Píxeles de temblor al romperse la racha")]
    [SerializeField] private float shakeAmount = 8f;

    private TMP_Text text;
    private ScoreManager scoreManager;
    private Vector3 basePosition;
    private float multiplier = 1f;
    private float punchTimer;
    private float breakTimer;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        scoreManager = FindFirstObjectByType<ScoreManager>();
        basePosition = transform.localPosition;
    }

    private void OnEnable()
    {
        if (scoreManager != null)
        {
            scoreManager.StreakChanged += HandleStreakChanged;
            multiplier = scoreManager.StreakMultiplier;
        }
        punchTimer = breakTimer = 0f;
        Paint();
    }

    private void OnDisable()
    {
        if (scoreManager != null) scoreManager.StreakChanged -= HandleStreakChanged;
        transform.localScale = Vector3.one;
        transform.localPosition = basePosition;
    }

    private void HandleStreakChanged(int count, float newMultiplier)
    {
        if (newMultiplier > multiplier)
            punchTimer = animationTime;
        else if (newMultiplier < multiplier)
            breakTimer = animationTime;

        multiplier = newMultiplier;
        Paint();
    }

    private void Paint()
    {
        text.text = $"×{multiplier:0.#}";
    }

    private void Update()
    {
        // Tiempo sin escalar, como el resto de la UI
        float dt = Time.unscaledDeltaTime;

        float scale = 1f;
        if (punchTimer > 0f)
        {
            punchTimer -= dt;
            float t = Mathf.Clamp01(punchTimer / animationTime);   // 1 → 0
            scale = Mathf.Lerp(1f, punchScale, t * t);
        }
        transform.localScale = Vector3.one * scale;

        Vector3 offset = Vector3.zero;
        Color color = multiplier > 1f ? activeColor : idleColor;
        if (breakTimer > 0f)
        {
            breakTimer -= dt;
            float t = Mathf.Clamp01(breakTimer / animationTime);
            offset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * shakeAmount * t;
            color = Color.Lerp(color, brokenColor, t);
        }
        transform.localPosition = basePosition + offset;
        text.color = color;
    }
}
