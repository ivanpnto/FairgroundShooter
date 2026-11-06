using UnityEngine;

// Ataques del boss. Se añaden AL FINAL (los assets guardan el enum como número)
public enum BossAttack
{
    ProjectileRain   // I7.3 (prueba en bruto): las manos lanzan proyectiles en parábola
    // ChargedHonk, Minions: pasos siguientes
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
        public BossAttack[] attacks = { BossAttack.ProjectileRain };

        [Header("Ritmo del ciclo Idle → Telegraph → Attack → Vulnerable")]
        [Tooltip("Segundos en reposo (aleatorio entre x e y). Puntos débiles abiertos")]
        public Vector2 idleTime = new Vector2(1.5f, 2.5f);
        [Tooltip("Aviso antes de atacar: rojo. Puntos débiles cerrados")]
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
    }

    public Phase[] phases = { new Phase() };

    [Header("Cambio de fase")]
    [Tooltip("Segundos en que el boss no recibe daño ni ataca al cambiar de fase")]
    [Min(0f)] public float phaseChangeDuration = 2f;

    [Header("Proyectiles")]
    [Tooltip("Prefab con Target + Rigidbody + BossProjectile")]
    public GameObject projectilePrefab;
    [Min(1)] public int projectileHeartDamage = 1;
    [Tooltip("Dispersión del punto de llegada alrededor del centro de la zona de daño (m). Pequeña: tienen que llegar")]
    [Min(0f)] public float projectileAimJitter = 0.35f;

    [Header("Victoria")]
    [Tooltip("Segundos entre la muerte del boss y el fin de la partida (para la animación)")]
    [Min(0f)] public float deathDelay = 1.5f;

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
