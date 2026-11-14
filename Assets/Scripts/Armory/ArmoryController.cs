using UnityEngine;

// Armería en 3D (Fase 8): la carpa a rayas donde se compran y se equipan las armas.
//
// El viaje, el interior aislado, el paralaje y el señalar con el ratón son comunes con la botica y están en
// TentShopController. Aquí queda lo propio: cinco huecos con su arma, comprar (ProfileManager) y equipar
// (WeaponController), y el atrezo que celebra la venta.
public class ArmoryController : TentShopController
{
    [SerializeField] private ArmorySlot[] slots;
    [SerializeField] private ArmoryProps props;
    [SerializeField] private AudioClip bulbClip;

    private WeaponController weapons;

    protected override void Awake()
    {
        base.Awake();
        weapons = SceneCache<WeaponController>.Get();
    }

    private void OnEnable()
    {
        if (profiles == null) profiles = SceneCache<ProfileManager>.Get();
        if (weapons == null) weapons = SceneCache<WeaponController>.Get();
        if (profiles != null) profiles.ProfileChanged += RefreshSlots;
        if (weapons != null) weapons.OnWeaponEquipped += HandleEquipped;
    }

    private void OnDisable()
    {
        if (profiles != null) profiles.ProfileChanged -= RefreshSlots;
        if (weapons != null) weapons.OnWeaponEquipped -= HandleEquipped;
    }

    protected override string LabelFor(ShopSlot shopSlot)
    {
        var slot = (ArmorySlot)shopSlot;
        WeaponData w = slot.Weapon;
        string price = slot.State == ArmorySlot.SlotState.Locked ? $"{w.price} CR." : slot.State == ArmorySlot.SlotState.Owned ? "TUYA" : "EQUIPADA";
        return $"{w.displayName}\n<size=70%>{price}</size>";
    }

    // ── Compra y equipar (los botones de la ficha) ─────────────────────────────

    public bool Buy(ArmorySlot slot)
    {
        if (slot == null || profiles == null || !profiles.BuyWeapon(slot.Weapon)) return false;
        // ProfileChanged ya ha repintado sin animación; se repite el encendido con parpadeo
        slot.SetState(ArmorySlot.SlotState.Locked, false);
        slot.SetState(StateOf(slot.Weapon), true);
        if (props != null) props.PlaySale();
        if (audioSource != null && bulbClip != null) audioSource.PlayOneShot(bulbClip);
        RefreshHoverLabel();
        return true;
    }

    public void Equip(ArmorySlot slot)
    {
        if (slot == null || weapons == null || !weapons.IsUnlocked(slot.Weapon)) return;
        weapons.Equip(slot.Weapon);   // OnWeaponEquipped repinta las bombillas
        if (audioSource != null && bulbClip != null) audioSource.PlayOneShot(bulbClip);
        RefreshHoverLabel();
    }

    private void HandleEquipped(WeaponData _) => RefreshSlots();

    public ArmorySlot.SlotState StateOf(WeaponData w)
    {
        if (weapons == null || !weapons.IsUnlocked(w)) return ArmorySlot.SlotState.Locked;
        return weapons.CurrentWeapon == w ? ArmorySlot.SlotState.Equipped : ArmorySlot.SlotState.Owned;
    }

    protected override void RefreshSlots()
    {
        if (slots == null) return;
        foreach (ArmorySlot s in slots)
            if (s != null && s.Weapon != null) s.SetState(StateOf(s.Weapon), false);
        if (ui != null) ui.Refresh();
    }
}
