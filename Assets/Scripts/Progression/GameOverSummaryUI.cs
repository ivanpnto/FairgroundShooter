using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Parte nueva del Game Over (Fase 5): créditos ganados y totales, resumen de la partida y botones de
// tienda y récords. OTRA RONDA sigue llamando a GameManager.RestartGame desde su OnClick en la escena.
// Los datos se leen al mostrarse: GameManager los deja preparados antes de activar el panel.
public class GameOverSummaryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private Button shopButton;
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

        shopButton.onClick.AddListener(() => { if (menu != null) menu.ShowShop(); });
        recordsButton.onClick.AddListener(() => { if (menu != null) menu.ShowRecords(); });
    }

    // Bloque del nivel 4 en el resumen. El cartel grande de victoria/derrota es ResultBannerUI (bloque 2);
    // aquí queda el detalle: tiempo, corazones y bonus, o cuánta vida le quedaba al boss
    private string BossLine()
    {
        BossController boss = SceneCache<BossController>.Get();
        if (boss == null || boss.State == BossController.BossState.Inactive)
            return "";

        // Una línea corta: el tablero del resumen es estrecho (el detalle grande ya lo dio el cartel)
        if (gameManager.LastGameOverReason == GameOverReason.Victory)
            return $"\n<color=#FFD24A>Boss derrotado en {FormatTime(boss.FightTime)}  ·  bonus +{boss.LastVictoryBonus}</color>";
        if (gameManager.LastGameOverReason == GameOverReason.Defeat)
            return $"\n<color=#FF8C73>El boss ganó a los {FormatTime(boss.FightTime)} (le quedaba un {(boss.MaxHealth > 0f ? boss.Health / boss.MaxHealth : 0f):P0})</color>";
        return "";
    }

    public static string FormatTime(float seconds) => $"{Mathf.FloorToInt(seconds / 60f)}:{Mathf.FloorToInt(seconds % 60f):00}";

    // También al volver de la tienda: los créditos totales habrán bajado si se ha comprado algo
    private void OnEnable()
    {
        if (gameManager == null || scoreManager == null)
            return;

        int total = profileManager != null ? profileManager.Credits : 0;
        creditsText.text = $"+{gameManager.LastCreditsEarned} CRÉDITOS  ·  TIENES {total}";

        summaryText.text =
            $"Bonus de precisión del último nivel: +{gameManager.LastAccuracyBonus} ({gameManager.LastLevelAccuracy:P0})\n" +
            $"Precisión de la partida: {scoreManager.RunAccuracy:P0} ({scoreManager.RunShotsHit}/{scoreManager.RunShotsFired})\n" +
            $"Racha máxima: {scoreManager.MaxStreak} aciertos seguidos" + BossLine();
    }
}
