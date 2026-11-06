using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Lógica del boss (I7.2): máquina de estados en C# con un enum y una corrutina.
//
// Por qué la lógica aquí y no en el Animator: los tiempos de aviso, las ventanas y las fases son reglas
// de juego (§27.6) y se depuran mejor en código, con logs y valores de BossConfig. El Animator (I7.4)
// solo pinta: recibe un trigger con el nombre de cada estado, si lo tiene. Así la prueba en bruto con
// cubos y el modelo final usan exactamente la misma lógica.
//
//   Idle (abierto) → Telegraph (rojo, cerrado) → Attack (cerrado) → Vulnerable (abierto) → Idle …
//   Al vaciar la vida de una fase: PhaseChange (cerrado, sin atacar) → Idle de la fase siguiente.
//   Al vaciar la última: Dead → GameManager.EndGame(Victoria).
//
// El tiempo del boss solo corre en Playing: en la revelación, una transición o el fin, se congela.
[DisallowMultipleComponent]
public class BossController : MonoBehaviour
{
    public enum BossState { Inactive, Idle, Telegraph, Attack, Vulnerable, PhaseChange, Dead }

    [SerializeField] private BossConfig config;

    [Header("Piezas")]
    [Tooltip("Vacío = se buscan todos los BossWeakPoint hijos")]
    [SerializeField] private BossWeakPoint[] weakPoints;
    [Tooltip("Desde dónde salen los proyectiles (las manos). Se alternan")]
    [SerializeField] private Transform[] projectileOrigins;
    [Tooltip("Hacia dónde van (el centro de PlayerDamageZone). Vacío = se busca la zona en la escena")]
    [SerializeField] private Transform projectileDestination;
    [Tooltip("Lo que se pinta de rojo durante el aviso (en el modelo, la boca o las manos)")]
    [SerializeField] private Renderer[] telegraphRenderers;
    [SerializeField] private Material telegraphMaterial;
    [Tooltip("Opcional: luz roja que sube durante el aviso")]
    [SerializeField] private Light telegraphLight;
    [SerializeField, Min(0f)] private float telegraphLightIntensity = 8f;
    [Tooltip("Opcional (I7.4). Recibe un trigger con el nombre del estado, si existe el parámetro")]
    [SerializeField] private Animator animator;

    public BossState State { get; private set; } = BossState.Inactive;
    public int PhaseIndex { get; private set; }
    public int PhaseCount => config != null && config.phases != null ? config.phases.Length : 0;
    public float Health { get; private set; }            // vida total que queda (todas las fases)
    public float MaxHealth => config != null ? config.TotalHealth : 0f;
    public float PhaseHealth { get; private set; }       // vida que queda en la fase actual
    public bool IsDefeated => State == BossState.Dead;
    // Solo se le hace daño con los puntos débiles abiertos y si no está cambiando de fase
    public bool CanTakeDamage => (State == BossState.Idle || State == BossState.Vulnerable) && !phaseCleared;
    // Para el aturdido (×1,5, paso siguiente)
    public float DamageMultiplier { get; private set; } = 1f;

    public event System.Action<BossState> StateChanged;
    public event System.Action<float, float> HealthChanged;   // vida, máximo (barra de Jaime, J7.1)
    public event System.Action<int> PhaseStarted;             // índice de fase
    public event System.Action Defeated;

    private GameManager gameManager;
    private LevelManager levelManager;
    private Coroutine fight;
    private bool phaseCleared;   // la vida de la fase llegó a 0: hay que cortar lo que esté haciendo
    private int originIndex;
    private readonly List<BossProjectile> projectiles = new List<BossProjectile>();
    private readonly HashSet<int> animatorTriggers = new HashSet<int>();
    private Material[][] originalTelegraphMaterials;

    private BossConfig.Phase CurrentPhase => config.phases[PhaseIndex];
    private bool Playing => gameManager == null || gameManager.CurrentState == GameManager.GameState.Playing;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        levelManager = FindFirstObjectByType<LevelManager>();

        if (weakPoints == null || weakPoints.Length == 0)
            weakPoints = GetComponentsInChildren<BossWeakPoint>(true);
        foreach (BossWeakPoint point in weakPoints)
            if (point != null) point.Bind(this);

        if (projectileDestination == null)
        {
            PlayerDamageZone zone = FindFirstObjectByType<PlayerDamageZone>();
            if (zone != null) projectileDestination = zone.transform;
        }

