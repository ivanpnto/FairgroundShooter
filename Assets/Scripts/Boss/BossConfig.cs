using UnityEngine;

// Ataques del boss. Se añaden AL FINAL (los assets guardan el enum como número)
public enum BossAttack
{
    ProjectileRain,   // I7.3: las manos lanzan una tanda de proyectiles en parábola (1 corazón cada uno)
    ChargedHonk       // I7.3: bocinazo. Aviso largo; si no se contrarresta disparando a la mandíbula, 2 corazones
    // Minions: I7.5
}

// Datos del boss (I7.2), igual que LevelConfig: las cifras se ajustan jugando sin tocar código ni escena.
// Todas son provisionales (§27).
[CreateAssetMenu(fileName = "BossConfig", menuName = "Fairground/Boss Config")]
public class BossConfig : ScriptableObject
{
    [System.Serializable]
    public class Phase
    {
        public string label = "Fase";
        [Tooltip("Vida de esta fase. El daño que sobra al acabarla se pierde: no se salta una fase de un disparo")]
        [Min(1f)] public float health = 20f;
        [Tooltip("Ids de los BossWeakPoint que se abren en esta fase (los demás quedan cerrados)")]
        public string[] weakPointIds = { "Eye" };
        [Tooltip("Ataques de la fase. El ÚLTIMO de la lista es el que se estrena en ella: es el primero que hace, " +
                 "para presentarlo mientras el jugador aún tiene margen")]
        public BossAttack[] attacks = { BossAttack.ProjectileRain };

        [Header("Ritmo del ciclo Idle → Telegraph → Attack → Vulnerable")]
        [Tooltip("Segundos en reposo (aleatorio entre x e y). Puntos débiles abiertos")]
        public Vector2 idleTime = new Vector2(1.5f, 2.5f);
        [Tooltip("Aviso antes de la lluvia de proyectiles: manos en rojo. Puntos débiles cerrados")]
        [Min(0.1f)] public float telegraphTime = 1f;
        [Tooltip("Ventana tras el ataque. Puntos débiles abiertos")]
        [Min(0f)] public float vulnerableTime = 2.5f;

        [Header("Lluvia de proyectiles")]
        [Min(1)] public int projectilesPerVolley = 3;
        [Tooltip("Máximo de proyectiles en el aire a la vez (§27.5: 3 en la fase 1, 5 en la 3)")]
        [Min(1)] public int maxProjectilesAlive = 3;
        [Tooltip("Segundos entre lanzamientos de una misma tanda")]
        [Min(0f)] public float projectileInterval = 0.45f;
        [Tooltip("Tiempo de vuelo hasta la cámara (aleatorio entre x e y). Es el aviso real del proyectil")]
        public Vector2 projectileFlightTime = new Vector2(1.6f, 2.2f);

        [Header("Presión de fondo")]
        [Tooltip("Cada cuántos segundos suelta un proyectil suelto mientras está en reposo o vulnerable (0 = nunca). " +
                 "Obliga a elegir entre derribarlo y castigar al boss. Nunca durante el aviso del bocinazo (§27.6, regla 5)")]
        [Min(0f)] public float backgroundProjectileInterval = 0f;

        [Tooltip("Cada cuántos segundos sale un esbirro (0 = ninguno en esta fase). Solo cuenta el tiempo de combate")]
        [Min(0f)] public float minionInterval = 0f;
        [Tooltip("Esbirros a la vez como máximo")]
        [Min(0)] public int maxMinions = 0;
    }

    public Phase[] phases = { new Phase() };

    [Header("Cambio de fase")]
    [Tooltip("Segundos en que el boss no recibe daño ni ataca al cambiar de fase. El jugador tampoco recibe daño " +
             "y los proyectiles en el aire desaparecen: es una pausa para respirar y ver el cambio")]
    [Min(0f)] public float phaseChangeDuration = 2f;

    [Header("Proyectiles")]
    [Tooltip("Prefab con Target + Rigidbody + BossProjectile")]
    public GameObject projectilePrefab;
    [Min(1)] public int projectileHeartDamage = 1;
    [Tooltip("Altura del arco: cuánto sube el proyectil, a mitad de camino, por encima de la recta de la mano a la " +
             "cámara (m). De ella sale su gravedad. Con la gravedad real y 2 s de vuelo subiría ~4,9 m y pasaría " +
             "por encima del techo de la caseta, donde no se ve ni se le puede dar")]
    [Min(0f)] public float projectileArcHeight = 1f;
    [Tooltip("Dispersión del punto de llegada alrededor del centro de la zona de daño (m). Pequeña: tienen que llegar")]
    [Min(0f)] public float projectileAimJitter = 0.35f;

