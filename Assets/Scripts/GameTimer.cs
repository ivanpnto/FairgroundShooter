using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    [Tooltip("Solo se usa si no hay LevelManager; con niveles, la duración sale de cada LevelConfig")]
    [SerializeField] private float gameDuration = 60f;
    [SerializeField] private TMP_Text timerText;

    private float timeRemaining;
    private GameManager gameManager;

    // Cronómetro (I7.6): en el combate contra el boss no hay cuenta atrás; el tiempo sube y nunca acaba el nivel
    private bool stopwatch;
    private float elapsed;

    public float TimeRemaining => timeRemaining;
    public bool IsStopwatch => stopwatch;
    public float Elapsed => elapsed;

    private void Awake()
    {
        gameManager = GetComponent<GameManager>();
        timeRemaining = gameDuration;
    }

    private void Update()
    {
        if (gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        if (stopwatch)
        {
            elapsed += Time.deltaTime;
            UpdateTimerText();
            return;
        }

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
        if (timerText == null)
            return;
        timerText.text = stopwatch
            ? $"TIEMPO: {Mathf.FloorToInt(elapsed / 60f)}:{Mathf.FloorToInt(elapsed % 60f):00}"
            : $"TIEMPO: {Mathf.CeilToInt(timeRemaining)}";
    }

    // Lo llama LevelManager al empezar cada nivel
    public void StartLevel(float duration)
    {
        stopwatch = false;
        timeRemaining = duration;
        UpdateTimerText();
    }

    // Objetivos con contorno dorado
    public void AddTime(float seconds)
    {
        timeRemaining += seconds;
        UpdateTimerText();
    }

    // Lo llama BossReveal al empezar el combate: desde aquí el tiempo sube y OnTimeUp ya no llega
    public void StartStopwatch()
    {
        stopwatch = true;
        elapsed = 0f;
        UpdateTimerText();
    }

    public void ResetTimer()
    {
        stopwatch = false;
        timeRemaining = gameDuration;
        UpdateTimerText();
    }
}
