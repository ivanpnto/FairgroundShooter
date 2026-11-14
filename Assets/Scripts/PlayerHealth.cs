using UnityEngine;

// Vida del jugador (I7.1). Solo existe en el nivel del boss: en los niveles 1–3 está inactiva y
// TakeHit no hace nada, así que esos niveles no cambian.
//
// Se mide en corazones enteros (no en float como la vida de las dianas) porque es lo que ve el jugador
// y porque los golpes son de 1 o 2 (§27.5). Tras cada golpe hay una invulnerabilidad corta: si un
// proyectil y un esbirro llegan a la vez, solo cuenta el primero.
//
// Va en Player. Eventos para el HUD, la viñeta y la sacudida (Jaime, J7.1–J7.2) y para GameManager.
public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHearts = 3;
    [Tooltip("Segundos sin poder recibir daño después de cada golpe")]
    [SerializeField, Min(0f)] private float invulnerabilityAfterHit = 0.75f;

    public int Hearts { get; private set; }
    // Se fija al activarse: si el modo prueba se cambia a mitad de combate, no cambia hasta el siguiente
    public int MaxHearts => currentMax > 0 ? currentMax : maxHearts;   // > 0: antes de Awake
    public bool IsActive { get; private set; }
    public bool IsDead => IsActive && Hearts <= 0;
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    // Corazones que quita el golpe (1 o 2): la viñeta es más fuerte con 2
    public event System.Action<int> Damaged;
    // Corazones actuales y máximo, en cualquier cambio (golpe, curación, activar). Para el HUD
    public event System.Action<int, int> HeartsChanged;
    public event System.Action Died;

    private float invulnerableUntil;
    private int currentMax;
    private GameManager gameManager;
    private LevelManager levelManager;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        levelManager = FindFirstObjectByType<LevelManager>();
        currentMax = maxHearts;
    }

    // Se activa sola al empezar un nivel de boss y se apaga en cualquier otro (también al empezar una
    // partida nueva, que siempre arranca en el nivel 1). Así no hay que acordarse de reiniciarla
    private void OnEnable()
    {
        if (levelManager != null) levelManager.LevelStarted += HandleLevelStarted;
    }

    private void OnDisable()
    {
        if (levelManager != null) levelManager.LevelStarted -= HandleLevelStarted;
    }

    private void HandleLevelStarted(LevelConfig level, int index)
    {
        // I7.6: los corazones no se activan al empezar el nivel 4, sino con el combate (BossController.StartFight):
        // el nivel empieza como uno normal y los corazones destriparían la sorpresa
        Deactivate();
    }

    public void Activate()
    {
        IsActive = true;
        currentMax = DebugMode.Enabled ? DebugMode.BossHearts : maxHearts;   // F1: 10 en modo prueba
        Hearts = currentMax;
        invulnerableUntil = 0f;
        HeartsChanged?.Invoke(Hearts, currentMax);
    }

    public void Deactivate()
    {
        IsActive = false;
        currentMax = maxHearts;
        Hearts = currentMax;
    }

    // Devuelve true si el golpe ha quitado vida. Fuera de Playing (revelación, transición, fin) no se
    // puede perder vida: lo que llegue en ese momento se descarta
    public bool TakeHit(int amount)
    {
        if (!IsActive || IsDead || amount <= 0 || IsInvulnerable)
            return false;
        if (gameManager != null && gameManager.CurrentState != GameManager.GameState.Playing)
            return false;

        Hearts = Mathf.Max(0, Hearts - amount);
        invulnerableUntil = Time.time + invulnerabilityAfterHit;

        GameLog.Info($"PlayerHealth: −{amount} corazón(es) → {Hearts}/{currentMax}");
        Damaged?.Invoke(amount);
        HeartsChanged?.Invoke(Hearts, currentMax);

        if (Hearts <= 0)
            Died?.Invoke();
        return true;
    }

    // Objetivo especial rosa (§27.3). Devuelve false si ya estaba lleno: quien lo llame da el bonus de puntos
    public bool Heal(int amount)
    {
        if (!IsActive || IsDead || amount <= 0 || Hearts >= currentMax)
            return false;

        Hearts = Mathf.Min(currentMax, Hearts + amount);
        HeartsChanged?.Invoke(Hearts, currentMax);
        return true;
    }

    // Para el cambio de fase y la revelación: unos segundos en los que nada te quita vida
    public void GrantInvulnerability(float seconds)
    {
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
    }
}
