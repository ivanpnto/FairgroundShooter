using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Lógica del boss (I7.2–I7.3): máquina de estados en C# con un enum y corrutinas.
//
// Por qué la lógica aquí y no en el Animator: los tiempos de aviso, las ventanas y las fases son reglas
// de juego (§27.6) y se depuran mejor en código, con logs y valores de BossConfig. El Animator (I7.4)
// solo pinta: recibe un trigger con el nombre de cada estado, si lo tiene. Así la prueba con cubos y el
// modelo final usan exactamente la misma lógica.
//
//   Idle (fase abierta) → elige ataque →
//     Lluvia:    Telegraph (manos rojas, todo cerrado) → Attack (tanda) → Vulnerable (fase abierta)
//     Bocinazo:  Telegraph (boca roja, SOLO la mandíbula abierta) →
//                  · 4 de daño a la mandíbula a tiempo → Stunned (TODO abierto, daño ×1,5)
//                  · si no → Attack (2 corazones) → Vulnerable
//   Al vaciar la vida de una fase: PhaseChange (pierde piezas, pausa) → Idle de la siguiente.
//   Al vaciar la última: Dead → GameManager.EndGame(Victoria).
//
// En paralelo, en las fases que lo tengan, "presión de fondo": algún proyectil suelto en Idle/Vulnerable.
// El tiempo del boss solo corre en Playing: en la revelación, una transición o el fin, se congela.
[DisallowMultipleComponent]
public class BossController : MonoBehaviour
{
    public enum BossState { Inactive, Idle, Telegraph, Attack, Vulnerable, Stunned, PhaseChange, Dead }

    // Qué puntos débiles están abiertos
    private enum OpenMode { None, Phase, Counter, All }

    [System.Serializable]
    public class AttackVisual
    {
        public BossAttack attack;
        [Tooltip("Se pintan de rojo durante el aviso")]
        public Renderer[] renderers;
        [Tooltip("Opcional: luz roja que sube durante el aviso")]
        public Light light;
        [Min(0f)] public float lightIntensity = 8f;
    }

    [System.Serializable]
    public class PhaseParts
    {
        [Tooltip("Piezas que salen despedidas al EMPEZAR esta fase (la 0 no suelta nada)")]
        public Transform[] parts;
    }

    [SerializeField] private BossConfig config;

    [Header("Piezas")]
    [Tooltip("Vacío = se buscan todos los BossWeakPoint hijos")]
    [SerializeField] private BossWeakPoint[] weakPoints;
    [Tooltip("Desde dónde salen los proyectiles (las manos). Se alternan")]
    [SerializeField] private Transform[] projectileOrigins;
    [Tooltip("Hacia dónde van (el centro de PlayerDamageZone). Vacío = se busca la zona en la escena")]
    [SerializeField] private Transform projectileDestination;

    [Header("Aviso (rojo = va a atacar)")]
    [SerializeField] private AttackVisual[] attackVisuals;
    [SerializeField] private Material telegraphMaterial;

    [Header("Mandíbula (bocinazo)")]
    [Tooltip("Pivote de la mandíbula (origen en la bisagra). Se abre durante el bocinazo")]
    [SerializeField] private Transform jawPivot;
    [Tooltip("Giro de apertura. Negativo en X baja el frente con la bisagra detrás (positivo la metería en la cabeza)")]
    [SerializeField] private Vector3 jawOpenEuler = new Vector3(-28f, 0f, 0f);

    [Header("Reacción a los impactos (en el modelo, la capa additive del Animator la sustituye)")]
    [Tooltip("Lo que se tambalea (vacío = nada). Con cubos, el pivote del cuerpo; con el modelo, nada")]
    [SerializeField] private Transform flinchPivot;
    [Tooltip("Grados de tambaleo por unidad de daño")]
    [SerializeField, Min(0f)] private float flinchPerDamage = 2.5f;
    [SerializeField, Min(0f)] private float maxFlinch = 9f;

    [Header("Cambio de fase")]
    [Tooltip("Índice = fase que empieza. Esas piezas salen despedidas con física")]
    [SerializeField] private PhaseParts[] partsDroppedAtPhase;
    [SerializeField, Min(0f)] private float partImpulse = 4f;
    [SerializeField, Min(0.1f)] private float partLifetime = 3.5f;

    [Tooltip("Opcional (I7.4). Recibe triggers con el nombre del estado (y Telegraph<Ataque>), si existen")]
    [SerializeField] private Animator animator;

