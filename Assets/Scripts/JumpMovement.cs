using UnityEngine;

public class JumpMovement : TargetMovement
{
    [SerializeField] private float speed = 2f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float jumpDistance = 2f;
    [SerializeField] private float movementLimit = 4f;
    [SerializeField] private bool randomStartDirection = true;

    private float direction = 1f;
    private float jumpProgress;

    protected override void Start()
    {
        base.Start();

        if (randomStartDirection && Random.value < 0.5f)
            direction = -1f;

        FaceDirection(direction);
    }

    protected override void Move()
    {
        // Movimiento horizontal
        float horizontalMovement = speed * direction * DeltaTime;
        transform.position += Vector3.right * horizontalMovement;

        // Progreso del salto actual
        jumpProgress += Mathf.Abs(horizontalMovement) / jumpDistance;

        if (jumpProgress >= 1f)
        {
            jumpProgress -= 1f;
        }

        // Altura del salto
        float height = jumpHeight * Mathf.Abs(Mathf.Sin(jumpProgress * Mathf.PI));

        Vector3 position = transform.position;
        position.y = startPosition.y + height;

        // Comprobamos los límites (mismo criterio que HorizontalMovement)
        float distanceFromStart = position.x - startPosition.x;

        if (Mathf.Abs(distanceFromStart) >= movementLimit && Mathf.Sign(distanceFromStart) == direction)
        {
            direction *= -1f;
            FaceDirection(direction);
        }

        transform.position = position;
    }
}
