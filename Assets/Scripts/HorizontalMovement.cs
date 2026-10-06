using Unity.VisualScripting;
using UnityEngine;

public class HorizontalMovement : TargetMovement
{
    [SerializeField] private float speed = 2f;
    [SerializeField] private float distance = 3f;

    private float direction = 1f;

    protected override void Move()
    {
        transform.position += Vector3.right * speed * direction * Time.deltaTime;

        float distanceFromStart = transform.position.x - startPosition.x;

        if (Mathf.Abs(distanceFromStart) >= distance)
        {
            direction *= -1f;
        }
    }
}