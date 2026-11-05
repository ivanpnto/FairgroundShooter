using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    [SerializeField] private int score = 0;
    [SerializeField] private TMP_Text scoreText;

    // Racha (Fase 5): cada StreakStep aciertos seguidos el multiplicador sube StreakIncrement,
    // hasta MaxStreakMultiplier (×1 → ×1.5 → ×2 → ×2.5 → ×3)
    private const int StreakStep = 5;
    private const float StreakIncrement = 0.5f;
    private const float MaxStreakMultiplier = 3f;

    private GameManager gameManager;
    private PlayerShoot playerShoot;
    private LevelManager levelManager;
    private ProfileManager profileManager;

    // Aciertos SEGUIDOS a objetivos normales, contados por disparo (no por objetivo derribado)
    public int StreakCount { get; private set; }
    public float StreakMultiplier { get; private set; } = 1f;
    public int MaxStreak { get; private set; }   // de toda la partida (para el resumen final)

    // Del nivel actual (se reinician al empezar cada nivel): sirven para el bonus de precisión
    public int ShotsFired { get; private set; }
    public int ShotsHit { get; private set; }

    // De toda la partida (para el resumen final)
    public int RunShotsFired { get; private set; }
    public int RunShotsHit { get; private set; }

    // (aciertos seguidos, multiplicador). Lo escucha el HUD: si el multiplicador sube, celebra; si
    // vuelve a 1 desde algo mayor, se ha roto la racha
    public event System.Action<int, float> StreakChanged;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        playerShoot = FindFirstObjectByType<PlayerShoot>();
        levelManager = FindFirstObjectByType<LevelManager>();
        profileManager = FindFirstObjectByType<ProfileManager>();
    }

    private void OnEnable()
    {
        if (playerShoot != null) playerShoot.ShotResolved += HandleShot;
        if (levelManager != null) levelManager.LevelStarted += HandleLevelStarted;
    }

    private void OnDisable()
    {
        if (playerShoot != null) playerShoot.ShotResolved -= HandleShot;
        if (levelManager != null) levelManager.LevelStarted -= HandleLevelStarted;
    }

    private void Start()
    {
        UpdateScoreText();
    }

    // Lo llama Target al derribar un objetivo, con los puntos ya multiplicados por el nivel.
    // Los positivos se multiplican además por la mejora de puntos y por la racha; la penalización
    // se resta tal cual. ShotResolved llega DESPUÉS de Target.Hit(), así que un acierto puntúa con
    // la racha que había antes de contarlo
    public void AddScore(int points)
    {
        if (gameManager == null)
            return;

        if (gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        if (points > 0)
            points = Mathf.RoundToInt(points * UpgradeMultiplier() * StreakMultiplier);

        score += points;

        Debug.Log("Puntuación: " + score);

        UpdateScoreText();
    }

    private float UpgradeMultiplier() => profileManager != null
        ? WeaponStatsCalculator.ScoreMultiplier(profileManager.Current, profileManager.Upgrades)
        : 1f;

    private void HandleShot(ShotResult shot)
    {
        if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        ShotsFired++;
        RunShotsFired++;

        // Acierto = objetivo normal (puntos > 0). Dar a uno de penalización cuenta como fallo, también
        // para la precisión: no es un buen disparo. Uno de 0 puntos ni suma ni rompe la racha
        // Con penetración o disparo doble un disparo puede dar a varios objetivos: sigue contando como
        // UN acierto (§25), y basta con que uno sea de penalización para que sea un fallo
        bool penaltyHit = shot.HitTarget && shot.HitPenalty;
        bool normalHit = shot.HitTarget && !penaltyHit;

        if (normalHit)
        {
            ShotsHit++;
            RunShotsHit++;
            SetStreak(StreakCount + 1);
        }
        else if (!shot.HitTarget || penaltyHit)
        {
            SetStreak(0);
        }
    }

    private void SetStreak(int count)
    {
        if (count == StreakCount)
            return;

        StreakCount = count;
        MaxStreak = Mathf.Max(MaxStreak, count);
        StreakMultiplier = Mathf.Min(MaxStreakMultiplier, 1f + StreakIncrement * (count / StreakStep));

        StreakChanged?.Invoke(StreakCount, StreakMultiplier);
    }

    private void HandleLevelStarted(LevelConfig level, int index)
    {
        ShotsFired = 0;
        ShotsHit = 0;
    }

    // Precisión del nivel actual, 0..1 (0 si no hubo disparos)
    public float LevelAccuracy => ShotsFired > 0 ? (float)ShotsHit / ShotsFired : 0f;
    public float RunAccuracy => RunShotsFired > 0 ? (float)RunShotsHit / RunShotsFired : 0f;

    // Al terminar cada nivel: Round(aciertos / disparos × 100 × multiplicador de puntos del nivel).
    // Se suma directamente (sin racha ni mejora, y aunque el estado ya no sea Playing) y se devuelve
    // para mostrarlo en la pantalla de transición
    public int ApplyAccuracyBonus(float levelPointsMultiplier)
    {
        int bonus = Mathf.RoundToInt(LevelAccuracy * 100f * levelPointsMultiplier);
        score += bonus;
        UpdateScoreText();
        return bonus;
    }

    public int GetScore()
    {
        return score;
    }

    // Al empezar partida: puntuación, racha y contadores de disparos a cero
    public void ResetScore()
    {
        score = 0;
        ShotsFired = ShotsHit = RunShotsFired = RunShotsHit = 0;
        MaxStreak = 0;
        StreakCount = -1;   // fuerza el aviso aunque ya estuviera a 0, para que el HUD se reinicie
        SetStreak(0);

        UpdateScoreText();
    }

    private void UpdateScoreText()
    {
        scoreText.text = "SCORE: " + score;
    }
}