    [Header("Bocinazo (ataque cargado)")]
    [Tooltip("Toma de aire antes del aviso: cabeza roja y luz subiendo, pero la boca aún cerrada. Da tiempo a " +
             "recargar si se ve venir: con 1,2 s, una recarga de la Five-seven (1,5 s) empezada al ver la cabeza roja " +
             "acaba 0,3 s dentro de la ventana. Con 0,6 s acababa a 0,9 s y el golpe era casi seguro (§27.6, regla 8)")]
    [Min(0f)] public float honkWindupTime = 1.2f;
    [Tooltip("Aviso: la boca se abre y se pone roja, la luz sube. El más largo porque es el que más quita (§27.6, regla 3)")]
    [Min(0.5f)] public float honkTelegraphTime = 1.5f;
    [Tooltip("En furia el aviso se acorta, pero nunca por debajo de esto")]
    [Min(0.5f)] public float honkMinTelegraphTime = 1.2f;
    [Tooltip("Daño a la mandíbula durante el aviso que lo cancela y deja al boss aturdido")]
    [Min(0.1f)] public float honkCounterDamage = 4f;
    [Tooltip("Id del punto débil que contrarresta el bocinazo. Se abre durante su aviso en cualquier fase")]
    public string honkCounterWeakPointId = "Jaw";
    [Min(1)] public int honkHeartDamage = 2;
    [Tooltip("Segundos con la boca abierta tras soltarlo, antes de la ventana vulnerable")]
    [Min(0f)] public float honkRecoveryTime = 0.6f;

    [Header("Aturdido (bocinazo contrarrestado)")]
    [Min(0f)] public float stunDuration = 3f;
    [Tooltip("Daño extra mientras está aturdido. Se abren TODOS los puntos débiles")]
    [Min(1f)] public float stunDamageMultiplier = 1.5f;

    [Header("Furia")]
    [Tooltip("Segundos de combate (solo cuentan los de juego) tras los que se enfurece. 0 = nunca")]
    [Min(0f)] public float furyTime = 180f;
    [Tooltip("Multiplica los avisos, los reposos y los intervalos en furia")]
    [Range(0.3f, 1f)] public float furyTimeMultiplier = 0.75f;

    [Header("Puntos")]
    [Tooltip("Puntos por cada unidad de daño a un punto débil (× multiplicador del nivel, mejora y racha)")]
    [Min(0f)] public float weakPointPointsPerDamage = 10f;

    [Header("Victoria")]
    [Tooltip("Segundos entre la muerte del boss y el fin de la partida (para la animación)")]
    [Min(0f)] public float deathDelay = 2f;
    [Tooltip("Bonus al vencer (§27.7): (base + porCorazón × corazones + porSegundo × máx(0, tiempoPar − tiempo)) × multiplicador del nivel. Se suma sin racha ni mejora")]
    [Min(0)] public int victoryBonusBase = 2000;
    [Min(0)] public int victoryBonusPerHeart = 500;
    [Min(0)] public int victoryBonusPerSecond = 20;
    [Min(0f)] public float victoryParTime = 180f;

    [Header("Esbirros (I7.5)")]
    [Tooltip("Prefab con Target + MinionLunge (el muñeco de resorte)")]
    public GameObject minionPrefab;
    [Tooltip("Carril de la escena donde salen (SpawnLane)")]
    public string minionLaneId = "Boss_Minion";
    [Tooltip("Segundos a la vista antes de intentar saltar (× furia)")]
    public Vector2 minionWaitTime = new Vector2(2.2f, 3.2f);
    [Tooltip("Aviso antes del salto: tiembla y el contorno se pone rojo (§27.6, regla 1)")]
    [Min(0.1f)] public float minionWarningTime = 0.6f;
    [Min(0.1f)] public float minionLungeTime = 0.9f;
    [Min(1)] public int minionHeartDamage = 1;
    [Tooltip("Material del contorno durante el aviso (el rojo de peligro)")]
    public Material minionWarningMaterial;
    [Tooltip("Probabilidad de que un esbirro sea especial rosa (+1 corazón)")]
    [Range(0f, 1f)] public float minionHeartChance = 0.15f;
    public Material heartBonusOutline;

    public float TotalHealth
    {
        get
        {
            float total = 0f;
            if (phases != null)
                foreach (Phase phase in phases) total += phase.health;
            return total;
        }
    }
}
