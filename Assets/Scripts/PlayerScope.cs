using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mira telescópica: con un arma que tenga hasScope, el clic derecho mantenido acerca
/// el FOV de la cámara a scopeFov. Va en Player (que es la cámara).
/// El overlay negro con la retícula es de ScopeOverlayUI; aquí solo está el estado.
/// </summary>
public class PlayerScope : MonoBehaviour
{
    [Tooltip("Hijo de Player con el modelo del arma: se oculta al mirar por la mira")]
    [SerializeField] private GameObject weaponHolder;
    [Tooltip("Velocidad del zoom (mayor = más rápido). Con 20 tarda unos 0,15 s")]
    [SerializeField] private float zoomSpeed = 20f;

    // A partir de este ZoomAmount se ve el overlay y desaparece el arma. Al final del zoom
    // y no al principio: así se nota que el arma "sube" a la cara antes de tapar la vista
    public const float OverlayThreshold = 0.7f;

    private Camera cam;
    private GameManager gameManager;
    private WeaponController weaponController;
    private WeaponAmmo weaponAmmo;

    // El FOV de la escena (60). Se lee al empezar en lugar de fijarlo en el código
    // para que cambiarlo en la cámara no obligue a tocar este script
    private float defaultFov;

    /// <summary>0 = sin mira, 1 = zoom completo. Lo lee ScopeOverlayUI.</summary>
    public float ZoomAmount { get; private set; }

    public bool IsOverlayVisible => ZoomAmount >= OverlayThreshold;

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

        // Recargar saca de la mira, como en un arma real (hay que bajarla para cambiar el cargador)
        bool wantsScope = weapon != null && weapon.hasScope
                          && Mouse.current != null && Mouse.current.rightButton.isPressed
                          && (weaponAmmo == null || !weaponAmmo.IsReloading);

        float targetFov = wantsScope ? weapon.scopeFov : defaultFov;

        // Decaimiento exponencial hacia el objetivo: independiente de los FPS
        float t = 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, t);

        // Si el arma cambia (o deja de tener mira), el zoom se calcula respecto al
        // arma actual; sin mira, cualquier FOV por debajo del normal cuenta como 0
        float scopeFov = weapon != null && weapon.hasScope ? weapon.scopeFov : defaultFov;
        ZoomAmount = Mathf.Approximately(scopeFov, defaultFov)
            ? 0f
            : Mathf.InverseLerp(defaultFov, scopeFov, cam.fieldOfView);

        UpdateWeaponVisibility();
    }

    // Se desactiva el WeaponHolder entero: es más simple que tocar renderers y no choca con
    // WeaponVisual, que ya oculta el cargador durante la recarga (y con mira no se recarga)
    private void UpdateWeaponVisibility()
    {
        if (weaponHolder == null)
            return;

        bool visible = !IsOverlayVisible;
        if (weaponHolder.activeSelf != visible)
            weaponHolder.SetActive(visible);
    }
}
