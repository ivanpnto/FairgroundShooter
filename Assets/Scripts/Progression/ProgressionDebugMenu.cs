#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

// Menú de depuración (F1). Dos bloques:
//  · Progresión: dar 500 créditos, reiniciar el perfil actual y desbloquear todo.
//  · Modo prueba rápida (DebugMode): niveles de 10 s, 10 corazones contra el boss y revelación a los 2 s,
//    más atajos para la partida en curso (saltar de nivel, acabar el nivel, debilitar la fase del boss).
//
// Como el cambio de arma con 1–5, solo existe en el editor y en development builds: en una build normal no
// se compila, así que no sirve para hacer trampas.
//
// Se crea solo al arrancar (RuntimeInitializeOnLoadMethod) para no añadir nada a Main.unity.
// Usa IMGUI (OnGUI) porque es una herramienta interna: no necesita prefab ni estilo de feria.
public class ProgressionDebugMenu : MonoBehaviour
{
    private const int CreditsGift = 500;

    private ProfileManager profileManager;
    private WeaponController weaponController;
    private GameManager gameManager;
    private LevelManager levelManager;
    private GameTimer gameTimer;
    private PlayerHealth playerHealth;
    private BossController boss;
    private TargetSpawner spawner;
    private bool visible;
    private GUIStyle badgeStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        GameObject menu = new GameObject("ProgressionDebugMenu");
        menu.AddComponent<ProgressionDebugMenu>();
        DontDestroyOnLoad(menu);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            visible = !visible;
    }

    // Se buscan al abrir y no en Awake: este objeto nace antes de que existan en algunas cargas
    private void FindReferences()
    {
        if (profileManager == null) profileManager = FindFirstObjectByType<ProfileManager>();
        if (weaponController == null) weaponController = FindFirstObjectByType<WeaponController>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (levelManager == null) levelManager = FindFirstObjectByType<LevelManager>();
        if (gameTimer == null) gameTimer = FindFirstObjectByType<GameTimer>();
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (boss == null) boss = FindFirstObjectByType<BossController>();
        if (spawner == null) spawner = FindFirstObjectByType<TargetSpawner>();
    }

    private void OnGUI()
    {
        // Aviso permanente: con el menú cerrado es fácil olvidar que el modo está puesto
        if (DebugMode.Enabled || DebugMode.InfiniteAmmo)
            DrawBadge();

        if (!visible)
            return;

        FindReferences();
        GUILayout.BeginArea(new Rect(20, 20, 320, 470), "Depuración (F1)", GUI.skin.window);
        DrawProgression();
        GUILayout.Space(8);
        DrawTestMode();
        GUILayout.EndArea();
    }

    private void DrawBadge()
    {
        if (badgeStyle == null)
        {
            badgeStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, fontStyle = FontStyle.Bold };
            badgeStyle.normal.textColor = new Color(1f, 0.55f, 0.2f);
        }
        string text = DebugMode.Enabled ? "MODO PRUEBA (F1) · no cuenta para récords" : "MUNICIÓN INFINITA (F1)";
        if (DebugMode.Enabled && DebugMode.InfiniteAmmo) text += " · munición infinita";
        GUI.Label(new Rect(Screen.width * 0.5f - 200f, 6f, 400f, 26f), text, badgeStyle);
    }

    private void DrawProgression()
    {
        GUILayout.Label("— Progresión —");
        if (profileManager == null || profileManager.Current == null)
        {
            GUILayout.Label("No hay perfil elegido.");
            return;
        }

        PlayerProfile p = profileManager.Current;
        GUILayout.Label($"{p.name}: {p.credits} créditos · récord {p.bestScore} · {p.gamesPlayed} partidas");

        if (GUILayout.Button($"+{CreditsGift} créditos"))
            profileManager.AddCredits(CreditsGift);

        if (GUILayout.Button("Reiniciar el perfil actual"))
            profileManager.ResetCurrentProfile();

        if (GUILayout.Button("Desbloquear todo (armas y mejoras)"))
            profileManager.UnlockEverything(weaponController != null ? weaponController.AvailableWeapons : null);
    }

    private void DrawTestMode()
    {
        GUILayout.Label("— Modo prueba rápida —");

        bool enabled = GUILayout.Toggle(DebugMode.Enabled,
            $" Niveles de {DebugMode.LevelSeconds:0} s · {DebugMode.BossHearts} corazones · boss a los {DebugMode.MaxRevealDelay:0} s");
        if (enabled != DebugMode.Enabled)
            SetTestMode(enabled);

        DebugMode.InfiniteAmmo = GUILayout.Toggle(DebugMode.InfiniteAmmo, " Munición infinita (sin recargas)");

        bool playing = gameManager != null && gameManager.CurrentState == GameManager.GameState.Playing;
        if (!playing)
        {
            GUILayout.Label("Atajos de partida: empieza una partida para usarlos.");
            return;
        }

        // Saltar de nivel: el boss incluido aunque el perfil no lo haya comprado (es una herramienta de prueba)
        if (levelManager != null && levelManager.LevelCount > 0)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Ir al nivel:", GUILayout.Width(70));
            for (int i = 0; i < levelManager.LevelCount; i++)
                if (GUILayout.Button((i + 1).ToString()))
                    JumpToLevel(i);
            GUILayout.EndHorizontal();
        }

        bool countdown = gameTimer != null && !gameTimer.IsStopwatch;
        GUI.enabled = countdown;
        if (GUILayout.Button("Acabar el nivel ya (como si llegara a 0)"))
            gameManager.OnTimeUp();
        GUI.enabled = true;

        bool fighting = boss != null && boss.State != BossController.BossState.Inactive && !boss.IsDefeated;
        GUI.enabled = fighting;
        if (GUILayout.Button("Boss: dejar la fase con 0,1 de vida"))
            boss.DebugWeakenPhase();
        GUI.enabled = fighting && playerHealth != null && playerHealth.IsActive;
        if (GUILayout.Button("Rellenar corazones"))
            playerHealth.Activate();
        GUI.enabled = true;

        if (boss != null && fighting)
            GUILayout.Label($"Boss: fase {boss.PhaseIndex + 1}/{boss.PhaseCount} · vida {boss.Health:0.#}/{boss.MaxHealth:0.#} (×{boss.PowerScale:0.00})");
    }

    // Lo que ya está en marcha se adapta al momento, para no tener que reiniciar la partida:
    //  · la cuenta atrás se recorta a 10 s si quedaba más (al apagarlo no se devuelve: el nivel siguiente ya va bien);
    //  · si ya hay combate, los corazones pasan al nuevo máximo y se rellenan.
    private void SetTestMode(bool enabled)
    {
        DebugMode.Enabled = enabled;
        GameLog.Info($"DebugMode: {(enabled ? "activado" : "desactivado")}");

        if (enabled && gameTimer != null && !gameTimer.IsStopwatch && gameTimer.TimeRemaining > DebugMode.LevelSeconds)
            gameTimer.AddTime(DebugMode.LevelSeconds - gameTimer.TimeRemaining);

        // Solo en mitad de un combate: tras perder (GameOver) no se resucita al jugador
        bool playing = gameManager != null && gameManager.CurrentState == GameManager.GameState.Playing;
        if (playing && playerHealth != null && playerHealth.IsActive && !playerHealth.IsDead)
            playerHealth.Activate();
    }

    // Mismo orden que la transición normal (GameManager): primero se limpian las dianas y luego empieza el
    // nivel. LevelStarted ya reinicia el boss, la revelación, los corazones y la munición
    private void JumpToLevel(int index)
    {
        if (spawner != null) spawner.ResetSpawner();
        levelManager.StartLevel(index);
    }
}
#endif
