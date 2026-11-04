using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Apuntado (ADS): con un arma cuyo modelo tenga Sight, el clic derecho mantenido acerca el
/// FOV de la cámara a scopeFov. Va en Player (que es la cámara).
/// Aquí solo está el estado: la pose del arma la calcula WeaponVisual a partir de ZoomAmount,
/// y el overlay negro con la retícula (solo visores, ScopeType.Telescopic) es de ScopeOverlayUI.
/// </summary>
public class PlayerScope : MonoBehaviour
{
    [Tooltip("Hijo de Player con el modelo del arma: se oculta al mirar por la mira")]
    [SerializeField] private GameObject weaponHolder;
    [Tooltip("Velocidad del zoom (mayor = más rápido). Con 20 tarda unos 0,15 s")]
    [SerializeField] private float zoomSpeed = 20f;

    // Para calibrar el Sight y eyeRelief en Play sin tener que mantener el clic derecho
    // (que además mueve la vista al soltar el ratón para ir al Inspector)
    [Header("Depuración")]
    [Tooltip("Mantiene el apuntado sin pulsar nada")]
    [SerializeField] private bool forceAim;

    // A partir de este ZoomAmount se ve el overlay y desaparece el arma (solo con visor). Al
    // final del zoom y no al principio: así se nota que el arma "sube" a la cara antes de tapar la vista
    public const float OverlayThreshold = 0.7f;

    private Camera cam;
    private GameManager gameManager;
    private WeaponController weaponController;
    private WeaponAmmo weaponAmmo;

    // El FOV de la escena (60). Se lee al empezar en lugar de fijarlo en el código
    // para que cambiarlo en la cámara no obligue a tocar este script
    private float defaultFov;

    /// <summary>
    /// 0 = sin apuntar, 1 = apuntado del todo. Lo leen WeaponVisual (pose del arma),
    /// ScopeOverlayUI y CrosshairUI. Es lo que se anima; el FOV sale de él.
    /// </summary>
    public float ZoomAmount { get; private set; }

    // El arma con la que se calculó ZoomAmount: el overlay depende de su tipo de mira
    private WeaponData zoomWeapon;

    /// <summary>Solo con visor (Telescopic): con miras abiertas el arma se ve siempre.</summary>
    public bool IsOverlayVisible =>
        zoomWeapon != null && zoomWeapon.scopeType == ScopeType.Telescopic && ZoomAmount >= OverlayThreshold;

    /// <summary>
    /// Factor para la sensibilidad del ratón. Con zoom, el mismo giro en grados mueve la
    /// imagen muchas más veces; escalar por la tangente del medio ángulo hace que un
    /// movimiento del ratón desplace la imagen en pantalla lo mismo con y sin mira.
    /// </summary>
    public float SensitivityScale =>
        Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(defaultFov * 0.5f * Mathf.Deg2Rad);

    private void Awake()
    {
        // Todos van en Player: GetComponent, como en PlayerShoot
        cam = GetComponent<Camera>();
        weaponController = GetComponent<WeaponController>();
        weaponAmmo = GetComponent<WeaponAmmo>();
        gameManager = FindFirstObjectByType<GameManager>();

        defaultFov = cam.fieldOfView;
    }

    private void Update()
    {
        bool playing = gameManager != null && gameManager.CurrentState == GameManager.GameState.Playing;

        // Fuera de la partida se quita la mira al instante, sin animar: así RestartGame no
        // necesita un Reset propio y el Game Over nunca se queda con el zoom puesto
        if (!playing)
        {
            cam.fieldOfView = defaultFov;
            ZoomAmount = 0f;
            UpdateWeaponVisibility();
            return;
        }

        WeaponData weapon = weaponController != null ? weaponController.CurrentWeapon : null;
        WeaponModel model = weaponController != null ? weaponController.CurrentModel : null;

        // Sin Sight no hay dónde poner el ojo: esa arma no apunta (p. ej. el MP5 hasta que
        // tenga su alza). Recargar saca de la mira, como en un arma real
        bool canAim = weapon != null && model != null && model.Sight != null
                      && (weaponAmmo == null || !weaponAmmo.IsReloading);
        bool pressed = forceAim || (Mouse.current != null && Mouse.current.rightButton.isPressed);
        float target = canAim && pressed ? 1f : 0f;

        // Se anima ZoomAmount y no el FOV: así no depende de que scopeFov sea distinto de 60, y
        // al cambiar a un arma que no apunta vuelve a 0 suavemente. Exponencial: independiente de los FPS
        float t = 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime);
        ZoomAmount = Mathf.Lerp(ZoomAmount, target, t);
        // Sin esto la exponencial nunca llega a 0 y WeaponVisual calcularía la pose de apuntado siempre
        if (ZoomAmount < 0.001f) ZoomAmount = 0f;
        zoomWeapon = weapon;

        float scopeFov = weapon != null ? weapon.scopeFov : defaultFov;
        cam.fieldOfView = Mathf.Lerp(defaultFov, scopeFov, ZoomAmount);

        UpdateWeaponVisibility();
    }

    // Se desactiva el WeaponHolder entero (solo con visor, ver IsOverlayVisible): es más simple
    // que tocar renderers y no choca con WeaponVisual, que ya oculta el cargador durante la
    // recarga (y apuntando no se recarga)
    private void UpdateWeaponVisibility()
    {
        if (weaponHolder == null)
            return;

        bool visible = !IsOverlayVisible;
        if (weaponHolder.activeSelf != visible)
            weaponHolder.SetActive(visible);
    }
}
