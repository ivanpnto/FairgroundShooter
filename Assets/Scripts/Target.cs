using UnityEngine;

public class Target : MonoBehaviour
{
    [SerializeField] private int points = 10;
    [SerializeField] private float lifetime = 5f;

    private ScoreManager scoreManager;
    private TargetSpawner spawner;
    private int spawnEntryIndex = -1;   // Qué tipo de la lista del spawner es este objetivo

    private void Awake()
    {
        scoreManager = FindFirstObjectByType<ScoreManager>();
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    public void SetSpawner(TargetSpawner targetSpawner, int entryIndex)
    {
        spawner = targetSpawner;
        spawnEntryIndex = entryIndex;
    }

    public void Hit()
    {
        Debug.Log("¡Objetivo alcanzado!");

        scoreManager.AddScore(points);

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.TargetDestroyed(spawnEntryIndex);
        }
    }
}