using UnityEngine;

public class HorizontalMovement : TargetMovement
{
    [SerializeField] private float speed = 2f;
    [SerializeField] private float distance = 3f;
    [SerializeField] private bool randomStartDirection = true;

    private float direction = 1f;

    protected override void Start()
    {
        base.Start();

        if (randomStartDirection && Random.value < 0.5f)
            direction = -1f;

        FaceDirection(direction);
    }

    protected override void Move()
    {
        transform.position += Vector3.right * speed * direction * DeltaTime;

        float distanceFromStart = transform.position.x - startPosition.x;

        // Solo giramos si estamos fuera del límite Y seguimos alejándonos: si en un frame
        // nos pasamos mucho (pico de deltaTime) no se invierte la dirección en bucle.
        if (Mathf.Abs(distanceFromStart) >= distance && Mathf.Sign(distanceFromStart) == direction)
        {
            direction *= -1f;
            FaceDirection(direction);
        }
    }
}
