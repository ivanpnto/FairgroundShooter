using System.Collections;
using UnityEngine;
using TMPro;

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
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private LeaderboardUI leaderboardUI;

    [Header("Transición entre niveles (panel opcional)")]
    [SerializeField] private GameObject levelTransitionPanel;
    [SerializeField] private TMP_Text levelTransitionText;
    [SerializeField, Min(0f)] private float transitionDuration = 3f;

    private ScoreManager scoreManager;
    private GameTimer gameTimer;
    private WeaponAmmo weaponAmmo;
    private TargetSpawner targetSpawner;
    private LevelManager levelManager;
    private string playerName;
    private LeaderboardManager leaderboardManager;

    private void Awake()
    {
        scoreManager = FindFirstObjectByType<ScoreManager>();
        gameTimer = FindFirstObjectByType<GameTimer>();
        weaponAmmo = FindFirstObjectByType<WeaponAmmo>();
        targetSpawner = FindFirstObjectByType<TargetSpawner>();
        levelManager = FindFirstObjectByType<LevelManager>();
        leaderboardManager = FindFirstObjectByType<LeaderboardManager>();

        if (levelTransitionPanel != null)
            levelTransitionPanel.SetActive(false);
    }

    public void StartGame()
    {
        if (CurrentState != GameState.Waiting)
            return;

        playerName = nameInputField.text;

        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "Jugador";
        }

        BeginRun();

        Debug.Log($"Jugador: {playerName}");
    }

    public void RestartGame()
    {
        StopAllCoroutines();   // por si se reinicia en mitad de una transición
        scoreManager.ResetScore();

        BeginRun();

        Debug.Log("Nueva partida iniciada.");
    }

    // Una partida = todos los niveles seguidos con la puntuación acumulada
    private void BeginRun()
    {
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

    // Lo llama GameTimer cuando el tiempo del nivel llega a 0
    public void OnTimeUp()
    {
        if (CurrentState != GameState.Playing)
            return;

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
        LevelConfig next = levelManager.NextLevel;
        string message = $"¡NIVEL {finished} COMPLETADO!\nPUNTOS: {scoreManager.GetScore()}\n\nSIGUIENTE: {next.levelName.ToUpper()}";

        if (levelTransitionPanel != null)
        {
            if (levelTransitionText != null)
                levelTransitionText.text = message;

            levelTransitionPanel.SetActive(true);
        }

        Debug.Log(message.Replace('\n', ' '));

        yield return new WaitForSeconds(transitionDuration);

        if (levelTransitionPanel != null)
            levelTransitionPanel.SetActive(false);

        levelManager.StartLevel(levelManager.CurrentIndex + 1);
        CurrentState = GameState.Playing;
    }

    public void EndGame()
    {
        if (CurrentState != GameState.Playing)
            return;

        CurrentState = GameState.GameOver;
        targetSpawner.ResetSpawner();

        int finalScore = scoreManager.GetScore();

        leaderboardManager.AddScore(playerName, finalScore);

        finalScoreText.text = $"PUNTOS: {finalScore}";

        hud.SetActive(false);
        gameOverPanel.SetActive(true);

        leaderboardUI.UpdateRanking();

        Debug.Log($"Jugador: {playerName} | Puntuación: {finalScore}");
    }
}
