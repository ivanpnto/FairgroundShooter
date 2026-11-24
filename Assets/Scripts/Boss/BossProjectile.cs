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
//
// Object pooling (F8): BossController no crea ni destruye proyectiles en cada tanda, los saca de un pool y
// los devuelve. Por eso el aviso Removed sale en OnDisable (el pool desactiva el objeto al devolverlo, y
// Unity también llama a OnDisable antes de destruirlo) y ResetForFlight deja la física como recién creada.
public enum ProjectileKind { Ball, Club, Pie }

[RequireComponent(typeof(Target), typeof(Rigidbody))]
public class BossProjectile : MonoBehaviour, IPlayerHazard
{
    [Tooltip("Giro al volar (grados/s, aleatorio hasta este valor por eje): que no parezca un cubo flotando")]
    [SerializeField] private float maxSpin = 360f;

    // Bloque 2: un solo prefab con los tres modelos como hijos (en el orden de ProjectileKind) y uno visible
    // al azar en cada lanzamiento. Un prefab por modelo obligaría a tener un pool por prefab; así el pool, la
    // física y el collider (una esfera que vale para los tres) son los mismos y solo cambia lo que se ve
    [Tooltip("Modelos hijos en el orden de ProjectileKind: bola, maza, tarta. Vacío = el aspecto del prefab")]
    [SerializeField] private GameObject[] models;

    public ProjectileKind Kind { get; private set; }

    public int HeartDamage { get; private set; } = 1;
    public bool IsActiveHazard => target != null && !target.IsDead && !target.IsExpiring && !reached;

    // Se avisa al boss cuando deja de volar (derribado, llegado o expirado; devuelto al pool o destruido)
    // para llevar la cuenta de los que hay en el aire
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

        PickModel();
    }

    // Enciende un modelo al azar y apaga los demás
    private void PickModel()
    {
        if (models == null || models.Length == 0)
            return;
        int index = Random.Range(0, models.Length);
        for (int i = 0; i < models.Length; i++)
            if (models[i] != null) models[i].SetActive(i == index);
        Kind = (ProjectileKind)Mathf.Min(index, (int)ProjectileKind.Pie);
    }

    // Corrección final (24/11): en el último tramo del vuelo el proyectil se recoloca hacia la cabeza del jugador,
    // esté de pie o agachado. Antes iba a un punto fijo a la altura de pie: al agacharse te pasaba por encima y te
    // quitaba vida igual (la zona de daño es fija y llega hasta abajo), así que parecía que esquivabas y no era así.
    // Agacharse solo debe servir contra el chorro de agua. No cambia el tiempo de vuelo (el aviso) y empieza tarde,
    // cuando ya se ve que viene a por ti: no es un misil teledirigido, es «se te viene encima».
    [Tooltip("Parte del vuelo a partir de la cual corrige hacia la cabeza del jugador (0..1)")]
    [Range(0f, 1f)] [SerializeField] private float homingStart = 0.5f;

    private float flightDuration, flightElapsed;
    private Vector3 aimOffset;   // la dispersión del lanzamiento, que se conserva en parte al corregir
    private float destinationZ;  // profundidad del destino (la zona de daño): se corrige solo en altura y lado

    private void FixedUpdate()
    {
        body.AddForce(gravity, ForceMode.Acceleration);
        if (flightDuration <= 0f || reached || target == null || target.IsDead) return;

        flightElapsed += Time.fixedDeltaTime;
        float remaining = flightDuration - flightElapsed;
        if (flightElapsed < flightDuration * homingStart || remaining < 0.03f) return;

        Camera cam = Camera.main;
        if (cam == null) return;
        // A la altura y el lado de la cabeza, pero en la profundidad del destino: el golpe cuenta al entrar en la zona
        // de daño, delante de la cámara, y ahí ya tiene que ir a la altura de los ojos
        Vector3 eye = cam.transform.position;
        Vector3 aim = new Vector3(eye.x, eye.y, destinationZ) + aimOffset * 0.4f;
        // Velocidad que llega a aim en el tiempo que queda con la misma gravedad, y se va acercando a ella (no de golpe)
        Vector3 needed = (aim - body.position - 0.5f * gravity * remaining * remaining) / remaining;
        float blend = 1f - Mathf.Exp(-Time.fixedDeltaTime * 25f);
        body.linearVelocity = Vector3.Lerp(body.linearVelocity, needed, blend);
    }

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
        flightDuration = flightTime;
        flightElapsed = 0f;
        // La dispersión del lanzamiento respecto al centro de la zona de daño (no respecto a la cámara: si el jugador
        // estaba agachado al lanzarla, eso no es dispersión y la mandaría por encima de la cabeza)
        PlayerDamageZone zone = SceneCache<PlayerDamageZone>.Get();
        aimOffset = zone != null ? destination - zone.transform.position : Vector3.zero;
        aimOffset.z = 0f;
        destinationZ = destination.z;
        body.angularVelocity = new Vector3(Random.Range(-maxSpin, maxSpin), Random.Range(-maxSpin, maxSpin),
            Random.Range(-maxSpin, maxSpin)) * Mathf.Deg2Rad;
    }

    // Lo llama BossController al sacarlo del pool, antes de Launch: lo coloca en el origen sin la velocidad del
    // vuelo anterior (un Rigidbody la conserva aunque se desactive el objeto) y sin gravedad hasta el lanzamiento
    public void ResetForFlight(Vector3 position, Quaternion rotation)
    {
        PickModel();
        reached = false;
        gravity = Vector3.zero;
        HeartDamage = 1;
        flightDuration = 0f;
        flightElapsed = 0f;

        // Se mueve el Rigidbody y no solo el Transform: con interpolación, mover solo el Transform haría
        // que el primer fotograma se dibujase interpolando desde donde acabó el vuelo anterior
        transform.SetPositionAndRotation(position, rotation);
        body.position = position;
        body.rotation = rotation;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    public void OnReachedPlayer()
    {
        // Si este golpe quitó el último corazón, la derrota ya lo devolvió al pool (ClearProjectiles) dentro de
        // PlayerHealth.TakeHit, antes de llegar aquí: devolverlo otra vez lanzaba InvalidOperationException
        if (!gameObject.activeSelf)
            return;
        reached = true;
        target.Despawn(0f);   // destruir o devolver al pool: lo decide Target
    }

    private void OnDisable() => Removed?.Invoke(this);
}
