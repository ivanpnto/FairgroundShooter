using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Waiting,
        Playing,
        GameOver
    }

    public GameState CurrentState { get; private set; } = GameState.Waiting;

    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject hud;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private LeaderboardUI leaderboardUI;

    private ScoreManager scoreManager;
    private GameTimer gameTimer;
    private WeaponAmmo weaponAmmo;
    private TargetSpawner targetSpawner;
    private string playerName;
    private LeaderboardManager leaderboardManager;

    private void Awake()
    {
        scoreManager = FindFirstObjectByType<ScoreManager>();
        gameTimer = FindFirstObjectByType<GameTimer>();
        weaponAmmo = FindFirstObjectByType<WeaponAmmo>();
        targetSpawner = FindFirstObjectByType<TargetSpawner>();
        leaderboardManager = FindFirstObjectByType<LeaderboardManager>();
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

        CurrentState = GameState.Playing;

        startPanel.SetActive(false);
        hud.SetActive(true);
        gameOverPanel.SetActive(false);

        Debug.Log($"Game state: {CurrentState}");
        Debug.Log($"Jugador: {playerName}");
    }

    public void EndGame()
    {
        if (CurrentState != GameState.Playing)
            return;

        CurrentState = GameState.GameOver;

        int finalScore = scoreManager.GetScore();

        leaderboardManager.AddScore(playerName, finalScore);

        finalScoreText.text = $"SCORE: {finalScore}";

        hud.SetActive(false);
        gameOverPanel.SetActive(true);

        leaderboardUI.UpdateRanking();

        Debug.Log($"Jugador: {playerName} | Puntuación: {finalScore}");
        Debug.Log($"Game state: {CurrentState}");
    }

    public void RestartGame()
    {
        scoreManager.ResetScore();
        gameTimer.ResetTimer();
        weaponAmmo.ResetAmmo();
        targetSpawner.ResetSpawner();

        CurrentState = GameState.Playing;

        startPanel.SetActive(false);
        hud.SetActive(true);
        gameOverPanel.SetActive(false);

        Debug.Log("Nueva partida iniciada.");
    }
}