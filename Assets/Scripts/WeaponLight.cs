using UnityEngine;
using UnityEngine.Rendering.Universal;

// Luz del arma (I4.6). El arma está a medio metro de la cámara, fuera del alcance de los focos del
// escenario, y en la noche se veía casi negra. Se ilumina con una luz pegada a la cámara que SOLO afecta
// al arma, gracias a las Rendering Layers de URP (antes "Light Layers"):
//   - La luz tiene como máscara únicamente la Rendering Layer "Weapon".
//   - Los renderers del arma se añaden a esa Rendering Layer (sin quitarles "Default", así que los demás
//     focos los siguen iluminando como antes).
// Resultado: el arma se lee bien y la escena no recibe un foco extra que delataría el truco.
//
// La Rendering Layer NO es la capa (Layer) del GameObject: esa sirve para raycasts y cámaras; esta solo
// para decidir qué luces afectan a qué renderers. Por eso este script la asigna él mismo al arma
// equipada, sin depender de cómo estén configurados los prefabs de arma.
[RequireComponent(typeof(Light))]
public class WeaponLight : MonoBehaviour
{
    [SerializeField] private string renderingLayerName = "Weapon";

    private Light weaponLight;
    private WeaponController weaponController;
    private Transform weaponHolder;
    private uint layerBit;

    private void Awake()
    {
        weaponLight = GetComponent<Light>();
        weaponController = FindFirstObjectByType<WeaponController>();

        WeaponVisual visual = FindFirstObjectByType<WeaponVisual>();   // está en WeaponHolder
        weaponHolder = visual != null ? visual.transform : null;

        int index = RenderingLayerMask.NameToRenderingLayer(renderingLayerName);
        if (index < 0)
        {
            Debug.LogWarning($"WeaponLight: no existe la Rendering Layer '{renderingLayerName}' (Tags and Layers).", this);
            enabled = false;
            return;
        }

        layerBit = 1u << index;

        // La luz solo afecta a esa Rendering Layer
        weaponLight.renderingLayerMask = (int)layerBit;
        UniversalAdditionalLightData data = weaponLight.GetUniversalAdditionalLightData();
        if (data != null) data.renderingLayers = layerBit;
    }

    private void OnEnable()
    {
        if (weaponController != null) weaponController.OnWeaponEquipped += HandleWeaponEquipped;
    }

    private void OnDisable()
    {
        if (weaponController != null) weaponController.OnWeaponEquipped -= HandleWeaponEquipped;
    }

    // Por si el arma inicial se equipó antes de que este script se suscribiera
    private void Start() => TagWeaponRenderers();

    private void HandleWeaponEquipped(WeaponData data) => TagWeaponRenderers();

    private void TagWeaponRenderers()
    {
        if (weaponHolder == null)
            return;

        // Incluye renderers desactivados (p. ej. la mira, que se activa al apuntar)
        foreach (Renderer rend in weaponHolder.GetComponentsInChildren<Renderer>(true))
            rend.renderingLayerMask |= layerBit;
    }
}
