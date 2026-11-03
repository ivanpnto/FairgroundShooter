using UnityEngine;

// Animación "en reposo" de una pieza suelta de un modelo (I4.1): el ala del pájaro, los tentáculos de
// la medusa, los ojos del búho... Un solo script genérico en lugar de uno por animal: cambia el
// parámetro (qué se oscila, cuánto y a qué ritmo), no el código.
//
// Va en la PIEZA (hijo del modelo), nunca en la raíz del objetivo: la raíz la mueven los scripts de
// movimiento y TargetFall. Como la pieza oscila en su espacio local, hereda sin más el volteo de
// FaceDirection, la caída al derribarlo y la subida de TargetAppear.
//
// La pieza tiene que tener el pivote donde "se engancha" (hombro del ala, nacimiento del tentáculo,
// centro del ojo): por eso se separan en Blender con el origen ahí antes de exportar.
public class IdleOscillator : MonoBehaviour
{
    public enum Channel { Rotation, Scale }
    public enum Wave
    {
        Sine,    // vaivén continuo (ala, tentáculos)
        Blink    // casi siempre en reposo y, de vez en cuando, un pulso rápido (parpadeo)
    }

    [SerializeField] private Channel channel = Channel.Rotation;
    [SerializeField] private Wave wave = Wave.Sine;

    [Tooltip("Rotation: eje de giro local (para un recorte plano, Z gira dentro del plano de la silueta). " +
             "Scale: ejes que se escalan (p. ej. (0,1,0) para cerrar un ojo en vertical)")]
    [SerializeField] private Vector3 axis = Vector3.forward;

    [Tooltip("Rotation: grados a cada lado. Scale: cuánto se reduce en el pulso (0,9 = se queda al 10 %)")]
    [SerializeField] private float amplitude = 25f;

    [Header("Sine")]
    [SerializeField, Min(0f)] private float frequency = 3f;   // ciclos por segundo
    [Tooltip("Desfase fijo en ciclos (0-1): los tentáculos con desfases distintos parecen una onda")]
    [SerializeField, Range(0f, 1f)] private float phaseOffset = 0f;
    [Tooltip("Añade un desfase aleatorio al aparecer, para que dos animales iguales no vayan sincronizados")]
    [SerializeField] private bool randomPhase = true;

    [Header("Blink")]
    [SerializeField] private Vector2 blinkInterval = new Vector2(1.5f, 4f);
    [SerializeField, Min(0.01f)] private float blinkDuration = 0.14f;

    [Header("Piezas enlazadas")]
    [Tooltip("Reciben exactamente la misma oscilación (los dos ojos tienen que parpadear a la vez)")]
    [SerializeField] private Transform[] linked = new Transform[0];

    private Transform[] parts;            // esta pieza + las enlazadas
    private Quaternion[] baseRotations;
    private Vector3[] baseScales;
    private float phase;      // en radianes
    private float nextBlink;
    private float blinkTime = -1f;

    private void Awake()
    {
        parts = new Transform[linked.Length + 1];
        parts[0] = transform;
        linked.CopyTo(parts, 1);

        baseRotations = new Quaternion[parts.Length];
        baseScales = new Vector3[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == null) continue;
            baseRotations[i] = parts[i].localRotation;
            baseScales[i] = parts[i].localScale;
        }
    }

    private void OnEnable()
    {
        phase = (phaseOffset + (randomPhase ? Random.value : 0f)) * 2f * Mathf.PI;
        nextBlink = Random.Range(blinkInterval.x, blinkInterval.y);
        blinkTime = -1f;
    }

    private void OnDisable()
    {
        // Deja la pieza en su pose original (útil en el editor y si se reutiliza el objeto)
        if (parts == null) return;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == null) continue;
            parts[i].localRotation = baseRotations[i];
            parts[i].localScale = baseScales[i];
        }
    }

    private void Update()
    {
        // 'v' va de -1 a 1 (Sine) o de 0 a 1 (Blink: 1 = pulso máximo)
        float v = wave == Wave.Sine ? SineValue() : BlinkValue();

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == null) continue;

            if (channel == Channel.Rotation)
            {
                parts[i].localRotation = baseRotations[i] * Quaternion.AngleAxis(amplitude * v, axis);
            }
            else
            {
                // Solo en los ejes marcados: escala = base × (1 − amplitud·v)
                float k = 1f - amplitude * v;
                Vector3 s = baseScales[i];
                if (axis.x != 0f) s.x *= k;
                if (axis.y != 0f) s.y *= k;
                if (axis.z != 0f) s.z *= k;
                parts[i].localScale = s;
            }
        }
    }

    private float SineValue()
    {
        phase += frequency * 2f * Mathf.PI * Time.deltaTime;
        return Mathf.Sin(phase);
    }

    private float BlinkValue()
    {
        if (blinkTime < 0f)
        {
            nextBlink -= Time.deltaTime;
            if (nextBlink > 0f) return 0f;
            blinkTime = 0f;   // empieza un parpadeo
        }

        blinkTime += Time.deltaTime;
        float k = blinkTime / blinkDuration;

        if (k >= 1f)
        {
            blinkTime = -1f;
            nextBlink = Random.Range(blinkInterval.x, blinkInterval.y);
            return 0f;
        }

        return Mathf.Sin(k * Mathf.PI);   // cierra y abre en un solo pulso
    }
}
