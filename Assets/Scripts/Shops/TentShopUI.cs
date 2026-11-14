using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Base de la interfaz de las tiendas en 3D (Fase 8): fundido a negro, créditos, ayuda, VOLVER y la ficha del
// objeto elegido. Solo pinta y reenvía: comprar lo hace el controlador. Qué pone la ficha lo decide la hija.
public abstract class TentShopUI : MonoBehaviour
{
    [FormerlySerializedAs("armory")]
    [SerializeField] protected TentShopController shop;
    [SerializeField] private CanvasGroup fade;          // negro a pantalla completa
    [SerializeField] private GameObject interiorRoot;   // todo lo que se ve dentro de la carpa
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private Button backButton;
    [Tooltip("Botones del menú que abren esta tienda (pueden quedar vacíos si la abre MenuController)")]
    [SerializeField] private Button[] openButtons;
    [SerializeField] protected GameObject card;

    protected ProfileManager profiles;
    protected ShopSlot selected;

    public bool HasSelection => selected != null;

    protected virtual void Awake()
    {
        profiles = SceneCache<ProfileManager>.Get();
        if (backButton != null) backButton.onClick.AddListener(() => { if (shop != null) shop.Close(); });
        if (openButtons != null)
            foreach (Button b in openButtons)
                if (b != null) b.onClick.AddListener(() => { if (shop != null) shop.Open(); });
        SetFade(0f);
        ShowInterior(false);
    }

    public void SetFade(float alpha)
    {
        if (fade == null) return;
        fade.alpha = alpha;
        fade.blocksRaycasts = alpha > 0.01f;   // transparente no debe tapar los clics
        fade.gameObject.SetActive(alpha > 0.001f);
    }

    public void ShowInterior(bool show)
    {
        if (interiorRoot != null) interiorRoot.SetActive(show);
        if (!show) ClearSelection();
        Refresh();
    }

    public void Select(ShopSlot slot)
    {
        selected = slot;
        Refresh();
    }

    public void ClearSelection()
    {
        selected = null;
        Refresh();
    }

    public void Refresh()
    {
        int credits = profiles != null ? profiles.Credits : 0;
        if (creditsText != null) creditsText.text = $"TUS CRÉDITOS: {credits}";
        if (hintText != null) hintText.gameObject.SetActive(selected == null);
        if (card != null) card.SetActive(selected != null);
        if (selected != null && shop != null) FillCard(selected, credits);
    }

    protected abstract void FillCard(ShopSlot slot, int credits);
}
