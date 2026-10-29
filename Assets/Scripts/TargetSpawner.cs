using System.Collections.Generic;
using UnityEngine;

public class TargetSpawner : MonoBehaviour
{
    [Header("Contorno de los objetivos especiales")]
    [SerializeField] private Material timeBonusOutline;   // dorado: tiempo extra
    [SerializeField] private Material ammoBonusOutline;   // rojo: cargador lleno

    private readonly Dictionary<string, SpawnLane> lanes = new Dictionary<string, SpawnLane>();
    private readonly List<int> candidates = new List<int>();

    private LevelConfig level;
    private int[] activePerEntry = new int[0];
    private int activeTargets;
    private float spawnTimer;

    private GameManager gameManager;

    private void Awake()
    {
        gameManager = GetComponent<GameManager>();
        CacheLanes();
    }

    // Los carriles son fijos en la escena: se buscan una vez y se indexan por id
    private void CacheLanes()
    {
        lanes.Clear();

        SpawnLane[] found = FindObjectsByType<SpawnLane>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (SpawnLane lane in found)
        {
            if (string.IsNullOrEmpty(lane.LaneId))
                continue;

            if (lanes.ContainsKey(lane.LaneId))
            {
                Debug.LogWarning($"TargetSpawner: hay dos carriles con el id '{lane.LaneId}'.", lane);
                continue;
            }

            lanes.Add(lane.LaneId, lane);
        }
    }

    // Lo llama LevelManager al empezar cada nivel
    public void Configure(LevelConfig config)
    {
        ResetSpawner();

        level = config;
        activePerEntry = new int[config != null && config.entries != null ? config.entries.Length : 0];

        if (config == null || config.entries == null)
            return;

        foreach (LevelConfig.SpawnEntry entry in config.entries)
        {
            if (!lanes.ContainsKey(entry.laneId))
                Debug.LogWarning($"TargetSpawner: el nivel '{config.levelName}' usa el carril '{entry.laneId}', que no existe en la escena.", this);
        }
    }

    private void Update()
    {
        if (level == null || gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        spawnTimer -= Time.deltaTime;

        if (spawnTimer > 0f)
            return;

        spawnTimer = level.spawnInterval;

        if (activeTargets < level.maxActiveTargets)
            SpawnTarget();
    }

    private void SpawnTarget()
    {
        // 1. Candidatos: tipos bien configurados, con carril válido y por debajo de su máximo
        candidates.Clear();
        float totalWeight = 0f;

        for (int i = 0; i < level.entries.Length; i++)
        {
            LevelConfig.SpawnEntry entry = level.entries[i];

            if (entry.prefab == null || entry.weight <= 0f || activePerEntry[i] >= entry.maxActive)
                continue;

            if (!lanes.TryGetValue(entry.laneId, out SpawnLane lane) || !lane.HasPoints)
                continue;

            candidates.Add(i);
            totalWeight += entry.weight;
        }

        if (candidates.Count == 0)
            return;

        // 2. Ruleta ponderada
        float roll = Random.value * totalWeight;
        int chosen = candidates[candidates.Count - 1];

        foreach (int i in candidates)
        {
            roll -= level.entries[i].weight;

            if (roll <= 0f)
            {
                chosen = i;
                break;
            }
        }

        // 3. Instanciar en un punto de su carril
        LevelConfig.SpawnEntry selected = level.entries[chosen];
        Transform spawnPoint = lanes[selected.laneId].GetRandomPoint();

        GameObject targetObject = Instantiate(selected.prefab, spawnPoint.position, spawnPoint.rotation);
        Target target = targetObject.GetComponent<Target>();

        if (target == null)
        {
            Debug.LogWarning($"TargetSpawner: el prefab '{selected.prefab.name}' no tiene componente Target.", this);
            Destroy(targetObject);
            return;
        }

        // 4. Dificultad del nivel. Se aplica antes de su Start(), que es donde arranca el lifetime
        target.SetSpawner(this, chosen);
        target.ApplyLevel(level.pointsMultiplier, level.lifetimeMultiplier);

        TargetMovement movement = targetObject.GetComponent<TargetMovement>();
        if (movement != null)
            movement.SetSpeedMultiplier(level.speedMultiplier);

        // 5. ¿Objetivo especial?
        TargetBonus bonus = RollBonus(target);
        if (bonus == TargetBonus.Time)
            target.ApplyBonus(bonus, timeBonusOutline, level.timeBonusSeconds);
        else if (bonus == TargetBonus.Ammo)
            target.ApplyBonus(bonus, ammoBonusOutline, 0f);

        activePerEntry[chosen]++;
        activeTargets++;
    }

    private TargetBonus RollBonus(Target target)
    {
        if (!target.CanHaveBonus)
            return TargetBonus.None;

        float roll = Random.value;

        if (roll < level.timeBonusChance)
            return TargetBonus.Time;

        if (roll < level.timeBonusChance + level.ammoBonusChance)
            return TargetBonus.Ammo;

        return TargetBonus.None;
    }

    public void TargetDestroyed(int entryIndex)
    {
        activeTargets = Mathf.Max(0, activeTargets - 1);

        if (entryIndex >= 0 && entryIndex < activePerEntry.Length)
            activePerEntry[entryIndex] = Mathf.Max(0, activePerEntry[entryIndex] - 1);
    }

    public void ResetSpawner()
    {
        Target[] targets = FindObjectsByType<Target>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Target target in targets)
        {
            // Destroy es diferido: desvinculamos antes para que su OnDestroy
            // no descuente de los contadores que ponemos a 0 justo debajo
            target.SetSpawner(null, -1);
            Destroy(target.gameObject);
        }

        activeTargets = 0;

        for (int i = 0; i < activePerEntry.Length; i++)
            activePerEntry[i] = 0;

        spawnTimer = 0f;
    }
}
