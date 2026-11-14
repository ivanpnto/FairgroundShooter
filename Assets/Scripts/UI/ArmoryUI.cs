using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Interfaz de la armería (Fase 8). Lo común (fundido, créditos, VOLVER, botones ARMERÍA) está en TentShopUI;
// aquí, la ficha del arma: estadísticas con las mejoras del perfil y el botón COMPRAR / EQUIPAR.
public class ArmoryUI : TentShopUI
{
    [Header("Ficha")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button actionButton;
    [SerializeField] private TMP_Text actionText;

    private ArmoryController Armory => shop as ArmoryController;

    protected override void Awake()
    {
        base.Awake();
        if (actionButton != null) actionButton.onClick.AddListener(HandleAction);
    }

    protected override void FillCard(ShopSlot slot, int credits)
    {
        var armorySlot = slot as ArmorySlot;
        if (armorySlot == null || Armory == null) return;
        WeaponData w = armorySlot.Weapon;
        nameText.text = w.displayName.ToUpper();

        // Con las mejoras del perfil ya aplicadas: lo que el jugador tendría de verdad en la mano
        WeaponStats s = profiles != null
            ? WeaponStatsCalculator.Compute(w, profiles.Current, profiles.Upgrades)
            : WeaponStatsCalculator.Compute(w, null, null);
        float rate = s.TimeBetweenShots > 0f ? 1f / s.TimeBetweenShots : 0f;
        statsText.text =
            $"Daño  <b>{s.Damage:0.##}</b>\n" +
            $"Cargador  <b>{s.MagazineSize}</b>\n" +
            $"Cadencia  <b>{rate:0.#} disp/s</b>\n" +
            $"Recarga  <b>{s.ReloadTime:0.#} s</b>\n" +
            $"Modo  <b>{(w.isAutomatic ? "automática" : "semiautomática")}</b>\n" +
            "<size=75%><i>con tus mejoras</i></size>";

        switch (Armory.StateOf(w))
        {
            case ArmorySlot.SlotState.Locked:
                bool canPay = credits >= w.price;
                statusText.text = canPay ? $"{w.price} CRÉDITOS" : $"TE FALTAN {w.price - credits}";
                actionText.text = "COMPRAR";
                actionButton.interactable = canPay;
                break;
            case ArmorySlot.SlotState.Owned:
                statusText.text = "ES TUYA";
                actionText.text = "EQUIPAR";
                actionButton.interactable = true;
                break;
            default:
                statusText.text = "LA LLEVAS EQUIPADA";
                actionText.text = "EQUIPADA";
                actionButton.interactable = false;
                break;
        }
    }

    private void HandleAction()
    {
        var slot = selected as ArmorySlot;
        if (slot == null || Armory == null) return;
        if (Armory.StateOf(slot.Weapon) == ArmorySlot.SlotState.Locked) Armory.Buy(slot);
        else Armory.Equip(slot);
        Refresh();
    }
}
