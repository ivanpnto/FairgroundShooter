using TMPro;
using UnityEngine;

// Recordatorio del cambio de arma en el HUD (25/11), encima de las balas: «Q · PISTOLA» con la Barrett en la mano y
// «Q · BARRETT» con la pistola. Solo se ve si el arma de la partida tiene pistola de apoyo y el HUD está a la vista.
// Va junto a ObjectiveUI en ObjectiveHUD (siempre activo) por el mismo motivo: el HUD se apaga entre niveles.
public class SidearmHintUI : MonoBehaviour
{
    [SerializeField] private GameObject hud;
    [SerializeField] private TMP_Text hintText;

    private WeaponController weapons;
    private GameManager gameManager;
    private WeaponData shownFor;

    private void Awake()
    {
        weapons = FindFirstObjectByType<WeaponController>();
        gameManager = FindFirstObjectByType<GameManager>();
        if (hintText != null) hintText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (hintText == null || weapons == null) return;

        bool visible = weapons.HasSidearm && gameManager != null && gameManager.IsInRun
                       && (hud == null || hud.activeInHierarchy);
        if (hintText.gameObject.activeSelf != visible) hintText.gameObject.SetActive(visible);
        if (!visible || weapons.CurrentWeapon == shownFor) return;

        shownFor = weapons.CurrentWeapon;
        hintText.text = "Q · " + (weapons.IsHoldingSidearm ? ShortName(weapons.PrimaryWeapon) : "PISTOLA");
    }

    // «Barrett M82A1» → «BARRETT»: el nombre entero no cabe junto a las balas
    public static string ShortName(WeaponData data)
    {
        if (data == null || string.IsNullOrEmpty(data.displayName)) return "";
        return data.displayName.Trim().Split(' ')[0].ToUpperInvariant();
    }
}
