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
    [Tooltip("Bloque 2: icono a la izquierda. Sin icono (las armas) se oculta y los textos ocupan su sitio")]
    [SerializeField] private Image iconImage;
    [SerializeField] private float iconSpace = 84f;

    // Posición X y ancho de los textos en la plantilla (sin icono); con icono se desplazan iconSpace a la derecha
    // y se estrechan lo mismo, para no meterse debajo del botón de compra
    private float titleX, infoX, titleW, infoW;
    private bool positionsSaved;

    public void Setup(string title, string info, string buyLabel, bool canBuy, System.Action onBuy, Sprite icon = null)
    {
        SetIcon(icon);
        titleText.text = title;
        infoText.text = info;
        buyText.text = buyLabel;

        // Desactivado (y no oculto) para que se vea el precio aunque aún no llegue
        buyButton.interactable = canBuy;
        buyButton.onClick.RemoveAllListeners();
        if (canBuy && onBuy != null)
            buyButton.onClick.AddListener(() => onBuy());
    }

    private void SetIcon(Sprite icon)
    {
        if (iconImage == null)
            return;

        if (!positionsSaved)
        {
            titleX = titleText.rectTransform.anchoredPosition.x;
            infoX = infoText.rectTransform.anchoredPosition.x;
            titleW = titleText.rectTransform.sizeDelta.x;
            infoW = infoText.rectTransform.sizeDelta.x;
            positionsSaved = true;
        }

        bool hasIcon = icon != null;
        iconImage.gameObject.SetActive(hasIcon);
        iconImage.sprite = icon;
        float offset = hasIcon ? iconSpace : 0f;
        Vector2 t = titleText.rectTransform.anchoredPosition; t.x = titleX + offset; titleText.rectTransform.anchoredPosition = t;
        Vector2 i = infoText.rectTransform.anchoredPosition; i.x = infoX + offset; infoText.rectTransform.anchoredPosition = i;
        titleText.rectTransform.sizeDelta = new Vector2(titleW - offset, titleText.rectTransform.sizeDelta.y);
        infoText.rectTransform.sizeDelta = new Vector2(infoW - offset, infoText.rectTransform.sizeDelta.y);
    }
}
