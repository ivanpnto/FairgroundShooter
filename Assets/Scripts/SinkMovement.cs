using UnityEngine;

// Neumático: cae desde la superficie despacio, oscilando a los lados y girando sobre sí mismo.
public class SinkMovement : TargetMovement
{
    [SerializeField] private float sinkSpeed = 0.45f;       // m/s
    [SerializeField] private float driftAmplitude = 0.35f;  // m de vaivén lateral
    [SerializeField] private float driftFrequency = 0.35f;  // ciclos por segundo
    [SerializeField] private float spinSpeed = 25f;         // grados por segundo
    [SerializeField] private float minHeight = 0.3f;        // se queda "posado" en el fondo

    private float time;

    protected override void Move()
    {
        float dt = DeltaTime;
        time += dt;

        float y = Mathf.Max(startPosition.y - sinkSpeed * time, minHeight);
        float x = startPosition.x + Mathf.Sin(time * driftFrequency * 2f * Mathf.PI) * driftAmplitude;

        transform.position = new Vector3(x, y, startPosition.z);
        transform.Rotate(0f, 0f, spinSpeed * dt, Space.Self);   // el collider gira con él: es redondo, no importa
    }
}
