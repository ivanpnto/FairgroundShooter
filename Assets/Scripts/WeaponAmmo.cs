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

    public bool CanShoot => currentAmmo > 0 && !isReloading;

    private void Awake()
    {
        currentAmmo = magazineSize;
        UpdateAmmoText();
    }

    private void Update()
    {
        if (!isReloading)
            return;

        reloadTimer -= Time.deltaTime;

        if (reloadTimer <= 0f)
        {
            FinishReload();
        }
    }

    public void UseAmmo()
    {
        if (!CanShoot)
            return;

        currentAmmo--;

        UpdateAmmoText();

        if (currentAmmo <= 0)
        {
            StartReload();
        }
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
        if (isReloading)
        {
            ammoText.text = "RECARGANDO...";
        }
        else
        {
            ammoText.text = $"BALAS: {currentAmmo}/{magazineSize}";
        }
    }
}