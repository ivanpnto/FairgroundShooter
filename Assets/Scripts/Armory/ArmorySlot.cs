using TMPro;
using UnityEngine;

// Un hueco de la pared de la armería (Fase 8): un arma colgada, su bombilla y su placa de latón.
//
// Tres estados, que se leen sin texto: bombilla apagada = no la tienes; encendida y cálida = tuya;
// dorada y más brillante = la que llevas. La placa lo dice además con palabras (precio, TUYA, EQUIPADA).
// El paso del ratón (levantar y girar el arma) lo hace la base, ShopSlot.
//
// Solo pinta. Qué arma está desbloqueada o equipada lo deciden ProfileManager y WeaponController;
// ArmoryController llama a SetState cuando algo cambia.
public class ArmorySlot : ShopSlot
{
    public enum SlotState { Locked, Owned, Equipped }

    [SerializeField] private WeaponData weapon;
    [SerializeField] private Renderer bulb;
    [SerializeField] private Light bulbLight;
    [SerializeField] private TMP_Text plate;

    [Header("Bombilla")]
    [SerializeField] private Material bulbOff;
    [SerializeField] private Material bulbOn;
    [SerializeField] private Material bulbGold;
    [SerializeField] private Color onColor = new Color(1f, 0.78f, 0.45f);
    [SerializeField] private Color goldColor = new Color(1f, 0.82f, 0.3f);
    [SerializeField] private float onIntensity = 0.9f;
    [SerializeField] private float goldIntensity = 1.6f;

    private float flickerAge = -1f;

    public WeaponData Weapon => weapon;
    public SlotState State { get; private set; } = SlotState.Locked;

    // animate: la bombilla parpadea al encenderse (al comprar). Al abrir la armería, sin parpadeo
    public void SetState(SlotState state, bool animate)
    {
        bool turningOn = State == SlotState.Locked && state != SlotState.Locked;
        State = state;
        if (bulb != null)
            bulb.sharedMaterial = state == SlotState.Locked ? bulbOff : state == SlotState.Owned ? bulbOn : bulbGold;
        if (bulbLight != null)
        {
            bulbLight.enabled = state != SlotState.Locked;
            bulbLight.color = state == SlotState.Equipped ? goldColor : onColor;
            bulbLight.intensity = state == SlotState.Equipped ? goldIntensity : onIntensity;
        }
        if (plate != null && weapon != null)
            plate.text = state == SlotState.Locked ? $"{weapon.price} CR." : state == SlotState.Owned ? "TUYA" : "EQUIPADA";
        flickerAge = animate && turningOn ? 0f : -1f;
    }

    protected override void Update()
    {
        base.Update();

        // Encendido con parpadeo, como una bombilla vieja que tarda en coger: 0,5 s
        if (flickerAge >= 0f && bulbLight != null)
        {
            flickerAge += Time.deltaTime;
            bool on = flickerAge > 0.45f || Mathf.Repeat(flickerAge * 14f, 1f) > 0.45f;
            float target = State == SlotState.Equipped ? goldIntensity : onIntensity;
            bulbLight.intensity = on ? target : target * 0.1f;
            if (bulb != null) bulb.sharedMaterial = on ? (State == SlotState.Equipped ? bulbGold : bulbOn) : bulbOff;
            if (flickerAge > 0.5f) flickerAge = -1f;
        }
    }
}
