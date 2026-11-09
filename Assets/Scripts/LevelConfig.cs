using UnityEngine;

// Datos de un nivel: qué entorno se ve, qué objetivos salen y con qué dificultad.
// Es un asset (no un componente) para poder crear niveles nuevos sin tocar la escena ni el código.
[CreateAssetMenu(fileName = "Level", menuName = "Fairground/Level Config")]
public class LevelConfig : ScriptableObject
{
    [System.Serializable]
    public class SpawnEntry
    {
        public string label;                  // Solo informativo ("Pato", "Medusa"...)
        public GameObject prefab;
        [Tooltip("Id del SpawnLane de la escena donde aparece este objetivo")]
        public string laneId;
        [Min(0f)] public float weight = 1f;   // Probabilidad relativa frente a los demás tipos
        [Min(1)] public int maxActive = 1;    // Máximo de este tipo a la vez

        [Tooltip("Cuántos aparecen a la vez en formación (bandada). 1 = uno suelto. Cada miembro es un objetivo " +
                 "independiente; maxActive cuenta miembros, así que debe ser al menos groupSize")]
        [Min(1)] public int groupSize = 1;
        [Tooltip("Separación entre miembros: X hacia fuera de la caseta (entran detrás del líder), Y alternando arriba/abajo (forma de V)")]
        public Vector3 groupSpacing = new Vector3(0.9f, 0.3f, 0f);
    }

    [Header("Presentación")]
    public string levelName = "Nivel";
    [Tooltip("Id del conjunto de entorno que activa el LevelManager (decorado + iluminación)")]
    public string environmentId = "Pond";

    [Tooltip("Música de fondo del nivel (la reproduce MusicManager en bucle). Vacío = silencio")]
    public AudioClip music;

    [Header("Boss (nivel 4)")]
    [Tooltip("Nivel con boss (§27). Solo se juega si el perfil lo ha comprado (UpgradeEffect.UnlockBossLevel). " +
             "Empieza como un nivel normal (dianas y cuenta atrás) y a los bossRevealDelay s llega la revelación; " +
             "desde ahí el temporizador es un cronómetro y el nivel solo acaba con victoria o derrota")]
    public bool isBossLevel;
    [Tooltip("Segundos de nivel «normal» antes de la revelación (solo cuenta el tiempo en Playing). " +
             "Con 0, la revelación empieza nada más abrirse el nivel")]
    [Min(0f)] public float bossRevealDelay = 12f;

    [Header("Duración")]
    [Min(1f)] public float duration = 60f;

    [Header("Aparición")]
    public SpawnEntry[] entries;
    [Min(0.1f)] public float spawnInterval = 1.3f;
    [Tooltip("Ritmo creciente: al final del nivel el intervalo vale spawnInterval × este factor " +
             "(se interpola linealmente con el tiempo jugado). 1 = ritmo constante")]
    [Range(0.2f, 1f)] public float endSpawnIntervalFactor = 0.6f;
    [Min(1)] public int maxActiveTargets = 5;

    [Header("Dificultad (multiplicadores sobre los valores del prefab)")]
    [Min(0f)] public float pointsMultiplier = 1f;      // también agrava las penalizaciones
    [Min(0.1f)] public float speedMultiplier = 1f;     // acelera todo el patrón de movimiento
    [Min(0.1f)] public float lifetimeMultiplier = 1f;  // < 1: los objetivos desaparecen antes
    [Tooltip("Vida final = vida del prefab × este valor, SIN redondear (1 × 1.5 = 1.5). Los de penalización no lo aplican")]
    [Min(0.1f)] public float healthMultiplier = 1f;

    [Header("Objetivos especiales (contorno de color)")]
    [Range(0f, 1f)] public float timeBonusChance = 0.05f;   // contorno dorado: tiempo extra
    [Range(0f, 1f)] public float ammoBonusChance = 0.05f;   // contorno rojo: cargador lleno
    [Min(0f)] public float timeBonusSeconds = 3f;
}