    // ---------------------------------------------------------------- estado público y eventos

    public BossState State { get; private set; } = BossState.Inactive;
    public BossAttack CurrentAttack { get; private set; }
    public int PhaseIndex { get; private set; }
    public int PhaseCount => config != null && config.phases != null ? config.phases.Length : 0;
    public float Health { get; private set; }            // vida total que queda (todas las fases)
    public float MaxHealth => config != null ? config.TotalHealth : 0f;
    public float PhaseHealth { get; private set; }       // vida que queda en la fase actual
    public bool IsDefeated => State == BossState.Dead;
    public bool IsFurious { get; private set; }
    public float FightTime { get; private set; }         // segundos de combate (solo en Playing)
    public float DamageMultiplier { get; private set; } = 1f;
    public float HonkCounterProgress => config != null ? Mathf.Clamp01(honkDamage / config.honkCounterDamage) : 0f;

    // Se le hace daño si hay algún punto débil abierto y no está cambiando de fase
    public bool CanTakeDamage => openMode != OpenMode.None && !phaseCleared && State != BossState.Dead;

    public event System.Action<BossState> StateChanged;
    public event System.Action<float, float> HealthChanged;   // vida, máximo (barra de Jaime, J7.1)
    public event System.Action<int> PhaseStarted;             // índice de fase
    public event System.Action<float> HonkCounterChanged;     // 0..1 (para que la boca/sonido reaccionen)
    public event System.Action<bool> HonkResolved;            // true = contrarrestado (aturdido); false = golpe
    public event System.Action FuryStarted;
    public event System.Action Defeated;

    // ---------------------------------------------------------------- privado

    private GameManager gameManager;
    private LevelManager levelManager;
    private ScoreManager scoreManager;
    private PlayerHealth playerHealth;

    private Coroutine fight, background;
    private bool phaseCleared;          // la vida de la fase llegó a 0: hay que cortar lo que esté haciendo
    private OpenMode openMode = OpenMode.None;
    private float honkDamage;
    private bool lastWasHonk;
    private BossAttack nextAttack;      // se elige al EMPEZAR el reposo (ver BackgroundProjectilesRoutine)
    private int attacksThisPhase;
    private int originIndex;

    private readonly List<BossProjectile> projectiles = new List<BossProjectile>();
    private readonly HashSet<int> animatorTriggers = new HashSet<int>();
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

    private float jawOpen, jawTarget;   // 0 cerrada, 1 abierta
    private Quaternion jawClosedRotation;
    private float flinch;               // grados de tambaleo que quedan
    private Quaternion flinchBaseRotation;

    private struct PartPose { public Transform part; public Transform parent; public Vector3 position; public Quaternion rotation; }
    private readonly List<PartPose> partPoses = new List<PartPose>();

    private BossConfig.Phase CurrentPhase => config.phases[Mathf.Clamp(PhaseIndex, 0, PhaseCount - 1)];
    private bool Playing => gameManager == null || gameManager.CurrentState == GameManager.GameState.Playing;
    private float Fury => IsFurious ? config.furyTimeMultiplier : 1f;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        levelManager = FindFirstObjectByType<LevelManager>();
        scoreManager = FindFirstObjectByType<ScoreManager>();
        playerHealth = FindFirstObjectByType<PlayerHealth>();

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

        if (attackVisuals != null)
            foreach (AttackVisual visual in attackVisuals)
            {
                if (visual.light != null) visual.light.intensity = 0f;
                if (visual.renderers == null) continue;
                foreach (Renderer rend in visual.renderers)
                    if (rend != null && !originalMaterials.ContainsKey(rend)) originalMaterials.Add(rend, rend.sharedMaterials);
            }

        if (jawPivot != null) jawClosedRotation = jawPivot.localRotation;
        if (flinchPivot != null) flinchBaseRotation = flinchPivot.localRotation;

