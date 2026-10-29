using UnityEngine;

/// <summary>
/// Latido suave de escala para llamar la atención sobre un botón. Solo presentación.
/// </summary>
public class UIPulse : MonoBehaviour
{
    [Tooltip("Cuánto crece en el punto máximo (0.05 = un 5 %)")]
    [SerializeField] private float amount = 0.05f;
    [Tooltip("Latidos por segundo")]
    [SerializeField] private float speed = 1.2f;

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    // Al ocultarse el panel se deja en su tamaño original: si no, al volver a
    // mostrarse empezaría con la escala a medias del último frame
    private void OnDisable()
    {
        transform.localScale = baseScale;
    }

    private void Update()
    {
        // Seno de 0 a 1 (no de -1 a 1): el botón solo crece, nunca encoge por debajo
        // de su tamaño de diseño, así que no se monta sobre lo que tiene alrededor
        float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed * Mathf.PI * 2f);
        transform.localScale = baseScale * (1f + amount * t);
    }
}
