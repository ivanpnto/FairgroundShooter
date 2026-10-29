using UnityEngine;

// Un "carril" de aparición en la escena, identificado por un id de texto.
// Los LevelConfig (assets) no pueden referenciar objetos de la escena, así que
// apuntan a un carril por su id y el spawner lo busca al configurar el nivel.
public class SpawnLane : MonoBehaviour
{
    [SerializeField] private string laneId;
    [SerializeField] private Transform[] points;

    public string LaneId => laneId;
    public bool HasPoints => points != null && points.Length > 0;

    public Transform GetRandomPoint()
    {
        return HasPoints ? points[Random.Range(0, points.Length)] : transform;
    }

    private void OnDrawGizmos()
    {
        if (!HasPoints)
            return;

        Gizmos.color = Color.cyan;

        foreach (Transform point in points)
        {
            if (point != null)
                Gizmos.DrawWireSphere(point.position, 0.3f);
        }
    }
}
