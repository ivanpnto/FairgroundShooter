using UnityEngine;

public class SenoMovement : TargetMovement
{
    [Header("Configuración del Seno")]
    [SerializeField] private float horizontalSpeed = 3f;  // Velocidad de avance continuo
    [SerializeField] private float verticalAmplitude = 3f; // Altura de la ola (qué tanto sube y baja)
    [SerializeField] private float frequency = 3f;         // Qué tan juntas están las curvas de la ola

    private float time;
    private float direction = 1f;

    protected override void Start()
    {
        base.Start();

        // Cruza el puesto hacia el lado contrario al que aparece:
        // si nace a la derecha (x > 0) va hacia la izquierda, y viceversa.
        direction = startPosition.x > 0f ? -1f : 1f;
        FaceDirection(direction);
    }

    protected override void Move()
    {
        time += DeltaTime;

        // El eje X avanza constantemente en línea recta
        float x = time * horizontalSpeed * direction;

        // El eje Y sube y baja usando la función matemática del seno
        float y = Mathf.Sin(time * frequency) * verticalAmplitude;

        // Se aplica la nueva posición sumada a la posición inicial
        transform.position = startPosition + new Vector3(x, y, 0f);
    }
}
