using UnityEngine;

// Búho: se asoma por detrás de los arbustos, aguanta un momento a la vista y vuelve a esconderse,
// una y otra vez en el mismo sitio. Solo se le puede acertar mientras está a la vista: con el
// collider siempre activo se le podría dar a través del arbusto (los arbustos no tienen collider).
public class PeekMovement : TargetMovement
{
    [SerializeField] private float riseHeight = 1.0f;     // metros que sube al asomarse
    [SerializeField] private float riseTime = 0.3f;
    [SerializeField] private float holdTime = 1.1f;       // tiempo a la vista
    [SerializeField] private float hideTime = 0.3f;
    [SerializeField] private Vector2 waitRange = new Vector2(0.5f, 1.2f);   // escondido entre asomadas
    [SerializeField, Range(0f, 1f)] private float hittableFrom = 0.6f;     // fracción asomada desde la que se puede acertar

    private enum Phase { Wait, Rise, Hold, Hide }

    private Phase phase = Phase.Wait;
    private float timer;
    private Collider[] colliders;

    protected override void Start()
    {
        base.Start();
        colliders = GetComponents<Collider>();
        timer = Random.Range(0.1f, 0.4f);
        SetHittable(false);
    }

    protected override void Move()
    {
        timer -= DeltaTime;
        float shown = 0f;   // 0 = escondido, 1 = asomado del todo

        switch (phase)
        {
            case Phase.Wait:
                if (timer <= 0f) { phase = Phase.Rise; timer = riseTime; }
                break;

            case Phase.Rise:
                shown = 1f - Mathf.Max(timer, 0f) / riseTime;
                if (timer <= 0f) { phase = Phase.Hold; timer = holdTime; }
                break;

            case Phase.Hold:
                shown = 1f;
                if (timer <= 0f) { phase = Phase.Hide; timer = hideTime; }
                break;

            case Phase.Hide:
                shown = Mathf.Max(timer, 0f) / hideTime;
                if (timer <= 0f) { phase = Phase.Wait; timer = Random.Range(waitRange.x, waitRange.y); }
                break;
        }

        float eased = 1f - (1f - shown) * (1f - shown);   // sale rápido y frena arriba
        transform.position = startPosition + Vector3.up * (riseHeight * eased);

        SetHittable(shown >= hittableFrom);
    }

    // Al morir, Target desactiva este componente, así que esto ya no vuelve a encender el collider
    private void SetHittable(bool hittable)
    {
        foreach (Collider col in colliders)
        {
            if (col != null)
                col.enabled = hittable;
        }
    }
}
