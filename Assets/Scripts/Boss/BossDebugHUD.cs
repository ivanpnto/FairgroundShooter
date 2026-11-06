using UnityEngine;

// HUD provisional de la prueba en bruto (I7.2): corazones, vida y estado del boss en una esquina.
// Solo existe en el editor y en builds de desarrollo. Lo sustituye el HUD de Jaime (J7.1) y entonces se
// quita de la escena; mientras, deja probar sin depender de él.
public class BossDebugHUD : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private PlayerHealth health;
    private BossController boss;
    private GameManager gameManager;
    private ScoreManager scoreManager;
    private GUIStyle style;

    private void Awake()
    {
        health = FindFirstObjectByType<PlayerHealth>();
        boss = FindFirstObjectByType<BossController>();
        gameManager = FindFirstObjectByType<GameManager>();
        scoreManager = FindFirstObjectByType<ScoreManager>();
    }

    private void OnGUI()
    {
        if (style == null)
            style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft };

        string hearts = health != null && health.IsActive
            ? new string('♥', health.Hearts) + new string('·', health.MaxHearts - health.Hearts) +
              (health.IsInvulnerable ? "  (invulnerable)" : "")
            : "—";
        string bossText = "sin boss";
        if (boss != null)
        {
            bossText = $"{boss.State}" + (boss.State == BossController.BossState.Telegraph ? $" ({boss.CurrentAttack})" : "") +
                       $" · fase {boss.PhaseIndex + 1}/{boss.PhaseCount} · vida {boss.Health:0.#}/{boss.MaxHealth:0.#}" +
                       $" · {boss.FightTime:0} s" + (boss.IsFurious ? " · FURIA" : "");
            if (boss.State == BossController.BossState.Telegraph && boss.CurrentAttack == BossAttack.ChargedHonk)
                bossText += $"\nMandíbula: {boss.HonkCounterProgress:P0}";
        }
        string game = gameManager != null
            ? $"{gameManager.CurrentState}" + (gameManager.CurrentState == GameManager.GameState.GameOver
                ? $" ({gameManager.LastGameOverReason})" : "")
            : "";
        string score = scoreManager != null ? $" · puntos {scoreManager.GetScore()} · racha {scoreManager.StreakCount}" : "";

        GUI.Box(new Rect(10, 10, 620, 106), $"Corazones: {hearts}\nBoss: {bossText}\nPartida: {game}{score}", style);
    }
#endif
}
