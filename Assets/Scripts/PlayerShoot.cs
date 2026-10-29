using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShoot : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private WeaponAmmo weaponAmmo;

    // NUEVO: capas que puede golpear el disparo (quita la del jugador/arma si la tienen)
    [SerializeField] private LayerMask hitMask = ~0; // ~0 = todas las capas

    private GameManager gameManager;

    // El alcance sale del arma equipada; ya no hay campo maxDistance propio para
    // que no existan dos valores que puedan contradecirse
    private WeaponController weaponController;
    private PlayerAim playerAim;

    // Momento (en Time.time) a partir del cual se permite el siguiente disparo
    private float nextFireTime;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();

        // Todos van en Player: GetComponent evita más campos que asignar en la escena
        weaponController = GetComponent<WeaponController>();
        playerAim = GetComponent<PlayerAim>();
    }

    // Suscribirse en OnEnable y desuscribirse en OnDisable evita llamadas
    // a un objeto desactivado o destruido
    private void OnEnable()
    {
        if (weaponController != null) weaponController.OnWeaponEquipped += HandleWeaponEquipped;
    }

    private void OnDisable()
    {
        if (weaponController != null) weaponController.OnWeaponEquipped -= HandleWeaponEquipped;
    }

    // El arma recién equipada puede disparar al momento: no hereda la espera de la anterior
    private void HandleWeaponEquipped(WeaponData data) => nextFireTime = Time.time;

    private void Update()
    {
        if (gameManager == null)
            return;

        if (gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        // Sin arma equipada no hay alcance con el que disparar; se comprueba antes de
        // UseAmmo() para no gastar una bala en un disparo que no ocurre
        if (weaponController == null || weaponController.CurrentWeapon == null)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Antes de leer CurrentWeapon: si se cambia de arma este frame, el disparo
        // de este mismo frame ya usa los datos de la nueva
        HandleDebugWeaponKeys();
#endif

        WeaponData weapon = weaponController.CurrentWeapon;

        // NUEVO: Mouse/Keyboard.current pueden ser null si no hay dispositivo conectado
        if (Mouse.current != null)
        {
            // Automática: dispara mientras se mantiene el botón. Semiautomática: un disparo por clic
            bool wantsToShoot = weapon.isAutomatic
                ? Mouse.current.leftButton.isPressed
                : Mouse.current.leftButton.wasPressedThisFrame;

            // El tiempo se comprueba antes que la munición para no gastar una bala si aún no toca.
            // CAMBIO: UseAmmo() comprueba y gasta la bala; solo disparamos si ha tenido éxito
            if (wantsToShoot && Time.time >= nextFireTime && weaponAmmo.UseAmmo())
            {
                // Cadencia: el siguiente disparo se programa sumando el intervalo al momento
                // en que TOCABA disparar, no al momento actual. El disparo siempre ocurre algo
                // tarde (al llegar el frame), y con "Time.time + intervalo" ese retraso se
                // perdería en cada disparo y la cadencia real quedaría por debajo de fireRate.
                // Pero si nextFireTime se ha quedado atrás más de un intervalo (el jugador
                // llevaba un rato sin disparar), sumar sin más dejaría "disparos acumulados"
                // que saldrían seguidos al volver a pulsar; por eso antes se trae al presente.
                if (Time.time - nextFireTime > weapon.TimeBetweenShots)
                    nextFireTime = Time.time;

                nextFireTime += weapon.TimeBetweenShots;

                Shoot();

                // Después de Shoot(): el retroceso desvía los disparos siguientes, no este.
                // La vista no se recupera hasta 1.5 intervalos sin disparar: en ráfaga continua
                // (o clicando a la cadencia máxima) el retroceso se acumula y el arma "trepa".
                // Con tope de 0.25 s: en armas lentas (Barrett, 1.25 s entre disparos) cada
                // disparo debe ser un golpe que se asienta antes del siguiente, no acumularse
                if (playerAim != null)
                    playerAim.AddRecoil(weapon.cameraKick, weapon.cameraKickSide,
                        weapon.cameraRecoverySpeed, Mathf.Min(weapon.TimeBetweenShots * 1.5f, 0.25f));
            }
        }

        // NUEVO: recarga manual
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            weaponAmmo.TryReload();
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Atajo de depuración: 1–5 equipan las armas de AvailableWeapons en plena partida.
    // Solo existe en el editor y en builds de desarrollo; en una build normal no se
    // compila, así que no sirve para hacer trampas (Equip rellena el cargador)
    private void HandleDebugWeaponKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        var weapons = weaponController.AvailableWeapons;
        if (weapons == null)
            return;

        for (int i = 0; i < weapons.Count && i < 5; i++)
        {
            // Fila superior y teclado numérico: Key.Digit1..5 y Key.Numpad1..5 son consecutivas
            if (!keyboard[Key.Digit1 + i].wasPressedThisFrame && !keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                continue;

            // La misma arma no se reequipa: rellenaría el cargador gratis
            if (weapons[i] != null && weapons[i] != weaponController.CurrentWeapon)
            {
                weaponController.Equip(weapons[i]);
                Debug.Log($"Depuración: equipada {weapons[i].displayName} (tecla {i + 1})");
            }
            return;
        }
    }
#endif

    private void Shoot()
    {
        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        if (Physics.Raycast(ray, out RaycastHit hit, weaponController.CurrentWeapon.maxDistance, hitMask, QueryTriggerInteraction.Ignore))
        {
            Debug.Log("Impacto en: " + hit.collider.gameObject.name);

            Target target = hit.collider.GetComponent<Target>();

            if (target != null)
            {
                target.Hit();
            }
        }
    }
}
