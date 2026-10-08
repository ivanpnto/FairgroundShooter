using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    [SerializeField] private int score = 0;
    [SerializeField] private TMP_Text scoreText;

    private GameManager gameManager;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
    }

    private void Start()
    {
        UpdateScoreText();
    }

    public void AddScore(int points)
    {
        if (gameManager == null)
            return;

        if (gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        score += points;

        Debug.Log("Puntuación: " + score);

        UpdateScoreText();
    }

    public int GetScore()
    {
        return score;
    }

    public void ResetScore()
    {
        score = 0;

        UpdateScoreText();
    }

    private void UpdateScoreText()
    {
        scoreText.text = "SCORE: " + score;
    }
}