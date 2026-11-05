using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tienda: se genera a partir de las mejoras de ProfileManager y de las armas de WeaponController,
// así que una mejora o un arma nueva aparece sin tocar la UI. Comprar lo hace ProfileManager; la
// tienda se repinta entera con ProfileChanged (créditos, niveles y botones siempre coherentes).
public class ShopPanelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private Transform upgradesContent;
    [SerializeField] private Transform weaponsContent;
    [Tooltip("Fila de ejemplo (desactivada) que se clona por cada mejora y cada arma")]
    [SerializeField] private ShopRowUI rowTemplate;
    [SerializeField] private Button backButton;

    private ProfileManager profileManager;
    private WeaponController weaponController;
    private MenuController menu;
    private readonly List<GameObject> rows = new List<GameObject>();

    private void Awake()
    {
        profileManager = FindFirstObjectByType<ProfileManager>();
        weaponController = FindFirstObjectByType<WeaponController>();
        menu = FindFirstObjectByType<MenuController>();

        rowTemplate.gameObject.SetActive(false);
        backButton.onClick.AddListener(() => { if (menu != null) menu.Back(); });
    }

    private void OnEnable()
    {
        if (profileManager != null) profileManager.ProfileChanged += Rebuild;
        Rebuild();
    }

    private void OnDisable()
    {
        if (profileManager != null) profileManager.ProfileChanged -= Rebuild;
    }

    private void Rebuild()
    {
        foreach (GameObject row in rows)
            Destroy(row);
        rows.Clear();

        if (profileManager == null)
            return;

        int credits = profileManager.Credits;
        creditsText.text = $"TUS CRÉDITOS: {credits}";

        if (profileManager.Upgrades != null)
            foreach (UpgradeData upgrade in profileManager.Upgrades)
                if (upgrade != null)
                    AddUpgradeRow(upgrade, credits);

        if (weaponController != null && weaponController.AvailableWeapons != null)
            foreach (WeaponData weapon in weaponController.AvailableWeapons)
                if (weapon != null)
                    AddWeaponRow(weapon, credits);
    }

    private void AddUpgradeRow(UpgradeData upgrade, int credits)
    {
        int level = profileManager.GetUpgradeLevel(upgrade.id);
        int cost = upgrade.NextLevelCost(level);
        bool maxed = cost < 0;

        ShopRowUI row = NewRow(upgradesContent);
        row.Setup(
            $"{upgrade.displayName}   {level}/{upgrade.maxLevel}",
            upgrade.description,
            maxed ? "MÁXIMO" : $"{cost} CR.",
            !maxed && credits >= cost,
            () => profileManager.BuyUpgradeLevel(upgrade));
    }

    private void AddWeaponRow(WeaponData weapon, int credits)
    {
        bool unlocked = profileManager.IsWeaponUnlocked(weapon.id);
        string mode = weapon.isAutomatic ? "automática" : "semiautomática";

        ShopRowUI row = NewRow(weaponsContent);
        row.Setup(
            weapon.displayName,
            $"{weapon.magazineSize} balas · {weapon.fireRate:0.#} disp/s · daño {weapon.baseDamage:0.##} · {mode}",
            unlocked ? "TUYA" : $"{weapon.price} CR.",
            !unlocked && credits >= weapon.price,
            () => profileManager.BuyWeapon(weapon));
    }

    private ShopRowUI NewRow(Transform parent)
    {
        ShopRowUI row = Instantiate(rowTemplate, parent);
        row.gameObject.SetActive(true);
        rows.Add(row.gameObject);
        return row;
    }
}
