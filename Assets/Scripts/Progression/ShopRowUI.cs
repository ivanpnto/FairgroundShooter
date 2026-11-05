using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Una fila de la tienda (mejora o arma): título, descripción y botón de compra. Solo presentación:
// ShopPanelUI decide los textos, si se puede comprar y qué pasa al pulsar
public class ShopRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyText;

    public void Setup(string title, string info, string buyLabel, bool canBuy, System.Action onBuy)
    {
        titleText.text = title;
        infoText.text = info;
        buyText.text = buyLabel;

        // Desactivado (y no oculto) para que se vea el precio aunque aún no llegue
        buyButton.interactable = canBuy;
        buyButton.onClick.RemoveAllListeners();
        if (canBuy && onBuy != null)
            buyButton.onClick.AddListener(() => onBuy());
    }
}
