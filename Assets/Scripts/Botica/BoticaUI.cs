using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Interfaz de la botica (Fase 8). Lo común (fundido, créditos, VOLVER) está en TentShopUI; aquí, la ficha del
// tónico: qué hace, en qué nivel está, cuánto cuesta el siguiente y el botón COMPRAR.
public class BoticaUI : TentShopUI
{
    [Header("Ficha")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button actionButton;
    [SerializeField] private TMP_Text actionText;

    private BoticaController Botica => shop as BoticaController;

    protected override void Awake()
    {
        base.Awake();
        if (actionButton != null) actionButton.onClick.AddListener(HandleAction);
    }

    protected override void FillCard(ShopSlot shopSlot, int credits)
    {
        var slot = shopSlot as BoticaSlot;
        if (slot == null || slot.Upgrade == null || Botica == null) return;
        UpgradeData u = slot.Upgrade;
        int level = Botica.LevelOf(slot);
        int cost = Botica.CostOf(slot);

        nameText.text = u.displayName.ToUpper();
        descriptionText.text = u.description;
        levelText.text = slot.IsTicket || u.maxLevel <= 1 ? "" : $"NIVEL {level} DE {u.maxLevel}";

        if (cost < 0)
        {
            statusText.text = slot.IsTicket ? "YA TIENES TU ENTRADA" : "AL MÁXIMO";
            actionText.text = slot.IsTicket ? "TUYA" : "MÁXIMO";
            actionButton.interactable = false;
            return;
        }
        bool canPay = credits >= cost;
        statusText.text = canPay ? $"{cost} CRÉDITOS" : $"TE FALTAN {cost - credits}";
        actionText.text = "COMPRAR";
        actionButton.interactable = canPay;
    }

    private void HandleAction()
    {
        var slot = selected as BoticaSlot;
        if (slot == null || Botica == null) return;
        Botica.Buy(slot);
        Refresh();
    }
}
