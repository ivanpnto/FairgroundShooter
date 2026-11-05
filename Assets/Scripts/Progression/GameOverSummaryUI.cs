using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Parte nueva del Game Over (Fase 5): créditos ganados y totales, resumen de la partida y botones de
// habilidades, armería y récords. OTRA RONDA sigue llamando a GameManager.RestartGame desde su OnClick en la escena.
// Los datos se leen al mostrarse: GameManager los deja preparados antes de activar el panel.
public class GameOverSummaryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private Button skillsButton;
    // Era el botón TIENDA: el mismo ticket pasa a ser ARMERÍA, así no se pierde la referencia
    [FormerlySerializedAs("shopButton")]
    [SerializeField] private Button armoryButton;
    [SerializeField] private Button recordsButton;

    private GameManager gameManager;
    private ScoreManager scoreManager;
    private ProfileManager profileManager;
    private MenuController menu;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        scoreManager = FindFirstObjectByType<ScoreManager>();
        profileManager = FindFirstObjectByType<ProfileManager>();
        menu = FindFirstObjectByType<MenuController>();

        skillsButton.onClick.AddListener(() => { if (menu != null) menu.ShowSkills(); });
        armoryButton.onClick.AddListener(() => { if (menu != null) menu.ShowArmory(); });
        recordsButton.onClick.AddListener(() => { if (menu != null) menu.ShowRecords(); });
    }

    // También al volver de Habilidades o Armería: los créditos totales habrán bajado si se ha comprado algo
    private void OnEnable()
    {
        if (gameManager == null || scoreManager == null)
            return;

        int total = profileManager != null ? profileManager.Credits : 0;
        creditsText.text = $"+{gameManager.LastCreditsEarned} CRÉDITOS  ·  TIENES {total}";

        summaryText.text =
            $"Bonus de precisión del último nivel: +{gameManager.LastAccuracyBonus} ({gameManager.LastLevelAccuracy:P0})\n" +
            $"Precisión de la partida: {scoreManager.RunAccuracy:P0} ({scoreManager.RunShotsHit}/{scoreManager.RunShotsFired})\n" +
            $"Racha máxima: {scoreManager.MaxStreak} aciertos seguidos";
    }
}
