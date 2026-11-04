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

    // Estadísticas EFECTIVAS del arma equipada (WeaponData + mejoras del perfil). Munición, cadencia,
    // alcance y daño se leen de aquí; WeaponData queda para lo visual, el sonido y el modo de disparo
    public WeaponStats CurrentStats { get; private set; }

    // El modelo instanciado del arma actual (null si su prefab no tiene WeaponModel).
    // Lo lee WeaponEffects para colocar el fogonazo en su muzzle
    public WeaponModel CurrentModel { get; private set; }

    // De solo lectura: quien la use puede recorrerla pero no cambiarla
    public IReadOnlyList<WeaponData> AvailableWeapons => availableWeapons;

    // Avisa de que hay un arma nueva lista para usar. Con un evento, WeaponController no
    // necesita conocer a quien reacciona (PlayerShoot reinicia su cadencia, y en el futuro
    // podrían suscribirse el HUD o el sonido)
    public event System.Action<WeaponData> OnWeaponEquipped;

    // Las estadísticas han cambiado sin cambiar de arma (se ha comprado una mejora o se ha cambiado de
    // perfil). Al equipar también se recalculan, pero eso ya lo avisa OnWeaponEquipped
    public event System.Action<WeaponStats> StatsChanged;

    private ProfileManager profileManager;

    // Guardamos el modelo que hemos creado nosotros en lugar de vaciar WeaponHolder:
    // así no destruimos otros hijos que se le añadan en el futuro (fogonazo, mira...)
    private GameObject currentModel;

    private void Awake()
    {
        profileManager = FindFirstObjectByType<ProfileManager>();
    }

    private void OnEnable()
    {
        if (profileManager != null) profileManager.ProfileChanged += HandleProfileChanged;
    }

    private void OnDisable()
    {
        if (profileManager != null) profileManager.ProfileChanged -= HandleProfileChanged;
    }

    private void Start()
    {
        Equip(IsUnlocked(startingWeapon) ? startingWeapon : FirstUnlocked());
    }

    // Sin ProfileManager en la escena todo está desbloqueado, como antes de la Fase 5
    public bool IsUnlocked(WeaponData data)
    {
        if (data == null)
            return false;
        return profileManager == null || profileManager.IsWeaponUnlocked(data.id);
    }

    private WeaponData FirstUnlocked()
    {
        foreach (WeaponData data in availableWeapons)
            if (IsUnlocked(data))
                return data;
        return startingWeapon;
    }

    private WeaponStats ComputeStats(WeaponData data) => profileManager != null
        ? WeaponStatsCalculator.Compute(data, profileManager.Current, profileManager.Upgrades)
        : WeaponStatsCalculator.Compute(data, null, null);

    private void HandleProfileChanged()
    {
        if (CurrentWeapon == null)
            return;

        // Al cambiar de perfil el arma equipada puede no estar desbloqueada en el nuevo
        if (!IsUnlocked(CurrentWeapon))
        {
            Equip(FirstUnlocked());
            return;
        }

        WeaponStats stats = ComputeStats(CurrentWeapon);
        if (stats.Equals(CurrentStats))
            return;   // p. ej. solo han cambiado los créditos

        CurrentStats = stats;

        // La tienda solo está fuera de la partida: reconfigurar llena el cargador, que es lo esperado
        // tras comprar munición. Si cambia el tamaño en mitad de la partida no regala balas extra… salvo
        // con el menú de depuración, que es justo para eso
        weaponAmmo.Configure(stats.MagazineSize, stats.ReloadTime);
        StatsChanged?.Invoke(stats);
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

        CurrentStats = ComputeStats(data);
        weaponAmmo.Configure(CurrentStats.MagazineSize, CurrentStats.ReloadTime);

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
