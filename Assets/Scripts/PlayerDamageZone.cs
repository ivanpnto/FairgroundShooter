using UnityEngine;

// Lo que puede hacer daño al jugador al llegar a él (I7.1): proyectiles del boss, esbirros en el salto.
public interface IPlayerHazard
{
    // Corazones que quita (1 o 2)
    int HeartDamage { get; }

    // false si ya no debe hacer daño (lo han derribado, está expirando...)
    bool IsActiveHazard { get; }

    // Ha llegado al jugador: desaparece (haya quitado vida o no)
    void OnReachedPlayer();
}

// Trigger delante de la cámara (I7.1): lo que entra en él y es un IPlayerHazard hace daño.
//
// Lleva un Rigidbody kinematic propio. Unity solo lanza OnTriggerEnter si uno de los dos tiene
// Rigidbody: los proyectiles tienen el suyo, pero los esbirros (I7.5) se moverán por código, sin física.
// Con el Rigidbody aquí sirve para los dos.
//
// Capa PlayerZone: en la matriz de colisiones solo choca con BossHazard. Como es un trigger, el disparo
// (QueryTriggerInteraction.Ignore) lo atraviesa.
[RequireComponent(typeof(BoxCollider))]
public class PlayerDamageZone : MonoBehaviour
{
    private PlayerHealth playerHealth;

    // Bloque 2: lo que acaba de llegar y si ha quitado vida (false si eras invulnerable). PlayerHealth.Damaged
    // dice cuánto daño, pero no qué lo hizo: con esto los efectos distinguen una tarta de un esbirro
    public event System.Action<IPlayerHazard, bool> HazardReached;

    private void Reset() => Configure();

    private void Awake()
    {
        Configure();
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth == null)
            Debug.LogWarning("PlayerDamageZone: no hay PlayerHealth en la escena; nada hará daño.", this);
    }

    private void Configure()
    {
        GetComponent<BoxCollider>().isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        IPlayerHazard hazard = other.GetComponentInParent<IPlayerHazard>();
        if (hazard == null || !hazard.IsActiveHazard)
            return;

        // Se consume aunque el jugador sea invulnerable: si no, se quedaría delante de la cámara
        bool damaged = playerHealth != null && playerHealth.TakeHit(hazard.HeartDamage);
        HazardReached?.Invoke(hazard, damaged);   // antes de OnReachedPlayer: el proyectil aún no ha vuelto al pool
        hazard.OnReachedPlayer();
    }
}
