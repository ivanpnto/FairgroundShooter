using UnityEngine;

public class JumpMovement : TargetMovement
{
    [SerializeField] private float speed = 2f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float jumpDistance = 2f;
    [SerializeField] private float movementLimit = 4f;

    private float direction = 1f;
    private float jumpProgress;

    protected override void Move()
    {
        // Movimiento horizontal
        float horizontalMovement = speed * direction * Time.deltaTime;
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

        // Comprobamos los límites
        float distanceFromStart = position.x - startPosition.x;

        if (Mathf.Abs(distanceFromStart) >= movementLimit)
        {
            direction *= -1f;
        }

        transform.position = position;
    }
}