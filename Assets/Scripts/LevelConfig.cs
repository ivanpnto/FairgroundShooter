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
    }

    [Header("Presentación")]
    public string levelName = "Nivel";
    [Tooltip("Id del conjunto de entorno que activa el LevelManager (decorado + iluminación)")]
    public string environmentId = "Pond";

    [Header("Duración")]
    [Min(1f)] public float duration = 60f;

    [Header("Aparición")]
    public SpawnEntry[] entries;
    [Min(0.1f)] public float spawnInterval = 1.3f;
    [Min(1)] public int maxActiveTargets = 5;

    [Header("Dificultad (multiplicadores sobre los valores del prefab)")]
    [Min(0f)] public float pointsMultiplier = 1f;      // también agrava las penalizaciones
    [Min(0.1f)] public float speedMultiplier = 1f;     // acelera todo el patrón de movimiento
    [Min(0.1f)] public float lifetimeMultiplier = 1f;  // < 1: los objetivos desaparecen antes

    [Header("Objetivos especiales (contorno de color)")]
    [Range(0f, 1f)] public float timeBonusChance = 0.05f;   // contorno dorado: tiempo extra
    [Range(0f, 1f)] public float ammoBonusChance = 0.05f;   // contorno rojo: cargador lleno
    [Min(0f)] public float timeBonusSeconds = 3f;
}
