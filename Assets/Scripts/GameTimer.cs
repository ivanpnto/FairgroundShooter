using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    [SerializeField] private float gameDuration = 60f;
    [SerializeField] private TMP_Text timerText;

    private float timeRemaining;
    private GameManager gameManager;

    private void Awake()
    {
        gameManager = GetComponent<GameManager>();
        timeRemaining = gameDuration;
    }

    private void Update()
    {
        if (gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            gameManager.EndGame();
        }

        UpdateTimerText();
    }

    private void UpdateTimerText()
    {
        timerText.text = $"TIEMPO: {Mathf.CeilToInt(timeRemaining)}";
    }
}