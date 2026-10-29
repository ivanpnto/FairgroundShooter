using UnityEngine;

// Medusa: sube a impulsos (arranque rápido que frena) y entre impulso e impulso se hunde despacio,
// con un ligero vaivén lateral. El mejor momento para acertarle es el punto alto de cada impulso.
public class PulseMovement : TargetMovement
{
    [SerializeField] private float pulsePeriod = 1.4f;                     // segundos por impulso
    [SerializeField] private float pulseRise = 0.9f;                       // metros que sube en cada impulso
    [SerializeField, Range(0.1f, 0.9f)] private float riseFraction = 0.35f; // parte del ciclo que dura la subida
    [SerializeField] private float sinkSpeed = 0.25f;                      // m/s de hundimiento entre impulsos
    [SerializeField] private float swayAmplitude = 0.25f;
    [SerializeField] private float swayFrequency = 0.6f;                   // ciclos por segundo
    [SerializeField] private float maxHeight = 3.4f;                       // altura (Y del mundo) que no supera

    private float time;
    private float height;   // desplazamiento vertical acumulado respecto a la posición inicial

    protected override void Move()
    {
        float dt = DeltaTime;
        time += dt;

        float phase = (time % pulsePeriod) / pulsePeriod;

        if (phase < riseFraction)
        {
            // Velocidad que decrece linealmente durante la subida (ease-out). Integrada sobre
            // toda la subida da exactamente pulseRise metros.
            float t = phase / riseFraction;
            float riseDuration = pulsePeriod * riseFraction;
            height += pulseRise * 2f * (1f - t) / riseDuration * dt;
        }
        else
        {
            height -= sinkSpeed * dt;
        }

        height = Mathf.Min(height, maxHeight - startPosition.y);

        float x = startPosition.x + Mathf.Sin(time * swayFrequency * 2f * Mathf.PI) * swayAmplitude;
        transform.position = new Vector3(x, startPosition.y + height, startPosition.z);
    }
}
