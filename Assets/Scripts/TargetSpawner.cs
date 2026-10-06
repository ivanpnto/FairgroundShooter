using UnityEngine;

public class TargetSpawner : MonoBehaviour
{
    [SerializeField] private GameObject targetPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private int maxActiveTargets = 3;

    private GameManager gameManager;
    private float spawnTimer;
    private int activeTargets;

    private void Awake()
    {
        gameManager = GetComponent<GameManager>();
    }

    private void Update()
    {
        if (gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        spawnTimer -= Time.deltaTime;

        if (spawnTimer <= 0f)
        {
            if (activeTargets < maxActiveTargets)
            {
                SpawnTarget();
            }

            spawnTimer = spawnInterval;
        }
    }

    private void SpawnTarget()
    {
        if (targetPrefab == null || spawnPoints.Length == 0)
            return;

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        GameObject targetObject = Instantiate(
            targetPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        Target target = targetObject.GetComponent<Target>();

        if (target != null)
        {
            target.SetSpawner(this);
        }

        activeTargets++;
    }

    public void TargetDestroyed()
    {
        activeTargets = Mathf.Max(0, activeTargets - 1);
    }
}