using System.Collections;
using UnityEngine;

// Botica en 3D (Fase 8): la trastienda del puesto azul, donde se compran las habilidades.
//
// Sustituye al panel 2D de la tienda: MenuController.ShowShop abre esta carpa si está en la escena. El viaje, el
// interior aislado y el señalar con el ratón son de TentShopController (los mismos que en la armería); aquí
// queda lo propio: un frasco por mejora y el billete del nivel 4, comprar por niveles con
// ProfileManager.BuyUpgradeLevel (mismas reglas y precios que antes) y la reacción a la compra.
public class BoticaController : TentShopController
{
    [SerializeField] private BoticaSlot[] slots;
    [SerializeField] private ArmoryProps props;
    [SerializeField] private AudioClip glugClip;     // el tónico cae en el frasco
    [SerializeField] private AudioClip corkClip;     // pop del corcho al terminar
    [SerializeField] private AudioClip ticketClip;   // destello del billete dorado

    private void OnEnable()
    {
        if (profiles == null) profiles = SceneCache<ProfileManager>.Get();
        if (profiles != null) profiles.ProfileChanged += RefreshSlots;
    }

    private void OnDisable()
    {
        if (profiles != null) profiles.ProfileChanged -= RefreshSlots;
    }

    public int LevelOf(BoticaSlot slot) =>
        slot != null && slot.Upgrade != null && profiles != null ? profiles.GetUpgradeLevel(slot.Upgrade.id) : 0;

    // Coste del siguiente nivel; −1 si ya está al máximo
    public int CostOf(BoticaSlot slot) => slot != null && slot.Upgrade != null ? slot.Upgrade.NextLevelCost(LevelOf(slot)) : -1;

    protected override string LabelFor(ShopSlot shopSlot)
    {
        var slot = (BoticaSlot)shopSlot;
        UpgradeData u = slot.Upgrade;
        if (u == null) return "";
        int cost = CostOf(slot);
        string state;
        if (slot.IsTicket) state = cost < 0 ? "¡TU ENTRADA!" : $"{cost} CR.";
        else if (cost < 0) state = "AL MÁXIMO";
        else state = u.maxLevel > 1 ? $"NIVEL {LevelOf(slot)}/{u.maxLevel} · {cost} CR." : $"{cost} CR.";
        return $"{u.displayName}\n<size=70%>{state}</size>";
    }

    public bool Buy(BoticaSlot slot)
    {
        if (slot == null || profiles == null) return false;
        int before = LevelOf(slot);
        if (!profiles.BuyUpgradeLevel(slot.Upgrade)) return false;

        // ProfileChanged ya ha repintado sin animación; se vuelve al nivel de antes y se anima la subida
        slot.SetLevel(before, false);
        slot.SetLevel(before + 1, true);
        if (props != null) props.PlaySale();
        if (audioSource != null)
        {
            if (slot.IsTicket)
            {
                if (ticketClip != null) audioSource.PlayOneShot(ticketClip);
            }
            else
            {
                if (glugClip != null) audioSource.PlayOneShot(glugClip);
                if (corkClip != null) StartCoroutine(PlayLater(corkClip, slot.FillTime));
            }
        }
        RefreshHoverLabel();
        return true;
    }

    private IEnumerator PlayLater(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (audioSource != null) audioSource.PlayOneShot(clip);
    }

    protected override void RefreshSlots()
    {
        if (slots != null)
            foreach (BoticaSlot s in slots)
                if (s != null && s.Upgrade != null) s.SetLevel(LevelOf(s), false);
        if (ui != null) ui.Refresh();
    }
}
