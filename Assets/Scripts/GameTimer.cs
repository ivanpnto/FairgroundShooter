using UnityEngine;

public class GameTimer : MonoBehaviour
{
    [SerializeField] private float gameDuration = 60f;

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

        Debug.Log($"Time remaining: {timeRemaining:F1}");

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            gameManager.EndGame();
        }
    }
}