        // Pose original de las piezas que se sueltan, para recolocarlas en la partida siguiente
        if (partsDroppedAtPhase != null)
            foreach (PhaseParts group in partsDroppedAtPhase)
                if (group != null && group.parts != null)
                    foreach (Transform part in group.parts)
                        if (part != null)
                            partPoses.Add(new PartPose { part = part, parent = part.parent, position = part.localPosition, rotation = part.localRotation });
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
        RestoreParts();
        Health = MaxHealth;
        FightTime = 0f;
        IsFurious = false;
        HealthChanged?.Invoke(Health, MaxHealth);
        fight = StartCoroutine(FightRoutine());
        background = StartCoroutine(BackgroundProjectilesRoutine());
    }

    public void StopFight()
    {
        // Todas: la pelea, la presión de fondo y las que ocultan piezas sueltas (si no, una pieza recolocada
        // en la partida siguiente se ocultaría sola al acabar su cuenta)
        StopAllCoroutines();
        fight = background = null;

        ClearProjectiles();
        ShowTelegraph(false);
        DamageMultiplier = 1f;
        jawTarget = jawOpen = 0f;
        if (jawPivot != null) jawPivot.localRotation = jawClosedRotation;
        flinch = 0f;
        if (flinchPivot != null) flinchPivot.localRotation = flinchBaseRotation;
        SetOpen(OpenMode.None);
        SetState(BossState.Inactive);
    }

    private void Update()
    {
        if (State == BossState.Inactive)
            return;

        if (Playing && State != BossState.Dead)
        {
            FightTime += Time.deltaTime;
            if (!IsFurious && config.furyTime > 0f && FightTime >= config.furyTime)
            {
                IsFurious = true;
                Debug.Log("Boss: ¡FURIA!");
                FuryStarted?.Invoke();
            }
        }

        // Mandíbula: se acerca a su objetivo con suavidad (se abre durante el aviso del bocinazo)
        if (jawPivot != null)
        {
            jawOpen = Mathf.MoveTowards(jawOpen, jawTarget, Time.deltaTime * 4f);
            jawPivot.localRotation = jawClosedRotation * Quaternion.Euler(jawOpenEuler * jawOpen);
        }

        // Tambaleo: oscilación que se apaga. Aturdido, nunca baja de un mínimo (se le ve "grogui")
        if (flinchPivot != null)
        {
            float floor = State == BossState.Stunned ? 3f : (State == BossState.Dead ? 6f : 0f);
            flinch = Mathf.Max(floor, Mathf.MoveTowards(flinch, 0f, Time.deltaTime * 10f));
            float t = Time.time;
            flinchPivot.localRotation = flinchBaseRotation *
                Quaternion.Euler(Mathf.Sin(t * 13f) * flinch * 0.4f, 0f, Mathf.Sin(t * 9f) * flinch);
        }
    }

    // ---------------------------------------------------------------- máquina de estados

    private IEnumerator FightRoutine()
    {
        for (PhaseIndex = 0; PhaseIndex < PhaseCount; PhaseIndex++)
        {
            PhaseHealth = CurrentPhase.health;
            phaseCleared = false;
            attacksThisPhase = 0;
            lastWasHonk = false;
            PhaseStarted?.Invoke(PhaseIndex);
            Debug.Log($"Boss: fase {PhaseIndex + 1}/{PhaseCount} ({CurrentPhase.label}), vida {PhaseHealth}");

            // Ciclo de la fase hasta vaciar su vida. Cada espera se corta en cuanto phaseCleared se activa.
            // Los puntos débiles se abren/cierran ANTES de anunciar el estado: quien escuche StateChanged
            // (efectos, Animator) ya los ve como corresponde
            while (!phaseCleared)
            {
                // El ataque se decide ya, al empezar el reposo: si va a ser el bocinazo, este reposo no suelta
                // proyectiles de fondo (uno lanzado justo antes cruzaría la línea de tiro hacia la mandíbula)
                nextAttack = PickAttack();
                SetOpen(OpenMode.Phase);
                SetState(BossState.Idle);
                yield return Wait(Random.Range(CurrentPhase.idleTime.x, CurrentPhase.idleTime.y) * Fury);
                if (phaseCleared) break;

                CurrentAttack = nextAttack;
                attacksThisPhase++;
                lastWasHonk = CurrentAttack == BossAttack.ChargedHonk;

                if (CurrentAttack == BossAttack.ChargedHonk) yield return HonkRoutine();
                else yield return RainRoutine();
            }

            // Fase vaciada
            SetOpen(OpenMode.None);
            ShowTelegraph(false);
            DamageMultiplier = 1f;
            jawTarget = 0f;

            if (PhaseIndex < PhaseCount - 1)
            {
                SetState(BossState.PhaseChange);
                ClearProjectiles();
                if (playerHealth != null) playerHealth.GrantInvulnerability(config.phaseChangeDuration);
                flinch = maxFlinch;
                DropParts(PhaseIndex + 1);
                yield return Wait(config.phaseChangeDuration, interruptible: false);
            }
        }

        SetState(BossState.Dead);
        ClearProjectiles();
        flinch = maxFlinch;
        for (int i = 0; partsDroppedAtPhase != null && i < partsDroppedAtPhase.Length; i++) DropParts(i);
        Debug.Log($"Boss: derrotado en {FightTime:0.0} s");
        Defeated?.Invoke();

        yield return Wait(config.deathDelay, interruptible: false);
        if (gameManager != null) gameManager.EndGame(GameOverReason.Victory);
        fight = null;
    }

    // El último ataque de la lista es el nuevo de la fase: se estrena con él. Después, al azar, sin repetir
    // el bocinazo dos veces seguidas (es el peligroso: entre uno y otro tiene que haber respiro)
    private BossAttack PickAttack()
    {
        BossAttack[] attacks = CurrentPhase.attacks;
        if (attacks == null || attacks.Length == 0)
            return BossAttack.ProjectileRain;
        if (attacksThisPhase == 0)
            return attacks[attacks.Length - 1];

        for (int tries = 0; tries < 8; tries++)
        {
            BossAttack pick = attacks[Random.Range(0, attacks.Length)];
            if (!(lastWasHonk && pick == BossAttack.ChargedHonk)) return pick;
        }
        return BossAttack.ProjectileRain;
    }

    private IEnumerator RainRoutine()
    {
        SetOpen(OpenMode.None);
        SetState(BossState.Telegraph);
        yield return TelegraphWait(CurrentPhase.telegraphTime * Fury, null);
        if (phaseCleared) yield break;

        SetState(BossState.Attack);
        BossConfig.Phase phase = CurrentPhase;
        for (int i = 0; i < phase.projectilesPerVolley; i++)
        {
            // Tope de proyectiles a la vez (§27.5): se espera a que caiga alguno
            while (projectiles.Count >= phase.maxProjectilesAlive && !phaseCleared)
                yield return null;
            if (phaseCleared) yield break;

            ReleaseProjectile();
            yield return Wait(phase.projectileInterval * Fury);
        }

        SetOpen(OpenMode.Phase);
        SetState(BossState.Vulnerable);
        yield return Wait(CurrentPhase.vulnerableTime);
    }

    private IEnumerator HonkRoutine()
    {
        // 1) Toma de aire: todo cerrado, cabeza roja y luz tenue. Se ve venir y da tiempo a recargar
        honkDamage = 0f;
        HonkCounterChanged?.Invoke(0f);
        SetOpen(OpenMode.None);
        SetState(BossState.Telegraph);
        ShowTelegraph(true);
        float windup = config.honkWindupTime * Fury;
        for (float elapsed = 0f; elapsed < windup && !phaseCleared; )
        {
            if (Playing) elapsed += Time.deltaTime;
            SetTelegraphLight(0.15f * elapsed / windup);
            yield return null;
        }
        if (phaseCleared) yield break;

        // 2) Ventana: la boca se abre; solo la mandíbula recibe daño y ese daño cuenta para cancelarlo
        SetOpen(OpenMode.Counter);
        jawTarget = 1f;

        float time = Mathf.Max(config.honkTelegraphTime * Fury, IsFurious ? config.honkMinTelegraphTime : 0f);
        bool countered = false;
        yield return TelegraphWait(time, () => countered = honkDamage >= config.honkCounterDamage);
        if (phaseCleared) yield break;   // la mandíbula se llevó la última vida de la fase

        if (countered)
        {
            Debug.Log("Boss: ¡bocinazo contrarrestado! Aturdido");
            HonkResolved?.Invoke(true);
            DamageMultiplier = config.stunDamageMultiplier;
            jawTarget = 0.35f;   // queda con la boca entreabierta, grogui
            SetOpen(OpenMode.All);
            SetState(BossState.Stunned);
            flinch = maxFlinch;
            yield return Wait(config.stunDuration);
            DamageMultiplier = 1f;
            jawTarget = 0f;
            yield break;
        }

        // No se contrarrestó: golpe de 2 corazones
        SetOpen(OpenMode.None);
        SetState(BossState.Attack);
        HonkResolved?.Invoke(false);
        if (playerHealth != null) playerHealth.TakeHit(config.honkHeartDamage);
        yield return Wait(config.honkRecoveryTime);
        jawTarget = 0f;

        SetOpen(OpenMode.Phase);
        SetState(BossState.Vulnerable);
        yield return Wait(CurrentPhase.vulnerableTime);
    }

    // Aviso con la luz subiendo; se corta antes si stopWhen se cumple o se vacía la fase
    private IEnumerator TelegraphWait(float seconds, System.Func<bool> stopWhen)
    {
        ShowTelegraph(true);
        float elapsed = 0f;
        while (elapsed < seconds && !phaseCleared)
        {
            if (stopWhen != null && stopWhen()) break;
            if (Playing) elapsed += Time.deltaTime;
            // La luz sube durante el aviso: se lee cuánto falta sin mirar un número (§27.6, regla 2)
            SetTelegraphLight(elapsed / seconds);
            yield return null;
        }
        if (stopWhen != null) stopWhen();
        ShowTelegraph(false);
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

    // ---------------------------------------------------------------- proyectiles

    // Proyectiles sueltos en reposo o vulnerable: nunca durante un aviso ni un ataque (un peligro a la vez)
    private IEnumerator BackgroundProjectilesRoutine()
    {
        float timer = 0f;
        while (true)
        {
            yield return null;
            if (!Playing || PhaseCount == 0) continue;

            float interval = CurrentPhase.backgroundProjectileInterval * Fury;
            // Tampoco en el reposo que precede al bocinazo: un proyectil lanzado justo antes estaría en el aire
            // durante su ventana, tapando la mandíbula (en la prueba hacía fallar casi siempre el de la fase 3)
            bool calm = State == BossState.Vulnerable ||
                        (State == BossState.Idle && nextAttack != BossAttack.ChargedHonk);
            if (interval <= 0f || !calm || phaseCleared) { timer = 0f; continue; }

            timer += Time.deltaTime;
            if (timer >= interval && projectiles.Count < CurrentPhase.maxProjectilesAlive)
            {
                timer = 0f;
                ReleaseProjectile();
            }
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
        projectile.Launch(destination, flight, config.projectileArcHeight, config.projectileHeartDamage);
    }

    private void HandleProjectileRemoved(BossProjectile projectile) => projectiles.Remove(projectile);

    private void ClearProjectiles()
    {
        for (int i = projectiles.Count - 1; i >= 0; i--)
            if (projectiles[i] != null) Destroy(projectiles[i].gameObject);
        projectiles.Clear();
    }

    // ---------------------------------------------------------------- daño

    // Lo llama BossWeakPoint. Devuelve el daño aplicado (0 si no se podía). El sobrante de la fase se pierde
    public float ApplyDamage(BossWeakPoint point, float damage)
    {
        if (!CanTakeDamage || damage <= 0f)
            return 0f;

        float raw = damage * DamageMultiplier;

        // Durante el aviso del bocinazo, el daño a la mandíbula cuenta para cancelarlo
        if (openMode == OpenMode.Counter && point.Id == config.honkCounterWeakPointId)
        {
            honkDamage += raw;
            HonkCounterChanged?.Invoke(HonkCounterProgress);
        }

        float applied = Mathf.Min(raw, PhaseHealth);
        PhaseHealth -= applied;
        Health = Mathf.Max(0f, Health - applied);
        HealthChanged?.Invoke(Health, MaxHealth);
        flinch = Mathf.Min(maxFlinch, flinch + applied * flinchPerDamage);

        // Puntos por daño, con el multiplicador del nivel (AddScore pone mejora y racha)
        if (scoreManager != null && config.weakPointPointsPerDamage > 0f)
        {
            LevelConfig level = levelManager != null ? levelManager.CurrentLevel : null;
            float levelMultiplier = level != null ? level.pointsMultiplier : 1f;
            scoreManager.AddScore(Mathf.RoundToInt(applied * config.weakPointPointsPerDamage * levelMultiplier));
        }

        // Igual que la vida de Target: por debajo de esto cuenta como 0 (el daño suma en float)
        if (PhaseHealth <= 0.001f)
        {
            phaseCleared = true;
            SetOpen(OpenMode.None);   // ya mismo, no al siguiente frame: el resto del disparo no hace daño
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

        if (animator == null)
            return;
        TrySetTrigger(state.ToString());
        if (state == BossState.Telegraph)
            TrySetTrigger("Telegraph" + CurrentAttack);   // un clip de aviso por ataque (§27.2)
    }

    private void TrySetTrigger(string name)
    {
        int hash = Animator.StringToHash(name);
        if (animatorTriggers.Contains(hash)) animator.SetTrigger(hash);
    }

    private void SetOpen(OpenMode mode)
    {
        openMode = mode;
        if (weakPoints == null)
            return;

        string[] phaseIds = config != null && PhaseCount > 0 ? CurrentPhase.weakPointIds : null;
        foreach (BossWeakPoint point in weakPoints)
        {
            if (point == null) continue;
            bool open;
            switch (mode)
            {
                case OpenMode.Phase: open = phaseIds != null && System.Array.IndexOf(phaseIds, point.Id) >= 0; break;
                case OpenMode.Counter: open = point.Id == config.honkCounterWeakPointId; break;
                case OpenMode.All: open = true; break;
                default: open = false; break;
            }
            point.SetOpen(open);
        }
    }

    private AttackVisual VisualFor(BossAttack attack)
    {
        if (attackVisuals == null) return null;
        foreach (AttackVisual visual in attackVisuals)
            if (visual != null && visual.attack == attack) return visual;
        return null;
    }

    // Cambia los materiales una vez al entrar y salir del aviso (no en cada frame): primero se devuelve
    // todo a su material y luego, si hay aviso, se pinta de rojo lo del ataque actual
    private void ShowTelegraph(bool on)
    {
        foreach (KeyValuePair<Renderer, Material[]> entry in originalMaterials)
            if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;

        if (attackVisuals == null)
            return;
        foreach (AttackVisual visual in attackVisuals)
            if (visual != null && visual.light != null) visual.light.intensity = 0f;

        AttackVisual current = on ? VisualFor(CurrentAttack) : null;
        if (current == null)
            return;
        if (current.light != null) current.light.intensity = 0.2f * current.lightIntensity;
        if (current.renderers == null || telegraphMaterial == null)
            return;

        foreach (Renderer rend in current.renderers)
        {
            if (rend == null || !originalMaterials.TryGetValue(rend, out Material[] original)) continue;
            Material[] red = new Material[original.Length];
            for (int m = 0; m < red.Length; m++) red[m] = telegraphMaterial;
            rend.sharedMaterials = red;
        }
    }

    // Del 20 % al 100 % de la intensidad a lo largo del aviso
    private void SetTelegraphLight(float progress)
    {
        AttackVisual visual = VisualFor(CurrentAttack);
        if (visual != null && visual.light != null)
            visual.light.intensity = Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(progress)) * visual.lightIntensity;
    }

    // Piezas que salen despedidas con física: se desenganchan, reciben un impulso y se ocultan al rato
    private void DropParts(int phase)
    {
        if (partsDroppedAtPhase == null || phase < 0 || phase >= partsDroppedAtPhase.Length || partsDroppedAtPhase[phase] == null)
            return;
        Transform[] parts = partsDroppedAtPhase[phase].parts;
        if (parts == null) return;

        foreach (Transform part in parts)
        {
            if (part == null || !part.gameObject.activeSelf || part.parent == null) continue;

            part.SetParent(null, true);

            // Sus colliders pasan a trigger: el disparo y la onda ignoran los triggers, así que los trozos que
            // vuelan delante del boss no paran las balas (en la prueba, el sombrero y las hombreras tapaban la
            // mandíbula justo en el bocinazo de estreno de la fase). Sin colisión, caen tras la caseta
            foreach (Collider col in part.GetComponentsInChildren<Collider>()) col.isTrigger = true;

            Rigidbody body = part.GetComponent<Rigidbody>();
            if (body == null) body = part.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.mass = 2f;
            // Hacia arriba y hacia el jugador, un poco al azar: que se vea que el boss se está rompiendo
            Vector3 push = new Vector3(Random.Range(-0.5f, 0.5f), 1f, -0.6f).normalized * partImpulse;
            body.AddForce(push, ForceMode.VelocityChange);
            body.AddTorque(Random.insideUnitSphere * partImpulse, ForceMode.VelocityChange);
            StartCoroutine(HidePartLater(part.gameObject));
        }
    }

    private IEnumerator HidePartLater(GameObject part)
    {
        yield return new WaitForSeconds(partLifetime);
        if (part != null) part.SetActive(false);
    }

    // Partida nueva: las piezas sueltas vuelven a su sitio
    private void RestoreParts()
    {
        foreach (PartPose pose in partPoses)
        {
            if (pose.part == null) continue;
            Rigidbody body = pose.part.GetComponent<Rigidbody>();
            if (body != null) { body.isKinematic = true; Destroy(body); }
            foreach (Collider col in pose.part.GetComponentsInChildren<Collider>(true)) col.isTrigger = false;
            pose.part.SetParent(pose.parent, false);
            pose.part.localPosition = pose.position;
            pose.part.localRotation = pose.rotation;
            pose.part.gameObject.SetActive(true);
        }
    }
}
