using TMPro;
using UnityEngine;

// Un hueco de la pared de la armería (Fase 8): un arma colgada, su bombilla y su placa de latón.
//
// Tres estados, que se leen sin texto: bombilla apagada = no la tienes; encendida y cálida = tuya;
// dorada y más brillante = la que llevas. La placa lo dice además con palabras (precio, TUYA, EQUIPADA).
// Al pasar el ratón el arma se levanta y gira un poco en su soporte.
//
// Solo pinta. Qué arma está desbloqueada o equipada lo deciden ProfileManager y WeaponController;
// ArmoryController llama a SetState cuando algo cambia.
public class ArmorySlot : MonoBehaviour
{
    public enum SlotState { Locked, Owned, Equipped }

    [SerializeField] private WeaponData weapon;
    [Tooltip("Pivote del arma: es lo que se levanta y gira al pasar el ratón")]
    [SerializeField] private Transform holder;
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

    [Header("Paso del ratón")]
    [SerializeField] private float liftHeight = 0.07f;
    [SerializeField] private float liftTurn = 10f;   // grados alrededor del eje vertical

    private Vector3 holderHome;
    private Quaternion holderRot;
    private bool hovered;
    private float lift;
    private float flickerAge = -1f;

    public WeaponData Weapon => weapon;
    public SlotState State { get; private set; } = SlotState.Locked;
    // Punto encima del arma para la etiqueta flotante
    public Vector3 LabelPoint => holder != null ? holder.position + Vector3.up * 0.4f : transform.position;

    private void Awake()
    {
        if (holder != null)
        {
            holderHome = holder.localPosition;
            holderRot = holder.localRotation;
        }
    }

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

    public void SetHovered(bool value) => hovered = value;

    private void Update()
    {
        if (holder != null)
        {
            lift = Mathf.MoveTowards(lift, hovered ? 1f : 0f, Time.deltaTime * 6f);
            float e = lift * lift * (3f - 2f * lift);   // suavizado en los dos extremos
            holder.localPosition = holderHome + Vector3.up * (liftHeight * e);
            holder.localRotation = holderRot * Quaternion.Euler(0f, liftTurn * e, 0f);
        }

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
