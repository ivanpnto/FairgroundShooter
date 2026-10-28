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

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();

        // Ambos componentes van en Player: GetComponent evita otro campo que asignar en la escena
        weaponController = GetComponent<WeaponController>();
    }

    private void Update()
    {
        if (gameManager == null)
            return;

        if (gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        // NUEVO: Mouse/Keyboard.current pueden ser null si no hay dispositivo conectado
        // Sin arma equipada no hay alcance con el que disparar; se comprueba antes de
        // UseAmmo() para no gastar una bala en un disparo que no ocurre
        if (weaponController == null || weaponController.CurrentWeapon == null)
            return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // CAMBIO: UseAmmo() comprueba y gasta la bala; solo disparamos si ha tenido éxito
            if (weaponAmmo.UseAmmo())
            {
                Shoot();
            }
        }

        // NUEVO: recarga manual
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            weaponAmmo.TryReload();
        }
    }

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
