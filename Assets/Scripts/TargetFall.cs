using System.Collections;
using UnityEngine;

// Reacción de los objetivos al impacto, como en una caseta de tiro real:
//  - Impacto que no lo derriba (p. ej. la tortuga): se tambalea hacia atrás y vuelve.
//  - Impacto final: se abate 90º hacia atrás sobre su base, rebota un poco y se hunde tras el decorado.
// Solo anima: la lógica (puntos, collider, destrucción) sigue en Target. Para que dé tiempo a verlo,
// el destroyDelay del Target tiene que ser al menos TotalDuration.
[RequireComponent(typeof(Target))]
public class TargetFall : MonoBehaviour
{
    [Header("Caída")]
    [SerializeField] private float fallDuration = 0.35f;
    [SerializeField] private float fallAngle = 90f;
    [SerializeField] private float bounceAngle = 10f;
    [SerializeField] private float bounceDuration = 0.14f;

    [Header("Hundimiento")]
    [SerializeField] private float sinkDelay = 0.15f;
    [SerializeField] private float sinkDuration = 0.4f;
    [SerializeField] private float sinkDistance = 0.8f;

    [Header("Tambaleo (impactos que no lo derriban)")]
    [SerializeField] private float wobbleAngle = 18f;
    [SerializeField] private float wobbleDuration = 0.22f;

    public float TotalDuration => fallDuration + bounceDuration + sinkDelay + sinkDuration;

    private Target target;
    private Coroutine wobble;

    private void Awake()
    {
        target = GetComponent<Target>();
        target.Damaged += OnDamaged;
        target.Killed += OnKilled;
    }

    private void OnDestroy()
    {
        if (target != null)
        {
            target.Damaged -= OnDamaged;
            target.Killed -= OnKilled;
        }
    }

    private void OnDamaged(Target t, int hitsLeft)
    {
        if (wobble == null)
            wobble = StartCoroutine(Wobble());
    }

    private void OnKilled(Target t)
    {
        if (wobble != null)
        {
            StopCoroutine(wobble);
            wobble = null;
        }

        StartCoroutine(Fall());
    }

    // Eje de giro: la base del modelo (parte inferior de su silueta), en el momento del impacto.
    // Girar sobre +X del mundo con ángulo positivo lleva la parte de arriba hacia +Z: cae alejándose de la cámara.
    private Vector3 Pivot()
    {
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool first = true;

        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            if (!rend.enabled) continue;
            if (first) { bounds = rend.bounds; first = false; }
            else bounds.Encapsulate(rend.bounds);
        }

        return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }

    private IEnumerator Wobble()
    {
        Vector3 pivot = Pivot();
        float applied = 0f;
        float t = 0f;

        while (t < wobbleDuration)
        {
            t += Time.deltaTime;
            float angle = wobbleAngle * Mathf.Sin(Mathf.Clamp01(t / wobbleDuration) * Mathf.PI);   // va y vuelve
            transform.RotateAround(pivot, Vector3.right, angle - applied);
            applied = angle;
            yield return null;
        }

        transform.RotateAround(pivot, Vector3.right, -applied);   // deja el ángulo exacto de partida
        wobble = null;
    }

    private IEnumerator Fall()
    {
        Vector3 pivot = Pivot();
        float applied = 0f;
        float t = 0f;

        // Caída acelerada (como por gravedad): ángulo proporcional a t²
        while (t < fallDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fallDuration);
            float angle = fallAngle * k * k;
            transform.RotateAround(pivot, Vector3.right, angle - applied);
            applied = angle;
            yield return null;
        }

        // Pequeño rebote al chocar contra el tope
        t = 0f;
        while (t < bounceDuration)
        {
            t += Time.deltaTime;
            float angle = fallAngle - bounceAngle * Mathf.Sin(Mathf.Clamp01(t / bounceDuration) * Mathf.PI);
            transform.RotateAround(pivot, Vector3.right, angle - applied);
            applied = angle;
            yield return null;
        }

        transform.RotateAround(pivot, Vector3.right, fallAngle - applied);

        yield return new WaitForSeconds(sinkDelay);

        // Se hunde tras el decorado (acelerando) hasta que Target lo destruye
        Vector3 start = transform.position;
        t = 0f;
        while (t < sinkDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / sinkDuration);
            transform.position = start + Vector3.down * (sinkDistance * k * k);
            yield return null;
        }
    }
}