        if (animator != null)
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger) animatorTriggers.Add(p.nameHash);

        if (telegraphRenderers != null)
        {
            originalTelegraphMaterials = new Material[telegraphRenderers.Length][];
            for (int i = 0; i < telegraphRenderers.Length; i++)
                if (telegraphRenderers[i] != null) originalTelegraphMaterials[i] = telegraphRenderers[i].sharedMaterials;
        }
        if (telegraphLight != null) telegraphLight.intensity = 0f;
    }

    private void OnEnable()
    {
        if (levelManager != null) levelManager.LevelStarted += HandleLevelStarted;
    }

    private void OnDisable()
    {
        if (levelManager != null) levelManager.LevelStarted -= HandleLevelStarted;
        StopFight();
    }

    // Empieza solo en un nivel de boss; cualquier otro nivel (también una partida nueva) lo apaga
    private void HandleLevelStarted(LevelConfig level, int index)
    {
        if (level != null && level.isBossLevel) StartFight();
        else StopFight();
    }

    public void StartFight()
    {
        if (config == null || PhaseCount == 0)
        {
            Debug.LogError("BossController: falta BossConfig o no tiene fases.", this);
            return;
        }

        StopFight();
        Health = MaxHealth;
        HealthChanged?.Invoke(Health, MaxHealth);
        fight = StartCoroutine(FightRoutine());
    }

    public void StopFight()
    {
        if (fight != null) StopCoroutine(fight);
        fight = null;

        // Los proyectiles en el aire desaparecen: ninguno pasa a la partida siguiente
        for (int i = projectiles.Count - 1; i >= 0; i--)
            if (projectiles[i] != null) Destroy(projectiles[i].gameObject);
        projectiles.Clear();

        SetTelegraph(false);
        SetWeakPointsOpen(false);
        SetState(BossState.Inactive);
    }

    // ---------------------------------------------------------------- máquina de estados

    private IEnumerator FightRoutine()
    {
        for (PhaseIndex = 0; PhaseIndex < PhaseCount; PhaseIndex++)
        {
            PhaseHealth = CurrentPhase.health;
            phaseCleared = false;
            PhaseStarted?.Invoke(PhaseIndex);
            Debug.Log($"Boss: fase {PhaseIndex + 1}/{PhaseCount} ({CurrentPhase.label}), vida {PhaseHealth}");

            // Ciclo de la fase hasta vaciar su vida. Cada espera se corta en cuanto phaseCleared se activa
            while (!phaseCleared)
            {
                SetState(BossState.Idle);
                SetWeakPointsOpen(true);
                yield return Wait(Random.Range(CurrentPhase.idleTime.x, CurrentPhase.idleTime.y));
                if (phaseCleared) break;

                BossAttack attack = PickAttack();

                SetState(BossState.Telegraph);
                SetWeakPointsOpen(false);
                yield return TelegraphRoutine(CurrentPhase.telegraphTime);

                SetState(BossState.Attack);
                yield return AttackRoutine(attack);

                SetState(BossState.Vulnerable);
                SetWeakPointsOpen(true);
                yield return Wait(CurrentPhase.vulnerableTime);
            }

            SetWeakPointsOpen(false);
            SetTelegraph(false);

            if (PhaseIndex < PhaseCount - 1)
            {
                SetState(BossState.PhaseChange);
                yield return Wait(config.phaseChangeDuration);
            }
        }

        SetState(BossState.Dead);
        Debug.Log("Boss: derrotado");
        Defeated?.Invoke();

        yield return Wait(config.deathDelay, interruptible: false);
        if (gameManager != null) gameManager.EndGame(GameOverReason.Victory);
        fight = null;
    }

    // Espera que solo avanza en Playing y se corta al vaciar la vida de la fase
    private IEnumerator Wait(float seconds, bool interruptible = true)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (interruptible && phaseCleared) yield break;
            if (Playing) elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator TelegraphRoutine(float seconds)
    {
        float elapsed = 0f;
        SetTelegraph(true);
        while (elapsed < seconds && !phaseCleared)
        {
            if (Playing) elapsed += Time.deltaTime;
            // La luz sube durante el aviso: se lee cuánto falta sin mirar un número (§27.6, regla 2)
            SetTelegraphLight(elapsed / seconds);
            yield return null;
        }
        SetTelegraph(false);
    }

    private BossAttack PickAttack()
    {
        BossAttack[] attacks = CurrentPhase.attacks;
        return attacks != null && attacks.Length > 0 ? attacks[Random.Range(0, attacks.Length)] : BossAttack.ProjectileRain;
    }

    private IEnumerator AttackRoutine(BossAttack attack)
    {
        switch (attack)
        {
            case BossAttack.ProjectileRain:
                yield return ProjectileRainRoutine();
                break;
        }
    }

    private IEnumerator ProjectileRainRoutine()
    {
        BossConfig.Phase phase = CurrentPhase;
        for (int i = 0; i < phase.projectilesPerVolley; i++)
        {
            // Tope de proyectiles a la vez (§27.5): se espera a que caiga alguno
            while (projectiles.Count >= phase.maxProjectilesAlive && !phaseCleared)
                yield return null;
            if (phaseCleared) yield break;

            ReleaseProjectile();
            yield return Wait(phase.projectileInterval);
        }
    }

    // Público para que, con el modelo, lo llame un Animation Event en el fotograma en que la mano suelta (I7.4)
    public void ReleaseProjectile()
    {
        if (config.projectilePrefab == null || projectileDestination == null)
        {
            Debug.LogWarning("BossController: falta el prefab del proyectil o el destino (PlayerDamageZone).", this);
            return;
        }

        Transform origin = projectileOrigins != null && projectileOrigins.Length > 0
            ? projectileOrigins[originIndex++ % projectileOrigins.Length]
            : transform;

        GameObject instance = Instantiate(config.projectilePrefab, origin.position, Random.rotation);
        BossProjectile projectile = instance.GetComponent<BossProjectile>();
        if (projectile == null)
        {
            Debug.LogError("BossController: el prefab del proyectil no tiene BossProjectile.", instance);
            Destroy(instance);
            return;
        }

        // Puntos con el multiplicador del nivel, como cualquier diana; vida y tiempo de vida, los del prefab
        Target target = instance.GetComponent<Target>();
        LevelConfig level = levelManager != null ? levelManager.CurrentLevel : null;
        if (target != null && level != null) target.ApplyLevel(level.pointsMultiplier, 1f, 1f);

        Vector2 jitter = Random.insideUnitCircle * config.projectileAimJitter;
        Vector3 destination = projectileDestination.position + projectileDestination.right * jitter.x
                              + projectileDestination.up * jitter.y;
        float flight = Random.Range(CurrentPhase.projectileFlightTime.x, CurrentPhase.projectileFlightTime.y);

        projectile.Removed += HandleProjectileRemoved;
        projectiles.Add(projectile);
        projectile.Launch(destination, flight, config.projectileHeartDamage);
    }

    private void HandleProjectileRemoved(BossProjectile projectile) => projectiles.Remove(projectile);

    // ---------------------------------------------------------------- daño

    // Lo llama BossWeakPoint. Devuelve el daño aplicado (0 si no se podía). El sobrante de la fase se pierde
    public float ApplyDamage(BossWeakPoint point, float damage)
    {
        if (!CanTakeDamage || damage <= 0f)
            return 0f;

        float applied = Mathf.Min(damage * DamageMultiplier, PhaseHealth);
        PhaseHealth -= applied;
        Health = Mathf.Max(0f, Health - applied);
        HealthChanged?.Invoke(Health, MaxHealth);

        // Igual que la vida de Target: por debajo de esto cuenta como 0 (el daño suma en float)
        if (PhaseHealth <= 0.001f)
        {
            phaseCleared = true;
            SetWeakPointsOpen(false);   // ya mismo, no al siguiente frame: el resto del disparo no hace daño
        }
        return applied;
    }

    // ---------------------------------------------------------------- presentación

    private void SetState(BossState state)
    {
        if (State == state)
            return;
        State = state;
        StateChanged?.Invoke(state);

        int hash = Animator.StringToHash(state.ToString());
        if (animator != null && animatorTriggers.Contains(hash))
            animator.SetTrigger(hash);
    }

    private void SetWeakPointsOpen(bool open)
    {
        if (weakPoints == null)
            return;

        string[] active = open && config != null && PhaseIndex < PhaseCount ? CurrentPhase.weakPointIds : null;
        foreach (BossWeakPoint point in weakPoints)
        {
            if (point == null) continue;
            bool isActive = active != null && System.Array.IndexOf(active, point.Id) >= 0;
            point.SetOpen(isActive);
        }
    }

    // Del 20 % al 100 % de la intensidad a lo largo del aviso
    private void SetTelegraphLight(float progress)
    {
        if (telegraphLight != null)
            telegraphLight.intensity = Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(progress)) * telegraphLightIntensity;
    }

    // Cambia los materiales una vez al entrar y salir del aviso (no en cada frame)
    private void SetTelegraph(bool on)
    {
        if (on) SetTelegraphLight(0f);
        else if (telegraphLight != null) telegraphLight.intensity = 0f;

        if (telegraphRenderers == null || telegraphMaterial == null || originalTelegraphMaterials == null)
            return;

        for (int i = 0; i < telegraphRenderers.Length; i++)
        {
            Renderer rend = telegraphRenderers[i];
            if (rend == null || originalTelegraphMaterials[i] == null) continue;

            if (on)
            {
                Material[] red = new Material[originalTelegraphMaterials[i].Length];
                for (int m = 0; m < red.Length; m++) red[m] = telegraphMaterial;
                rend.sharedMaterials = red;
            }
            else
            {
                rend.sharedMaterials = originalTelegraphMaterials[i];
            }
        }
    }
}
