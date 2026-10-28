using System.Collections.Generic;
using UnityEngine;

public class TargetSpawner : MonoBehaviour
{
    // Un tipo de objetivo que el spawner puede generar, con su propio "carril".
    [System.Serializable]
    private class SpawnEntry
    {
        public string label;                 // Solo informativo en el Inspector ("Pato", "Pez"...)
        public GameObject prefab;
        public Transform[] spawnPoints;      // Puntos propios de este tipo de objetivo
        [Min(0f)] public float weight = 1f;  // Probabilidad relativa frente a los demás tipos
        [Min(1)] public int maxActive = 1;   // Máximo de este tipo a la vez en pantalla

        [System.NonSerialized] public int active;
    }

    [SerializeField] private SpawnEntry[] entries;
    [SerializeField] private float spawnInterval = 1.5f;
    [SerializeField] private int maxActiveTargets = 4;   // Tope global, sumando todos los tipos

    private GameManager gameManager;
    private float spawnTimer;
    private int activeTargets;
    private readonly List<int> candidates = new List<int>();

    private void Awake()
    {
        gameManager = GetComponent<GameManager>();

        foreach (SpawnEntry entry in entries)
            entry.active = 0;
    }

    private void Update()
    {
        if (gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        spawnTimer -= Time.deltaTime;

        if (spawnTimer > 0f)
            return;

        spawnTimer = spawnInterval;

        if (activeTargets < maxActiveTargets)
            SpawnTarget();
    }

    private void SpawnTarget()
    {
        // 1. Candidatos: tipos bien configurados que no han llegado a su propio máximo
        candidates.Clear();
        float totalWeight = 0f;

        for (int i = 0; i < entries.Length; i++)
        {
            SpawnEntry entry = entries[i];

            if (entry.prefab == null || entry.spawnPoints == null || entry.spawnPoints.Length == 0)
                continue;

            if (entry.weight <= 0f || entry.active >= entry.maxActive)
                continue;

            candidates.Add(i);
            totalWeight += entry.weight;
        }

        if (candidates.Count == 0)
            return;

        // 2. Ruleta ponderada: cada tipo ocupa un trozo proporcional a su peso
        float roll = Random.value * totalWeight;
        int chosen = candidates[candidates.Count - 1];

        foreach (int i in candidates)
        {
            roll -= entries[i].weight;

            if (roll <= 0f)
            {
                chosen = i;
                break;
            }
        }

        // 3. Instanciar en uno de los puntos de ese tipo
        SpawnEntry selected = entries[chosen];
        Transform spawnPoint = selected.spawnPoints[Random.Range(0, selected.spawnPoints.Length)];

        GameObject targetObject = Instantiate(selected.prefab, spawnPoint.position, spawnPoint.rotation);
        Target target = targetObject.GetComponent<Target>();

        if (target == null)
        {
            // Sin Target nadie avisaría al destruirse y el hueco quedaría ocupado para siempre
            Debug.LogWarning($"TargetSpawner: el prefab '{selected.prefab.name}' no tiene componente Target.", this);
            Destroy(targetObject);
            return;
        }

        target.SetSpawner(this, chosen);
        selected.active++;
        activeTargets++;
    }

    public void TargetDestroyed(int entryIndex)
    {
        activeTargets = Mathf.Max(0, activeTargets - 1);

        if (entryIndex >= 0 && entryIndex < entries.Length)
            entries[entryIndex].active = Mathf.Max(0, entries[entryIndex].active - 1);
    }

    public void ResetSpawner()
    {
        Target[] targets = FindObjectsByType<Target>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (Target target in targets)
        {
            // Destroy es diferido (fin de frame): desvinculamos antes para que su
            // OnDestroy no descuente de los contadores que ponemos a 0 justo debajo
            target.SetSpawner(null, -1);
            Destroy(target.gameObject);
        }

        activeTargets = 0;

        foreach (SpawnEntry entry in entries)
            entry.active = 0;

        spawnTimer = 0f;
    }
}
