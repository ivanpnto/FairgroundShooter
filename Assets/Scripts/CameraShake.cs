using UnityEngine;

// Sacudida de cámara (J7.2, bloque 2) con el modelo de «trauma».
//
// AddTrauma(0–1) suma trauma, que baja solo con el tiempo. La sacudida es trauma² (los golpes pequeños apenas
// mueven la vista y los grandes la mueven mucho) por un ruido de Perlin en cabeceo, guiñada y alabeo. Perlin y no
// Random: es continuo, así que la cámara tiembla en vez de dar saltos de un fotograma a otro.
//
// El problema es que PlayerAim también escribe la rotación de esta cámara, y solo cuando se juega (en la
// revelación del boss no). Si la sacudida se sumase sin más, en los fotogramas en que PlayerAim no escribe se
// iría acumulando. Por eso se hace en dos tiempos:
//   1. Update, ANTES que PlayerAim (DefaultExecutionOrder −100): quita la sacudida del fotograma anterior.
//   2. LateUpdate, después de todo: aplica la de este fotograma y la recuerda.
// Así PlayerAim, PlayerShoot y el raycast del disparo ven siempre la puntería «limpia»: la sacudida es solo
// visual y nunca desvía una bala. El arma, hija de la cámara, tiembla con ella.
[DefaultExecutionOrder(-100)]
public class CameraShake : MonoBehaviour
{
    [Tooltip("Grados máximos con trauma 1 (cabeceo, guiñada, alabeo)")]
    [SerializeField] private Vector3 maxAngles = new Vector3(4f, 3f, 5f);
    [SerializeField] private float frequency = 22f;
    [Tooltip("Trauma que se pierde por segundo")]
    [SerializeField] private float recovery = 1.6f;

    public float Trauma { get; private set; }

    private Quaternion applied = Quaternion.identity;
    private float seed;

    private void Awake() => seed = Random.value * 100f;

    public void AddTrauma(float amount) => Trauma = Mathf.Clamp01(Trauma + amount);

    private void Update()
    {
        // Paso 1: quitar lo que se puso en el LateUpdate anterior
        transform.localRotation *= Quaternion.Inverse(applied);
        applied = Quaternion.identity;
    }

    private void LateUpdate()
    {
        if (Trauma <= 0f) return;

        float shake = Trauma * Trauma;
        float t = Time.time * frequency;
        applied = Quaternion.Euler(
            maxAngles.x * shake * (Mathf.PerlinNoise(seed, t) * 2f - 1f),
            maxAngles.y * shake * (Mathf.PerlinNoise(seed + 1f, t) * 2f - 1f),
            maxAngles.z * shake * (Mathf.PerlinNoise(seed + 2f, t) * 2f - 1f));
        transform.localRotation *= applied;   // paso 2

        Trauma = Mathf.Max(0f, Trauma - recovery * Time.deltaTime);
    }

    private void OnDisable()
    {
        transform.localRotation *= Quaternion.Inverse(applied);
        applied = Quaternion.identity;
        Trauma = 0f;
    }
}
