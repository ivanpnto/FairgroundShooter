using UnityEngine;
using UnityEngine.InputSystem;

// Resultado de UN disparo, ya resuelto. Lo consumen la racha (Fase 5), el hitmarker y los
// efectos de impacto. Es un struct de solo lectura: se copia al pasarlo y nadie puede alterarlo.
//
// Desde I6.2 un disparo puede tener varios rayos (disparo doble) y cada rayo varios objetivos
// (penetración). ShotResult sigue siendo UNO por disparo: es lo que mantiene la regla de la racha
// ("un disparo = un acierto o un fallo"). Los campos de siempre describen el impacto PRINCIPAL
// (el primer objetivo alcanzado o, si no hubo ninguno, donde paró el primer rayo); los nuevos
// resumen todo el disparo. Cada impacto suelto se publica aparte en PlayerShoot.ImpactResolved.
public readonly struct ShotResult
{
    public readonly bool HitSomething;   // algún rayo chocó con algún collider (dentro del alcance)
    public readonly bool HitTarget;      // algún rayo dio DIRECTAMENTE a un objetivo vivo. false = FALLO
    public readonly bool HitScenery;     // el impacto principal es de la capa Scenery
    public readonly Vector3 Point;       // punto del impacto principal (o el final del alcance)
    public readonly Vector3 Normal;
    public readonly Collider Collider;   // collider del impacto principal (null si no chocó)
    public readonly Target Target;       // primer objetivo alcanzado (null si ninguno)
    public readonly float Damage;        // daño directo total aplicado a objetivos en este disparo
    public readonly bool KilledTarget;   // algún impacto directo derribó a su objetivo

    // I6.2
    public readonly int TargetsHit;      // objetivos distintos alcanzados de forma directa (no por explosión)
    public readonly bool HitPenalty;     // alguno de ellos era de penalización (rompe la racha)
    public readonly int ExplosionKills;  // objetivos derribados solo por la onda expansiva

    // I7.1: algún impacto directo dio a un punto débil del boss (un IDamageable que no es Target).
    // Cuenta como acierto para la racha y el hitmarker aunque Target sea null
    public readonly bool HitWeakPoint;

    public ShotResult(bool hitSomething, bool hitScenery, Vector3 point, Vector3 normal, Collider collider, Target target,
        float damage = 0f, bool killedTarget = false, int targetsHit = -1, bool hitPenalty = false, int explosionKills = 0,
        bool hitWeakPoint = false)
    {
        HitSomething = hitSomething;
        HitWeakPoint = hitWeakPoint;
        // HitTarget es lo que leen la racha, el hitmarker y el sonido de acierto: un punto débil es un acierto
        HitTarget = target != null || hitWeakPoint;
        HitScenery = hitScenery;
        Point = point;
        Normal = normal;
        Collider = collider;
        Target = target;
        Damage = HitTarget ? damage : 0f;
        KilledTarget = killedTarget;
        // -1 = constructor antiguo (un solo objetivo): se deduce del objetivo principal
        TargetsHit = targetsHit >= 0 ? targetsHit : (HitTarget ? 1 : 0);
        HitPenalty = hitPenalty || (target != null && target.IsPenalty);
        ExplosionKills = explosionKills;
    }

    // Acierto para la racha y la precisión: dio a algún objetivo normal y a ninguno de penalización
    public bool IsCleanHit => HitTarget && !HitPenalty;
}

// Un impacto concreto de un rayo (objetivo o superficie donde se detuvo). Para efectos por impacto
public readonly struct ShotImpact
{
    public readonly Vector3 Point;
    public readonly Vector3 Normal;
    public readonly Collider Collider;
    public readonly Target Target;       // null si es una superficie
    public readonly bool HitScenery;
    public readonly bool Killed;         // el impacto derribó al objetivo
    public readonly bool HitWeakPoint;   // I7.1: dio a un punto débil del boss (Target es null)

    // Acierto en este impacto: un objetivo o un punto débil. Para el confeti de WeaponEffects
    public bool IsHit => Target != null || HitWeakPoint;

    public ShotImpact(Vector3 point, Vector3 normal, Collider collider, Target target, bool hitScenery, bool killed,
        bool hitWeakPoint = false)
    {
        Point = point; Normal = normal; Collider = collider; Target = target; HitScenery = hitScenery; Killed = killed;
        HitWeakPoint = hitWeakPoint;
    }
}

