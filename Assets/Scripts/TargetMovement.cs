using UnityEngine;

public class TargetMovement : MonoBehaviour
{
    [SerializeField] private float speed = 2f;
    [SerializeField] private float distance = 3f;

    private Vector3 startPosition;
    private float direction = 1f;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        transform.position += Vector3.right * speed * direction * Time.deltaTime;

        float distanceFromStart = transform.position.x - startPosition.x;

        if (Mathf.Abs(distanceFromStart) >= distance)
        {
            direction *= -1f;
        }
    }
}