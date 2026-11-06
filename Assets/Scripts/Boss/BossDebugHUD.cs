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
    private GUIStyle style;

    private void Awake()
    {
        health = FindFirstObjectByType<PlayerHealth>();
        boss = FindFirstObjectByType<BossController>();
        gameManager = FindFirstObjectByType<GameManager>();
    }

    private void OnGUI()
    {
        if (style == null)
            style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft };

        string hearts = health != null && health.IsActive
            ? new string('♥', health.Hearts) + new string('·', health.MaxHearts - health.Hearts) +
              (health.IsInvulnerable ? "  (invulnerable)" : "")
            : "—";
        string bossText = boss != null
            ? $"{boss.State} · fase {boss.PhaseIndex + 1}/{boss.PhaseCount} · vida {boss.Health:0.##}/{boss.MaxHealth:0.##} " +
              $"(fase {boss.PhaseHealth:0.##})"
            : "sin boss";
        string game = gameManager != null
            ? $"{gameManager.CurrentState}" + (gameManager.CurrentState == GameManager.GameState.GameOver
                ? $" ({gameManager.LastGameOverReason})" : "")
            : "";

        GUI.Box(new Rect(10, 10, 560, 84), $"Corazones: {hearts}\nBoss: {bossText}\nPartida: {game}", style);
    }
#endif
}
