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

    [Header("Arma de apoyo (Q / rueda)")]
    [Tooltip("Pistola a la que se cambia con Q o la rueda. Solo si el arma principal tiene visor telescópico (la Barrett)")]
    [SerializeField] private WeaponData sidearm;
    [Tooltip("Duración total del cambio: la mitad bajando el arma y la otra mitad subiendo la nueva. No se dispara mientras dura")]
    [SerializeField, Min(0.05f)] private float switchDuration = 0.35f;

    [Header("Referencias")]
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private WeaponAmmo weaponAmmo;
    [SerializeField] private WeaponVisual weaponVisual;

    public WeaponData CurrentWeapon { get; private set; }

    // El arma elegida para la partida (selector o armería). CurrentWeapon es la que está en la mano: la misma, o la
    // pistola de apoyo si se ha cambiado con Q / rueda. Fuera de la partida siempre se vuelve a la principal
    public WeaponData PrimaryWeapon { get; private set; }
    public WeaponData Sidearm => sidearm;
    public bool HasSidearm => SidearmAllowed(PrimaryWeapon, sidearm);
    public bool IsHoldingSidearm => CurrentWeapon != null && CurrentWeapon != PrimaryWeapon;

    // Regla del cambio, aparte para poder probarla sin escena: solo un arma de visor telescópico (lenta, para
    // lejos) lleva pistola de apoyo; con cualquier otra el cambio no aporta nada
    public static bool SidearmAllowed(WeaponData primary, WeaponData sidearm) =>
        primary != null && sidearm != null && primary != sidearm && primary.scopeType == ScopeType.Telescopic;

    // Cambio en curso: no se dispara, ni se recarga, ni se apunta
    public bool IsSwitching => switchElapsed >= 0f;
    // 0 → 1 → 0 durante el cambio: cuánto ha bajado el arma (lo lee WeaponVisual)
    public float SwitchLowerAmount => IsSwitching ? SwitchCurve(switchElapsed / switchDuration) : 0f;

    // Triángulo suavizado: baja la primera mitad, sube la segunda. El modelo se cambia en el punto más bajo
    public static float SwitchCurve(float t)
    {
        t = Mathf.Clamp01(t);
        float tri = t < 0.5f ? t * 2f : (1f - t) * 2f;
        return Mathf.SmoothStep(0f, 1f, tri);
    }

    // Estadísticas del arma principal, aunque se lleve la pistola en la mano (escalado del boss)
    public WeaponStats PrimaryStats => PrimaryWeapon == CurrentWeapon ? CurrentStats : ComputeStats(PrimaryWeapon);

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
    private GameManager gameManager;

    // Balas que le quedan al arma que NO está en la mano (−1 = cargador lleno). Con solo dos armas (principal y
    // apoyo) basta un número: así cambiar dos veces no es una recarga gratis
    private int otherWeaponAmmo = -1;
    private float switchElapsed = -1f;
    private bool swappedThisSwitch;

    // Guardamos el modelo que hemos creado nosotros en lugar de vaciar WeaponHolder:
    // así no destruimos otros hijos que se le añadan en el futuro (fogonazo, mira...)
    private GameObject currentModel;

    private void Awake()
    {
        profileManager = FindFirstObjectByType<ProfileManager>();
        gameManager = FindFirstObjectByType<GameManager>();
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
        Equip(PreferredWeapon());
    }

    // La que recuerda el perfil si sigue desbloqueada; si no, la inicial o la primera que tenga
    private WeaponData PreferredWeapon()
    {
        string saved = profileManager != null ? profileManager.EquippedWeaponId : null;
        if (!string.IsNullOrEmpty(saved) && availableWeapons != null)
            foreach (WeaponData data in availableWeapons)
                if (data != null && data.id == saved && IsUnlocked(data))
                    return data;
        return IsUnlocked(startingWeapon) ? startingWeapon : FirstUnlocked();
    }

    // Cuántas armas puede elegir el perfil en el selector (con una sola, no se pregunta)
    public int UnlockedCount
    {
        get
        {
            int count = 0;
            if (availableWeapons != null)
                foreach (WeaponData data in availableWeapons)
                    if (IsUnlocked(data)) count++;
            return count;
        }
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

        // Al cambiar de perfil se equipa la que recuerda el nuevo (o una desbloqueada si la suya ya no lo está).
        // Con el mismo perfil, PreferredWeapon es la principal de ahora, porque Equip la guarda en el perfil
        WeaponData preferred = PreferredWeapon();
        if (preferred != PrimaryWeapon)
        {
            Equip(preferred);
            return;
        }

        // La tienda solo está fuera de la partida, y ahí siempre se lleva la principal
        if (IsHoldingSidearm)
            EquipModel(PrimaryWeapon, -1);

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

    // Equipa el arma PRINCIPAL (selector al jugar, armería). Llena los dos cargadores y cancela un cambio en curso
    public void Equip(WeaponData data)
    {
        if (data == null || data.modelPrefab == null)
        {
            Debug.LogWarning("WeaponController: el arma no tiene datos o modelPrefab asignado", this);
            return;
        }

        PrimaryWeapon = data;
        otherWeaponAmmo = -1;
        switchElapsed = -1f;
        EquipModel(data, -1);

        // Se recuerda en el perfil (sin perfil elegido todavía no hay dónde: lo pondrá HandleProfileChanged)
        if (profileManager != null) profileManager.SetEquippedWeapon(data.id);
    }

    // Q / rueda en partida. Devuelve false si ahora no se puede (sin pistola de apoyo o ya cambiando)
    public bool TrySwitchWeapon()
    {
        if (!HasSidearm || IsSwitching || CurrentWeapon == null)
            return false;

        switchElapsed = 0f;
        swappedThisSwitch = false;
        return true;
    }

    // Al empezar cada nivel: los dos cargadores llenos (el contorno rojo, en cambio, solo llena el de la mano)
    public void RefillAll()
    {
        otherWeaponAmmo = -1;
        weaponAmmo.ResetAmmo();
    }

    private void Update()
    {
        bool inRun = gameManager == null || gameManager.IsInRun;

        // Fuera de la partida se vuelve a la principal: menús, armería y selector la muestran a ella
        if (!inRun)
        {
            switchElapsed = -1f;
            if (IsHoldingSidearm)
                EquipModel(PrimaryWeapon, -1);
            otherWeaponAmmo = -1;
            return;
        }

        // El cambio solo avanza jugando: en pausa o entre niveles se queda congelado
        if (!IsSwitching || gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        switchElapsed += Time.deltaTime;

        if (!swappedThisSwitch && switchElapsed >= switchDuration * 0.5f)
        {
            swappedThisSwitch = true;
            WeaponData next = IsHoldingSidearm ? PrimaryWeapon : sidearm;
            // Las balas del arma que se guarda se apuntan; una recarga a medias se pierde (se cancela)
            int restore = otherWeaponAmmo;
            otherWeaponAmmo = weaponAmmo.CurrentAmmo;
            EquipModel(next, restore);
            GameLog.Info($"Cambio de arma: {next.displayName}");
        }

        if (switchElapsed >= switchDuration)
            switchElapsed = -1f;
    }

    // Pone el modelo y los datos de un arma en la mano. startAmmo −1 = cargador lleno
    private void EquipModel(WeaponData data, int startAmmo)
    {
        if (currentModel != null)
            Destroy(currentModel);

        CurrentWeapon = data;
        // La pose (hold* y apuntado) la escribe WeaponVisual en SetModel y en cada frame
        currentModel = Instantiate(data.modelPrefab, weaponHolder);

        CurrentStats = ComputeStats(data);
        weaponAmmo.Configure(CurrentStats.MagazineSize, CurrentStats.ReloadTime, startAmmo);

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
