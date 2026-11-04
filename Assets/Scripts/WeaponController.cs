using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Equipa un arma a partir de su WeaponData: crea el modelo bajo WeaponHolder y
/// reparte los datos a los sistemas que los usan (munición y parte visual).
/// Va en Player. Solo LEE el WeaponData: nunca escribe en él, porque los cambios
/// a un ScriptableObject en modo Play se quedan guardados en el asset.
/// </summary>
public class WeaponController : MonoBehaviour
{
    [SerializeField] private WeaponData startingWeapon;
    // La lista vive aquí y no en la UI: es dato de juego, y así los selectores del menú
    // de inicio y del Game Over comparten una única lista en lugar de una copia cada uno
    [Tooltip("Armas que se pueden elegir en los menús, en el orden en que se recorren")]
    [SerializeField] private WeaponData[] availableWeapons;

    [Header("Referencias")]
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private WeaponAmmo weaponAmmo;
    [SerializeField] private WeaponVisual weaponVisual;

    public WeaponData CurrentWeapon { get; private set; }

    // El modelo instanciado del arma actual (null si su prefab no tiene WeaponModel).
    // Lo lee WeaponEffects para colocar el fogonazo en su muzzle
    public WeaponModel CurrentModel { get; private set; }

    // De solo lectura: quien la use puede recorrerla pero no cambiarla
    public IReadOnlyList<WeaponData> AvailableWeapons => availableWeapons;

    // Avisa de que hay un arma nueva lista para usar. Con un evento, WeaponController no
    // necesita conocer a quien reacciona (PlayerShoot reinicia su cadencia, y en el futuro
    // podrían suscribirse el HUD o el sonido)
    public event System.Action<WeaponData> OnWeaponEquipped;

    // Guardamos el modelo que hemos creado nosotros en lugar de vaciar WeaponHolder:
    // así no destruimos otros hijos que se le añadan en el futuro (fogonazo, mira...)
    private GameObject currentModel;

    private void Start()
    {
        Equip(startingWeapon);
    }

    public void Equip(WeaponData data)
    {
        if (data == null || data.modelPrefab == null)
        {
            Debug.LogWarning("WeaponController: el arma no tiene datos o modelPrefab asignado", this);
            return;
        }

        if (currentModel != null)
            Destroy(currentModel);

        CurrentWeapon = data;
        // La pose (hold* y apuntado) la escribe WeaponVisual en SetModel y en cada frame
        currentModel = Instantiate(data.modelPrefab, weaponHolder);

        weaponAmmo.Configure(data.magazineSize, data.reloadTime);

        WeaponModel model = currentModel.GetComponent<WeaponModel>();
        CurrentModel = model;

        // Sin WeaponModel el arma funciona igual, solo que sin cargador ni corredera animados
        if (model == null)
            Debug.LogWarning($"WeaponController: '{data.modelPrefab.name}' no tiene componente WeaponModel", this);

        weaponVisual.SetModel(currentModel.transform, model, data);

        // Al final, cuando munición y visual ya están configurados para el arma nueva
        OnWeaponEquipped?.Invoke(data);
    }
}
