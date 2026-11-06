using System.Collections;
using UnityEngine;
using TMPro;

// Por qué acaba la partida (I7.1). Tiempo = se acabó el último nivel normal; Derrota = el jugador perdió
// los corazones contra el boss; Victoria = venció al boss. Para la pantalla de fin (Jaime, J7.1)
public enum GameOverReason
{
    Time,
    Defeat,
    Victory
}

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Waiting,
        Playing,
        LevelTransition,   // pausa entre niveles: no hay objetivos, ni tiempo, ni disparos
        GameOver
    }

    public GameState CurrentState { get; private set; } = GameState.Waiting;

    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject hud;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text finalScoreText;

    [Header("Transición entre niveles (panel opcional)")]
    [SerializeField] private GameObject levelTransitionPanel;
    [SerializeField] private TMP_Text levelTransitionTitle;   // "¡NIVEL 1 SUPERADO!"
    [SerializeField] private TMP_Text levelTransitionScore;   // "PUNTOS: 1234"
    [SerializeField] private TMP_Text levelTransitionNext;    // "SIGUIENTE: FONDO MARINO · 3"
    [SerializeField] private TMP_Text levelTransitionBonus;   // "BONUS DE PRECISIÓN: +87 (87 %)"
    [SerializeField, Min(1)] private int transitionSeconds = 3;

    private ScoreManager scoreManager;
    private GameTimer gameTimer;
    private WeaponAmmo weaponAmmo;
    private TargetSpawner targetSpawner;
    private LevelManager levelManager;
    private string playerName;
    private ProfileManager profileManager;

    // Resultado de la última partida, para la pantalla de fin
    public int LastCreditsEarned { get; private set; }
    public GameOverReason LastGameOverReason { get; private set; } = GameOverReason.Time;

    // Se lanza al final de EndGame, con los créditos ya guardados
    public event System.Action<GameOverReason> GameEnded;

    private PlayerHealth playerHealth;

    private void Awake()
    {
        scoreManager = FindFirstObjectByType<ScoreManager>();
        gameTimer = FindFirstObjectByType<GameTimer>();
        weaponAmmo = FindFirstObjectByType<WeaponAmmo>();
        targetSpawner = FindFirstObjectByType<TargetSpawner>();
        levelManager = FindFirstObjectByType<LevelManager>();
        profileManager = FindFirstObjectByType<ProfileManager>();
        playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (levelTransitionPanel != null)
            levelTransitionPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.Died += HandlePlayerDied;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.Died -= HandlePlayerDied;
    }

    // Se conservan los puntos de los niveles 1–3 y los del boss hasta ahora (§27.7): EndGame no resta nada
    private void HandlePlayerDied() => EndGame(GameOverReason.Defeat);

    public void StartGame()
    {
        if (CurrentState != GameState.Waiting)
            return;

        // Se juega siempre con un perfil (lo elige la pantalla de perfiles): ahí van créditos y récord.
        // Sin ProfileManager en la escena se juega igual, como antes de la Fase 5
        if (profileManager != null && profileManager.Current == null)
        {
            Debug.LogWarning("GameManager: elige un perfil antes de jugar.", this);
            MenuController menu = FindFirstObjectByType<MenuController>();
            if (menu != null) menu.ShowProfiles();
            return;
        }

        playerName = profileManager != null ? profileManager.Current.name : "Jugador";

        BeginRun();

        Debug.Log($"Jugador: {playerName}");
    }

    public void RestartGame()
    {
        StopAllCoroutines();   // por si se reinicia en mitad de una transición

        BeginRun();

        Debug.Log("Nueva partida iniciada.");
    }

    // Una partida = todos los niveles seguidos con la puntuación acumulada
    private void BeginRun()
    {
        // Puntos, racha y contadores de disparos a cero, también en la primera partida
        scoreManager.ResetScore();
        weaponAmmo.ResetAmmo();

        if (levelManager != null)
        {
            levelManager.StartLevel(0);
        }
        else
        {
            Debug.LogWarning("GameManager: no hay LevelManager en la escena; no aparecerán objetivos.", this);
            gameTimer.ResetTimer();
            targetSpawner.ResetSpawner();
        }

        CurrentState = GameState.Playing;

        startPanel.SetActive(false);
        hud.SetActive(true);
        gameOverPanel.SetActive(false);

        if (levelTransitionPanel != null)
            levelTransitionPanel.SetActive(false);
    }

    // Bonus de precisión del último nivel terminado (lo muestra la pantalla de fin)
    public int LastAccuracyBonus { get; private set; }
    public float LastLevelAccuracy { get; private set; }

    // Lo llama GameTimer cuando el tiempo del nivel llega a 0
    public void OnTimeUp()
    {
        if (CurrentState != GameState.Playing)
            return;

        // Antes de cambiar de estado: el bonus se calcula con los disparos de este nivel
        float levelMultiplier = levelManager != null && levelManager.CurrentLevel != null
            ? levelManager.CurrentLevel.pointsMultiplier : 1f;
        LastLevelAccuracy = scoreManager.LevelAccuracy;
        LastAccuracyBonus = scoreManager.ApplyAccuracyBonus(levelMultiplier);
        Debug.Log($"Bonus de precisión: +{LastAccuracyBonus} ({LastLevelAccuracy:P0} de {scoreManager.ShotsFired} disparos)");

        if (levelManager != null && levelManager.HasNextLevel)
            StartCoroutine(LevelTransitionRoutine());
        else
            EndGame();
    }

    private IEnumerator LevelTransitionRoutine()
    {
        CurrentState = GameState.LevelTransition;
        targetSpawner.ResetSpawner();   // ningún objetivo pasa de un nivel al siguiente

        int finished = levelManager.CurrentIndex + 1;
        string nextName = levelManager.NextLevel.levelName.ToUpper();

        SetText(levelTransitionTitle, $"¡NIVEL {finished} SUPERADO!");
        SetText(levelTransitionScore, $"PUNTOS: {scoreManager.GetScore()}");
        SetText(levelTransitionBonus, $"BONUS DE PRECISIÓN: +{LastAccuracyBonus} ({LastLevelAccuracy:P0})");

        hud.SetActive(false);
        if (levelTransitionPanel != null)
            levelTransitionPanel.SetActive(true);

        Debug.Log($"Nivel {finished} superado. Puntos: {scoreManager.GetScore()}. Siguiente: {nextName}");

        // Cuenta atrás visible: el jugador sabe cuándo vuelve la acción
        for (int s = transitionSeconds; s > 0; s--)
        {
            SetText(levelTransitionNext, $"SIGUIENTE: {nextName}\nEMPIEZA EN {s}");
            yield return new WaitForSeconds(1f);
        }

        if (levelTransitionPanel != null)
            levelTransitionPanel.SetActive(false);
        hud.SetActive(true);

        levelManager.StartLevel(levelManager.CurrentIndex + 1);
        CurrentState = GameState.Playing;
    }

    // Escenas cortas sin control del jugador (la revelación del boss, §27.1). Se reutiliza LevelTransition:
    // ya significa "no hay disparos, ni tiempo, ni objetivos", y todo lo que mira el estado lo respeta.
    // Devuelven false si no se podía (p. ej. la partida ya ha acabado)
    public bool PauseForCutscene()
    {
        if (CurrentState != GameState.Playing)
            return false;
        CurrentState = GameState.LevelTransition;
        return true;
    }

    public bool ResumeFromCutscene()
    {
        if (CurrentState != GameState.LevelTransition)
            return false;
        CurrentState = GameState.Playing;
        return true;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    // Sin parámetro, como antes: fin por tiempo. Se conserva por si algún botón o script lo llama así
    public void EndGame() => EndGame(GameOverReason.Time);

    public void EndGame(GameOverReason reason)
    {
        if (CurrentState != GameState.Playing)
            return;

        CurrentState = GameState.GameOver;
        LastGameOverReason = reason;
        targetSpawner.ResetSpawner();

        int finalScore = scoreManager.GetScore();

        // Créditos, récord y partidas jugadas van al perfil (sustituye al ranking de scores.json)
        LastCreditsEarned = profileManager != null ? profileManager.RecordGameResult(finalScore) : 0;
        string profileName = profileManager != null && profileManager.Current != null ? profileManager.Current.name : playerName;
        int totalCredits = profileManager != null ? profileManager.Credits : 0;

        finalScoreText.text = $"PUNTOS: {finalScore}";

        hud.SetActive(false);
        gameOverPanel.SetActive(true);

        // Resumen para equilibrar precios y créditos (Fase 5, hito 6)
        Debug.Log($"Fin de partida ({reason}) · {profileName} · puntos {finalScore} · créditos +{LastCreditsEarned} (total {totalCredits}) · " +
                  $"precisión {scoreManager.RunAccuracy:P0} ({scoreManager.RunShotsHit}/{scoreManager.RunShotsFired}) · " +
                  $"racha máxima {scoreManager.MaxStreak}");

        GameEnded?.Invoke(reason);
    }
}
