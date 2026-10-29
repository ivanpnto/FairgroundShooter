using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    [Tooltip("Solo se usa si no hay LevelManager; con niveles, la duración sale de cada LevelConfig")]
    [SerializeField] private float gameDuration = 60f;
    [SerializeField] private TMP_Text timerText;

    private float timeRemaining;
    private GameManager gameManager;

    public float TimeRemaining => timeRemaining;

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
            UpdateTimerText();
            gameManager.OnTimeUp();   // el GameManager decide: siguiente nivel o fin de partida
            return;
        }

        UpdateTimerText();
    }

    private void UpdateTimerText()
    {
        timerText.text = $"TIEMPO: {Mathf.CeilToInt(timeRemaining)}";
    }

    // Lo llama LevelManager al empezar cada nivel
    public void StartLevel(float duration)
    {
        timeRemaining = duration;
        UpdateTimerText();
    }

    // Objetivos con contorno dorado
    public void AddTime(float seconds)
    {
        timeRemaining += seconds;
        UpdateTimerText();
    }

    public void ResetTimer()
    {
        timeRemaining = gameDuration;
        UpdateTimerText();
    }
}
