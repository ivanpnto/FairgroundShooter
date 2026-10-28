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

    [Header("Referencias")]
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private WeaponAmmo weaponAmmo;
    [SerializeField] private WeaponVisual weaponVisual;

    public WeaponData CurrentWeapon { get; private set; }

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

#if UNITY_EDITOR
    // Solo en el editor: reaplicar la colocación cada frame permite ajustar
    // holdPosition/holdRotation/holdScale en el Inspector durante el Play y ver el
    // resultado al momento. Lo que cambies ahí se guarda en el asset, que es justo
    // lo que se quiere al calibrar cada arma.
    private void Update()
    {
        if (CurrentWeapon != null && currentModel != null)
            ApplyHoldTransform(CurrentWeapon);
    }
#endif

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
        currentModel = Instantiate(data.modelPrefab, weaponHolder);
        ApplyHoldTransform(data);

        weaponAmmo.Configure(data.magazineSize, data.reloadTime);

        WeaponModel model = currentModel.GetComponent<WeaponModel>();

        // Sin WeaponModel el arma funciona igual, solo que sin cargador ni corredera animados
        if (model == null)
            Debug.LogWarning($"WeaponController: '{data.modelPrefab.name}' no tiene componente WeaponModel", this);

        weaponVisual.SetModel(model, data);

        // Al final, cuando munición y visual ya están configurados para el arma nueva
        OnWeaponEquipped?.Invoke(data);
    }

    private void ApplyHoldTransform(WeaponData data)
    {
        currentModel.transform.localPosition = data.holdPosition;
        currentModel.transform.localRotation = Quaternion.Euler(data.holdRotation);
        currentModel.transform.localScale = data.holdScale;
    }
}
