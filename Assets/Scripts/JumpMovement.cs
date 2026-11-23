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
    [Tooltip("Segundos de huida antes de meterse en la madriguera (baja tras el decorado y se pierde). Negativo = " +
             "no se esconde, solo huye")]
    [SerializeField] private float burrowAfter = 0.45f;

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
        // Antes solo corría más y se le podía seguir disparando igual: el susto casi no se notaba. Ahora se esconde,
        // como el búho: o se le acierta en el primer medio segundo o se pierde
        if (burrowAfter >= 0f)
        {
            Target target = GetComponent<Target>();
            if (target != null) target.ExpireIn(burrowAfter);
        }
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