public class PlayerShoot : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private WeaponAmmo weaponAmmo;

    // NUEVO: capas que puede golpear el disparo (quita la del jugador/arma si la tienen)
    [SerializeField] private LayerMask hitMask = ~0; // ~0 = todas las capas

    private GameManager gameManager;

    // El alcance sale del arma equipada; ya no hay campo maxDistance propio para
    // que no existan dos valores que puedan contradecirse
    private WeaponController weaponController;
    private PlayerAim playerAim;

    // Momento (en Time.time) a partir del cual se permite el siguiente disparo
    private float nextFireTime;

    // Avisa de cada impacto del raycast para los efectos (partículas, sonido), igual que
    // WeaponAmmo.OnShot avisa del disparo. hitTarget = el collider tenía un Target
    public event System.Action<RaycastHit, bool> OnHit;

    // Se lanza en TODOS los disparos, también en los que no chocan con nada (OnHit no lo hace).
    // Es el "evento de disparo resuelto" del contrato de la Fase 5: acierto / fallo + punto de impacto
    public event System.Action<ShotResult> ShotResolved;

    [Tooltip("Capas que cuentan como decorado (caseta, escenario, suelo). Solo sirve para clasificar el impacto")]
    [SerializeField] private LayerMask sceneryMask = 1 << 8;   // capa Scenery

    // ---- Mejoras caras (I6.2). Los valores de cada mejora salen de WeaponStats; aquí solo la forma
    [Tooltip("Separación angular total entre los rayos del disparo doble (grados). 3,5° ≈ 25 cm a cada lado a 8 m: cubre más área en vez de duplicar el daño casi siempre")]
    [SerializeField, Min(0f)] private float multiRaySpread = 3.5f;

    // Un evento por impacto: con penetración y disparo doble hay varios por disparo. Para efectos
    // (chispas, confeti, polvo) en cada punto. ShotResolved sigue siendo uno por disparo
    public event System.Action<ShotImpact> ImpactResolved;
    // Un evento por rayo (origen, final): para trazas del disparo doble (J6.2)
    public event System.Action<Vector3, Vector3> RayTraced;
    // Una onda expansiva (centro, radio): para el efecto de explosión (J6.2)
    public event System.Action<Vector3, float> Exploded;

    // Listas reutilizadas entre disparos: sin basura para el recolector en cada clic
    // De IDamageable (I7.1): un mismo disparo puede dar a dianas y a puntos débiles
    private readonly System.Collections.Generic.List<IDamageable> shotTargets = new System.Collections.Generic.List<IDamageable>();
    private readonly System.Collections.Generic.List<IDamageable> rayTargets = new System.Collections.Generic.List<IDamageable>();
    private readonly System.Collections.Generic.List<IDamageable> blastTargets = new System.Collections.Generic.List<IDamageable>();
    private RaycastHit[] rayHits = new RaycastHit[16];
    private readonly Collider[] blastHits = new Collider[24];

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();

        // Todos van en Player: GetComponent evita más campos que asignar en la escena
        weaponController = GetComponent<WeaponController>();
        playerAim = GetComponent<PlayerAim>();
    }

    // Suscribirse en OnEnable y desuscribirse en OnDisable evita llamadas
    // a un objeto desactivado o destruido
    private void OnEnable()
    {
        if (weaponController != null) weaponController.OnWeaponEquipped += HandleWeaponEquipped;
    }

    private void OnDisable()
    {
        if (weaponController != null) weaponController.OnWeaponEquipped -= HandleWeaponEquipped;
    }

    // El arma recién equipada puede disparar al momento: no hereda la espera de la anterior
    private void HandleWeaponEquipped(WeaponData data) => nextFireTime = Time.time;

    private void Update()
    {
        if (gameManager == null)
            return;

        if (gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        // Sin arma equipada no hay alcance con el que disparar; se comprueba antes de
        // UseAmmo() para no gastar una bala en un disparo que no ocurre
        if (weaponController == null || weaponController.CurrentWeapon == null)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Antes de leer CurrentWeapon: si se cambia de arma este frame, el disparo
        // de este mismo frame ya usa los datos de la nueva
        HandleDebugWeaponKeys();
#endif

        WeaponData weapon = weaponController.CurrentWeapon;
        WeaponStats stats = weaponController.CurrentStats;   // cadencia (con mejoras); el modo sale de WeaponData

        // NUEVO: Mouse/Keyboard.current pueden ser null si no hay dispositivo conectado
        if (Mouse.current != null)
        {
            // Automática: dispara mientras se mantiene el botón. Semiautomática: un disparo por clic
            bool wantsToShoot = weapon.isAutomatic
                ? Mouse.current.leftButton.isPressed
                : Mouse.current.leftButton.wasPressedThisFrame;

            // El tiempo se comprueba antes que la munición para no gastar una bala si aún no toca.
            // CAMBIO: UseAmmo() comprueba y gasta la bala; solo disparamos si ha tenido éxito
            if (wantsToShoot && Time.time >= nextFireTime && weaponAmmo.UseAmmo())
            {
                // Cadencia: el siguiente disparo se programa sumando el intervalo al momento
                // en que TOCABA disparar, no al momento actual. El disparo siempre ocurre algo
                // tarde (al llegar el frame), y con "Time.time + intervalo" ese retraso se
                // perdería en cada disparo y la cadencia real quedaría por debajo de fireRate.
                // Pero si nextFireTime se ha quedado atrás más de un intervalo (el jugador
                // llevaba un rato sin disparar), sumar sin más dejaría "disparos acumulados"
                // que saldrían seguidos al volver a pulsar; por eso antes se trae al presente.
                if (Time.time - nextFireTime > stats.TimeBetweenShots)
                    nextFireTime = Time.time;

                nextFireTime += stats.TimeBetweenShots;

                Shoot();

                // Después de Shoot(): el retroceso desvía los disparos siguientes, no este.
                // La vista no se recupera hasta 1.5 intervalos sin disparar: en ráfaga continua
                // (o clicando a la cadencia máxima) el retroceso se acumula y el arma "trepa".
                // Con tope de 0.25 s: en armas lentas (Barrett, 1.25 s entre disparos) cada
                // disparo debe ser un golpe que se asienta antes del siguiente, no acumularse
                if (playerAim != null)
                    playerAim.AddRecoil(weapon.cameraKick, weapon.cameraKickSide,
                        weapon.cameraRecoverySpeed, Mathf.Min(stats.TimeBetweenShots * 1.5f, 0.25f));
            }
        }

        // NUEVO: recarga manual
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            weaponAmmo.TryReload();
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Atajo de depuración: 1–5 equipan las armas de AvailableWeapons en plena partida.
    // Solo existe en el editor y en builds de desarrollo; en una build normal no se
    // compila, así que no sirve para hacer trampas (Equip rellena el cargador)
    private void HandleDebugWeaponKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        var weapons = weaponController.AvailableWeapons;
        if (weapons == null)
            return;

        for (int i = 0; i < weapons.Count && i < 5; i++)
        {
            // Fila superior y teclado numérico: Key.Digit1..5 y Key.Numpad1..5 son consecutivas
            if (!keyboard[Key.Digit1 + i].wasPressedThisFrame && !keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                continue;

            // La misma arma no se reequipa: rellenaría el cargador gratis. Las bloqueadas en el perfil,
            // tampoco: el atajo es para probar, no para saltarse la tienda (para eso está el menú de depuración)
            if (weapons[i] != null && weapons[i] != weaponController.CurrentWeapon && weaponController.IsUnlocked(weapons[i]))
            {
                weaponController.Equip(weapons[i]);
                Debug.Log($"Depuración: equipada {weapons[i].displayName} (tecla {i + 1})");
            }
            return;
        }
    }
#endif

    // Pipeline del disparo (I6.2), con las estadísticas efectivas del arma:
    //   1. Rays rayos (1 normal, 2 con disparo doble), repartidos en horizontal alrededor del centro.
    //   2. Cada rayo recorre sus impactos ORDENADOS por distancia: daña objetivos vivos hasta
    //      Penetration objetivos y se detiene en el primer decorado (la bala no atraviesa paredes).
    //   3. Con ExplosionRadius > 0, cada impacto directo produce una onda expansiva.
    //   4. Al final, UN ShotResolved con el resumen (racha, hitmarker) y un ImpactResolved por impacto.
    // Sin mejoras (Rays 1, Penetration 1, radio 0) el resultado es idéntico al raycast de antes.
    private void Shoot()
    {
        WeaponStats stats = weaponController.CurrentStats;
        float range = stats.Range;
        int rays = Mathf.Max(1, stats.Rays);
        int penetration = Mathf.Max(1, stats.Penetration);

        shotTargets.Clear();
        bool hitPenalty = false, killedAny = false, hitSomething = false, hitWeakPoint = false;
        float totalDamage = 0f;
        int explosionKills = 0;

        // Impacto principal: el primer objetivo alcanzado o, si no hay, donde paró el primer rayo
        bool hasPrimary = false, primaryIsTarget = false;
        ShotImpact primary = default;
        Ray firstRay = default;

        for (int r = 0; r < rays; r++)
        {
            Ray ray = RayFor(r, rays);
            if (r == 0) firstRay = ray;

            rayTargets.Clear();
            Vector3 end = ray.GetPoint(range);

            int count = RaycastSorted(ray, range);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = rayHits[i];
                // I7.1: se busca IDamageable y no Target. Target sigue siendo el caso habitual
                IDamageable damageable = hit.collider.GetComponent<IDamageable>();
                Target target = damageable as Target;
                bool weakPoint = damageable != null && target == null;   // hoy, el único otro IDamageable

                // Ya no se le puede dar (derribado, expirando, cerrado), o ya lo alcanzó este rayo
                // (varios colliders): no cuenta y el rayo sigue
                if (damageable != null && (!damageable.CanBeHit || rayTargets.Contains(damageable)))
                    continue;

                hitSomething = true;
                bool scenery = (sceneryMask.value & (1 << hit.collider.gameObject.layer)) != 0;

                if (damageable != null)
                {
                    rayTargets.Add(damageable);
                    if (!shotTargets.Contains(damageable)) shotTargets.Add(damageable);
                    if (target != null && target.IsPenalty) hitPenalty = true;
                    if (weakPoint) hitWeakPoint = true;

                    // Daño ANTES de publicar nada: los puntos se suman antes que cualquier efecto
                    damageable.TakeDamage(stats.Damage);
                    totalDamage += stats.Damage;
                    bool killed = target != null && target.IsDead;
                    killedAny |= killed;

                    ShotImpact impact = new ShotImpact(hit.point, hit.normal, hit.collider, target, false, killed, weakPoint);
                    Publish(hit, impact);
                    if (!primaryIsTarget) { primary = impact; hasPrimary = true; primaryIsTarget = true; }

                    if (stats.ExplosionRadius > 0f)
                        explosionKills += Explode(hit.point, stats.ExplosionRadius, stats.Damage, damageable);

                    // Un punto débil para el rayo: la penetración no atraviesa al boss (§27.4). No se confía
                    // en que detrás haya un collider de decorado bien colocado
                    if (weakPoint || rayTargets.Count >= penetration)
                    {
                        end = hit.point;
                        break;
                    }
                }
                else
                {
                    // Decorado (o cualquier otra cosa sólida): el rayo se detiene aquí
                    ShotImpact impact = new ShotImpact(hit.point, hit.normal, hit.collider, null, scenery, false);
                    Publish(hit, impact);
                    if (!hasPrimary) { primary = impact; hasPrimary = true; }

                    // La bala explosiva también estalla contra el decorado: puede derribar lo que esté al lado
                    if (stats.ExplosionRadius > 0f)
                        explosionKills += Explode(hit.point, stats.ExplosionRadius, stats.Damage, null);

                    end = hit.point;
                    break;
                }
            }

            RayTraced?.Invoke(ray.origin, end);
        }

        ShotResult result = hasPrimary
            ? new ShotResult(true, primary.HitScenery, primary.Point, primary.Normal, primary.Collider,
                primaryIsTarget ? primary.Target : null, totalDamage, killedAny, shotTargets.Count, hitPenalty, explosionKills,
                hitWeakPoint)
            : new ShotResult(false, false, firstRay.GetPoint(range), -firstRay.direction, null, null,
                0f, false, 0, false, explosionKills);

        ShotResolved?.Invoke(result);
    }

    // Rayo i de n: el central si n = 1; si no, repartidos en horizontal entre −spread/2 y +spread/2
    private Ray RayFor(int index, int count)
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (count <= 1)
            return ray;

        float angle = Mathf.Lerp(-multiRaySpread * 0.5f, multiRaySpread * 0.5f, index / (float)(count - 1));
        ray.direction = Quaternion.AngleAxis(angle, playerCamera.transform.up) * ray.direction;
        return ray;
    }

    // RaycastAll no garantiza el orden: se ordena por distancia para recorrer los impactos como los
    // encontraría la bala. NonAlloc con un buffer que crece si se llena
    private int RaycastSorted(Ray ray, float range)
    {
        int count = Physics.RaycastNonAlloc(ray, rayHits, range, hitMask, QueryTriggerInteraction.Ignore);
        if (count == rayHits.Length)
        {
            rayHits = new RaycastHit[rayHits.Length * 2];
            count = Physics.RaycastNonAlloc(ray, rayHits, range, hitMask, QueryTriggerInteraction.Ignore);
        }
        System.Array.Sort(rayHits, 0, count, HitDistanceComparer.Instance);
        return count;
    }

    private sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    // Onda expansiva: daño = daño del arma × (1 − d / radio), con d la distancia del centro al punto
    // más cercano de cada objetivo. El objetivo del impacto directo queda fuera (ya recibió el daño
    // completo; si no, la bala explosiva haría casi el doble). Devuelve los derribados por la onda.
    // No cuenta para la racha ni para el hitmarker: solo el impacto directo decide acierto o fallo
    // I7.1: también daña puntos débiles (IDamageable), con el mismo decaimiento; solo los Target cuentan como derribos
    private int Explode(Vector3 center, float radius, float damage, IDamageable exclude)
    {
        Exploded?.Invoke(center, radius);

        int kills = 0;
        int count = Physics.OverlapSphereNonAlloc(center, radius, blastHits, hitMask, QueryTriggerInteraction.Ignore);
        blastTargets.Clear();   // para no dañar dos veces al mismo objetivo (varios colliders) en esta onda
        for (int i = 0; i < count; i++)
        {
            IDamageable target = blastHits[i].GetComponent<IDamageable>();
            if (target == null || target == exclude || !target.CanBeHit || blastTargets.Contains(target))
                continue;
            blastTargets.Add(target);

            float distance = Vector3.Distance(center, blastHits[i].ClosestPoint(center));
            float falloff = Mathf.Clamp01(1f - distance / radius);
            if (falloff <= 0f)
                continue;

            target.TakeDamage(damage * falloff);
            if (target is Target t && t.IsDead) kills++;
        }
        return kills;
    }

    private void Publish(RaycastHit hit, ShotImpact impact)
    {
        OnHit?.Invoke(hit, impact.IsHit);
        ImpactResolved?.Invoke(impact);
    }
}
