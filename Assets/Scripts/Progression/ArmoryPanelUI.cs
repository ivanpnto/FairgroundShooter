// Armería: una fila por cada arma de WeaponController.AvailableWeapons, en el orden de los menús.
// De momento es una lista como Habilidades; cuando sea la pantalla con las armas expuestas, se
// sustituye este panel y MenuController.ShowArmory sigue llevando a él
public class ArmoryPanelUI : ShopListPanelUI
{
    private WeaponController weaponController;

    protected override void Awake()
    {
        base.Awake();
        weaponController = FindFirstObjectByType<WeaponController>();
    }

    protected override void AddRows(int credits)
    {
        if (weaponController == null || weaponController.AvailableWeapons == null)
            return;

        foreach (WeaponData weapon in weaponController.AvailableWeapons)
        {
            if (weapon == null)
                continue;

            bool unlocked = profileManager.IsWeaponUnlocked(weapon.id);
            string mode = weapon.isAutomatic ? "automática" : "semiautomática";

            NewRow().Setup(
                weapon.displayName,
                $"{weapon.magazineSize} balas · {weapon.fireRate:0.#} disp/s · daño {weapon.baseDamage:0.##} · {mode}",
                unlocked ? "TUYA" : $"{weapon.price} CR.",
                !unlocked && credits >= weapon.price,
                () => profileManager.BuyWeapon(weapon));
        }
    }
}
