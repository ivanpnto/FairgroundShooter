using UnityEngine;

// Proyectil del boss (I7.3): tarta, bola de malabarista, bolo.
//
// Es un Target más (vida 1, pocos puntos): así derribarlo da puntos, cuenta como acierto para la racha,
// suelta confeti y le afecta la onda expansiva sin escribir nada nuevo. Lo que añade este componente es
// el vuelo (Rigidbody: la física integra la parábola) y el daño al llegar a la zona del jugador.
//
// Gravedad PROPIA, no la del proyecto: se elige el tiempo de vuelo (el aviso) y la altura del arco (que no
// pase por encima del techo de la caseta) y de ahí sale la gravedad. Con la gravedad real no se pueden
// fijar las dos cosas a la vez.
//
// Física: capa BossHazard, que en la matriz de colisiones solo choca con PlayerZone. El collider NO
// es trigger (el disparo ignora los triggers), así que, sin esa capa, chocaría con la pared, el mostrador
// y el techo de la caseta.
//
// Prefab: raíz con collider (no trigger) + Rigidbody + Target (vida 1, lifetime ~5 s, destroyDelay 0) +
// este componente, en la capa BossHazard.
[RequireComponent(typeof(Target), typeof(Rigidbody))]
public class BossProjectile : MonoBehaviour, IPlayerHazard
{
    [Tooltip("Giro al volar (grados/s, aleatorio hasta este valor por eje): que no parezca un cubo flotando")]
    [SerializeField] private float maxSpin = 360f;

    public int HeartDamage { get; private set; } = 1;
    public bool IsActiveHazard => target != null && !target.IsDead && !target.IsExpiring && !reached;

    // Se avisa al boss al destruirse (derribado, llegado o expirado) para llevar la cuenta de los que vuelan
    public event System.Action<BossProjectile> Removed;

    private Target target;
    private Rigidbody body;
    private bool reached;
    private Vector3 gravity;   // la aplica FixedUpdate (useGravity = false)

    private void Awake()
    {
        target = GetComponent<Target>();
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;   // sin tirones entre pasos de física

        // Derribado en el aire: deja de ser peligroso, pero no se queda flotando
        target.Killed += _ => body.linearVelocity *= 0.3f;
    }

    private void FixedUpdate() => body.AddForce(gravity, ForceMode.Acceleration);

    // Tiro balístico que pasa por destination al cabo de flightTime segundos con un arco de arcHeight metros.
    //   Altura del arco sobre la recta, a mitad de camino:  h = g·t² / 8   →   g = 8·h / t²  (hacia abajo)
    //   Posición:  p(t) = p0 + v0·t + ½·g·t²                                →   v0 = (destino − p0 − ½·g·t²) / t
    // Se fija el TIEMPO de vuelo y no la velocidad porque el tiempo es el aviso que tiene el jugador para
    // derribarlo (§27.5: 1,6–2,2 s); y la altura, porque decide si se ve todo el vuelo.
    public void Launch(Vector3 destination, float flightTime, float arcHeight, int heartDamage)
    {
        HeartDamage = heartDamage;
        flightTime = Mathf.Max(0.1f, flightTime);
        gravity = Vector3.down * (8f * Mathf.Max(0f, arcHeight) / (flightTime * flightTime));

        Vector3 displacement = destination - transform.position;
        Vector3 velocity = (displacement - 0.5f * gravity * flightTime * flightTime) / flightTime;

        body.linearVelocity = velocity;
        body.angularVelocity = new Vector3(Random.Range(-maxSpin, maxSpin), Random.Range(-maxSpin, maxSpin),
            Random.Range(-maxSpin, maxSpin)) * Mathf.Deg2Rad;
    }

    public void OnReachedPlayer()
    {
        reached = true;
        Destroy(gameObject);
    }

    private void OnDestroy() => Removed?.Invoke(this);
}
