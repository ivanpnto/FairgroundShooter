using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Interfaz de la armería (Fase 8): fundido a negro, créditos, VOLVER, la ficha lateral del arma elegida y los
// botones ARMERÍA del inicio y del resumen. Solo pinta y reenvía: comprar y equipar los hace ArmoryController.
public class ArmoryUI : MonoBehaviour
{
    [SerializeField] private ArmoryController armory;
    [SerializeField] private CanvasGroup fade;          // negro a pantalla completa
    [SerializeField] private GameObject interiorRoot;   // todo lo que se ve dentro de la carpa
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private Button backButton;
    [SerializeField] private Button[] openButtons;      // ARMERÍA en el inicio y en el resumen

    [Header("Ficha")]
    [SerializeField] private GameObject card;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button actionButton;
    [SerializeField] private TMP_Text actionText;

    private ProfileManager profiles;
    private ArmorySlot selected;

    public bool HasSelection => selected != null;

    private void Awake()
    {
        profiles = SceneCache<ProfileManager>.Get();
        if (backButton != null) backButton.onClick.AddListener(() => { if (armory != null) armory.Close(); });
        if (actionButton != null) actionButton.onClick.AddListener(HandleAction);
        if (openButtons != null)
            foreach (Button b in openButtons)
                if (b != null) b.onClick.AddListener(() => { if (armory != null) armory.Open(); });
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

    public void Select(ArmorySlot slot)
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
        if (selected == null || armory == null) return;

        WeaponData w = selected.Weapon;
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

        switch (armory.StateOf(w))
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
        if (selected == null || armory == null) return;
        if (armory.StateOf(selected.Weapon) == ArmorySlot.SlotState.Locked) armory.Buy(selected);
        else armory.Equip(selected);
        Refresh();
    }
}
