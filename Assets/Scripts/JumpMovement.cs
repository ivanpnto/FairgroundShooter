using UnityEngine;

public class JumpMovement : TargetMovement
{
    [SerializeField] private float speed = 2f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float jumpDistance = 2f;
    [SerializeField] private float movementLimit = 4f;
    [SerializeField] private bool randomStartDirection = true;
    [Tooltip("Al asustarse (ShyTarget) huye en sentido contrario a esta velocidad durante fleeTime s")]
    [SerializeField] private float fleeSpeedMultiplier = 2.2f;
    [SerializeField] private float fleeTime = 0.7f;

    private float direction = 1f;
    private float jumpProgress;
    private float fleeLeft;

    // Susto: media vuelta y a correr. Huyendo no se vuelve a asustar (no cambiaría nada)
    public override bool Startle()
    {
        if (fleeLeft > 0f)
            return false;
        direction *= -1f;
        FaceDirection(direction);
        fleeLeft = fleeTime;
        return true;
    }

    protected override void Start()
    {
        base.Start();

        if (randomStartDirection && Random.value < 0.5f)
            direction = -1f;

        FaceDirection(direction);
    }

    protected override void Move()
    {
        // Movimiento horizontal (más rápido mientras huye de un susto)
        float boost = fleeLeft > 0f ? fleeSpeedMultiplier : 1f;
        fleeLeft = Mathf.Max(0f, fleeLeft - DeltaTime);
        float horizontalMovement = speed * boost * direction * DeltaTime;
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
