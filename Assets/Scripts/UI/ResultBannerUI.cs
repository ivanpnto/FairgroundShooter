using TMPro;
using UnityEngine;

// Cartel de victoria o derrota contra el boss (J7.1, bloque 2).
//
// GameManager.EndGame, cuando el motivo es Victory o Defeat, retrasa el resumen ResultBannerSeconds; durante ese
// rato el juego ya está parado (GameOver) y este cartel entra con un rebote sobre la escena, con la fanfarria o
// el trombón triste. Al acabarse se apaga solo y aparece el resumen de siempre.
// Fin por tiempo (niveles 1–3, o el nivel 4 sin llegar al boss): no hay cartel.
public class ResultBannerUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform card;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip victoryClip;
    [SerializeField] private AudioClip defeatClip;

    [SerializeField] private Color victoryColor = new Color(1f, 0.82f, 0.29f);
    [SerializeField] private Color defeatColor = new Color(1f, 0.45f, 0.38f);

    private GameManager gameManager;
    private float age = -1f, duration;

    private void Awake()
    {
        gameManager = SceneCache<GameManager>.Get();
        Hide();
    }

    private void OnEnable() { if (gameManager != null) gameManager.GameEnded += HandleGameEnded; }
    private void OnDisable() { if (gameManager != null) gameManager.GameEnded -= HandleGameEnded; }

    private void HandleGameEnded(GameOverReason reason)
    {
        if ((reason != GameOverReason.Victory && reason != GameOverReason.Defeat) || gameManager.ResultBannerSeconds <= 0f)
            return;

        bool victory = reason == GameOverReason.Victory;
        BossController boss = SceneCache<BossController>.Get();

        titleText.text = victory ? "¡VICTORIA!" : "¡DERROTA!";
        titleText.color = victory ? victoryColor : defeatColor;
        if (boss != null)
            subtitleText.text = victory
                ? $"EL GRAN FERIANTE, DERROTADO EN {GameOverSummaryUI.FormatTime(boss.FightTime)}\nBONUS +{boss.LastVictoryBonus}"
                : $"EL GRAN FERIANTE TE HA VENCIDO\nLE QUEDABA UN {(boss.MaxHealth > 0f ? boss.Health / boss.MaxHealth : 0f):P0} DE VIDA";

        if (audioSource != null)
        {
            AudioClip clip = victory ? victoryClip : defeatClip;
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        duration = gameManager.ResultBannerSeconds;
        age = 0f;
        group.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (age < 0f) return;

        age += Time.deltaTime;
        if (age >= duration || gameManager.CurrentState != GameManager.GameState.GameOver)
        {
            Hide();
            return;
        }

        // Entrada con rebote (ease out back) en 0,45 s y salida en los últimos 0,3 s
        float tin = Mathf.Clamp01(age / 0.45f);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float back = 1f + c3 * Mathf.Pow(tin - 1f, 3f) + c1 * Mathf.Pow(tin - 1f, 2f);
        float tout = Mathf.Clamp01((duration - age) / 0.3f);
        card.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, back);
        card.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 2.2f) * 2f);
        group.alpha = Mathf.Min(tin * 2f, tout);
    }

    private void Hide()
    {
        age = -1f;
        if (group != null)
        {
            group.alpha = 0f;
            group.gameObject.SetActive(false);
        }
    }
}
