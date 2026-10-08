using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShoot : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float maxDistance = 100f;
    [SerializeField] private WeaponAmmo weaponAmmo;

    private GameManager gameManager;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
    }

    private void Update()
    {
        if (gameManager == null)
            return;

        if (gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (weaponAmmo.CanShoot)
            {
                Shoot();
                weaponAmmo.UseAmmo();
            }
        }
    }

    private void Shoot()
    {
        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
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