using UnityEngine;
using TMPro;

public class WeaponAmmo : MonoBehaviour
{
    [SerializeField] private int magazineSize = 6;
    [SerializeField] private float reloadTime = 2f;
    [SerializeField] private TMP_Text ammoText;

    private int currentAmmo;
    private bool isReloading;
    private float reloadTimer;

    private GameManager gameManager;

    // NUEVO: evento para que otros sistemas (visual, sonido, partículas) reaccionen al disparo
    public event System.Action OnShot;

    public bool CanShoot => currentAmmo > 0 && !isReloading;

    // NUEVO: estado de solo lectura para la parte visual
    public bool IsReloading => isReloading;

    // NUEVO: progreso de la recarga 0..1 (la animación dura exactamente reloadTime)
    public float ReloadProgress =>
        isReloading && reloadTime > 0f ? 1f - (reloadTimer / reloadTime) : 0f;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();

        currentAmmo = magazineSize;

        UpdateAmmoText();
    }

    private void Update()
    {
        if (gameManager == null)
            return;

        if (gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        if (!isReloading)
            return;

        reloadTimer -= Time.deltaTime;

        if (reloadTimer <= 0f)
        {
            FinishReload();
        }
    }

    // CAMBIO: devuelve bool para que la regla "¿puedo disparar?" viva en un único sitio
    public bool UseAmmo()
    {
        if (!CanShoot)
            return false;

        currentAmmo--;

        OnShot?.Invoke(); // "?." evita error si nadie está suscrito

        UpdateAmmoText();

        if (currentAmmo <= 0)
        {
            StartReload();
        }

        return true;
    }

    // NUEVO: recarga manual (tecla R)
    public void TryReload()
    {
        if (isReloading || currentAmmo == magazineSize)
            return;

        StartReload();
    }

    private void StartReload()
    {
        isReloading = true;
        reloadTimer = reloadTime;

        UpdateAmmoText();
    }

    private void FinishReload()
    {
        currentAmmo = magazineSize;
        isReloading = false;

        UpdateAmmoText();
    }

    private void UpdateAmmoText()
    {
        // CAMBIO: evita NullReferenceException si no se ha asignado en el Inspector
        if (ammoText == null)
        {
            Debug.LogWarning("WeaponAmmo: falta asignar ammoText", this);
            return;
        }    


        if (isReloading)
        {
            ammoText.text = "RECARGANDO...";
        }
        else
        {
            ammoText.text = $"BALAS: {currentAmmo}/{magazineSize}";
        }
    }

    // Lo llama WeaponController al equipar un arma. Los campos serializados se quedan
    // como valores por defecto por si el objeto se usa sin controlador
    public void Configure(int newMagazineSize, float newReloadTime)
    {
        magazineSize = newMagazineSize;
        reloadTime = newReloadTime;

        // Un arma recién equipada empieza llena: cancelar la recarga evita que
        // termine con los datos del arma anterior
        currentAmmo = magazineSize;
        isReloading = false;
        reloadTimer = 0f;

        UpdateAmmoText();
    }

    public void ResetAmmo()
    {
        currentAmmo = magazineSize;
        isReloading = false;
        reloadTimer = 0f;

        UpdateAmmoText();
    }
}
