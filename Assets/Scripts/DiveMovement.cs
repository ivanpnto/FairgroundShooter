using UnityEngine;

// Águila (nivel 3): cruza la caseta planeando y, de vez en cuando, cae en PICADO: baja de golpe,
// acelera, y vuelve a remontar hasta su altura de crucero. El mejor momento para acertarle es
// arriba, mientras planea; en el picado es rápida y cambia de altura.
//
// La altura sigue -depth·sin(π·k) con k de 0 a 1 durante el picado: baja y vuelve a subir de forma
// suave, sin saltos de velocidad. La inclinación del cuerpo sale de la derivada de esa curva
// (hacia dónde va de verdad), así que el pico apunta hacia abajo al caer y hacia arriba al remontar.
public class DiveMovement : TargetMovement
{
    [SerializeField] private float cruiseSpeed = 2.3f;                              // m/s en horizontal
    [SerializeField] private Vector2 cruiseTime = new Vector2(0.5f, 1.2f);         // planeo entre picados (s)
    [SerializeField] private float diveDepth = 1.6f;                                // metros que baja
    [SerializeField] private float diveDuration = 1.1f;                             // bajar y remontar (s)
    [SerializeField] private float diveSpeedBoost = 1.7f;                           // ×velocidad en lo más bajo
    [SerializeField] private float glideBob = 0.06f;                                // vaivén suave al planear (m)
    [SerializeField, Range(0f, 80f)] private float maxPitch = 40f;                  // inclinación máxima (grados)

    private float direction = 1f;
    private float cruiseTimer;
    private float diveProgress = -1f;   // -1 = planeando; 0..1 = en pleno picado
    private float time;
    private float x;

    protected override void Start()
    {
        base.Start();

        // Como el pájaro: cruza hacia el lado contrario al que aparece
        direction = startPosition.x > 0f ? -1f : 1f;
        FaceDirection(direction);

        x = startPosition.x;
        cruiseTimer = Random.Range(cruiseTime.x, cruiseTime.y);
    }

    protected override void Move()
    {
        float dt = DeltaTime;
        time += dt;

        float drop = 0f, dropRate = 0f, speed = cruiseSpeed;

        if (diveProgress < 0f)
        {
            cruiseTimer -= dt;
            if (cruiseTimer <= 0f)
                diveProgress = 0f;
        }
        else
        {
            diveProgress += dt / diveDuration;
            if (diveProgress >= 1f)
            {
                diveProgress = -1f;
                cruiseTimer = Random.Range(cruiseTime.x, cruiseTime.y);
            }
        }

        if (diveProgress >= 0f)
        {
            float s = Mathf.Sin(Mathf.PI * diveProgress);
            drop = diveDepth * s;
            // Derivada de la altura (m/s, negativa al bajar): para la inclinación
            dropRate = -diveDepth * Mathf.PI * Mathf.Cos(Mathf.PI * diveProgress) / diveDuration;
            speed = cruiseSpeed * (1f + (diveSpeedBoost - 1f) * s);
        }

        x += direction * speed * dt;
        float bob = diveProgress < 0f ? Mathf.Sin(time * 2.2f) * glideBob : 0f;
        transform.position = new Vector3(x, startPosition.y - drop + bob, startPosition.z);

        // Inclinación hacia donde se mueve. Con el modelo volteado (direction −1) el giro va al revés
        float pitch = Mathf.Clamp(Mathf.Atan2(dropRate, speed) * Mathf.Rad2Deg, -maxPitch, maxPitch);
        transform.rotation = Quaternion.Euler(0f, 0f, pitch * direction);
    }
}
