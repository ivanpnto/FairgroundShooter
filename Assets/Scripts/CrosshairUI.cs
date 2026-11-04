using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cruceta dinámica del HUD: cuatro líneas que se abren con cada disparo según el retroceso
/// del arma y vuelven al reposo, más un hitmarker (X) breve al acertar a un objetivo.
/// Solo presentación: lee el estado de WeaponAmmo, WeaponController, PlayerShoot, PlayerScope
/// y GameManager. Va en UI/HUD/Crosshair, que necesita un CanvasGroup.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CrosshairUI : MonoBehaviour
{
    [Header("Líneas")]
    [SerializeField] private RectTransform top;
    [SerializeField] private RectTransform bottom;
    [SerializeField] private RectTransform left;
    [SerializeField] private RectTransform right;
    [Tooltip("Distancia en píxeles del centro al extremo interior de cada línea en reposo")]
    [Min(0f)] [SerializeField] private float baseGap = 8f;
    // Proporcional a cameraKick (grados que sube la cámara por disparo): así la cruceta se
    // abre más con el arma que más retrocede, sin un valor nuevo por arma en WeaponData.
    // Con 4: MP5 1,2 px por disparo (se nota al acumularse en ráfaga) y Barrett 28 px
    [Tooltip("Píxeles de apertura por cada grado de WeaponData.cameraKick")]
    [Min(0f)] [SerializeField] private float spreadPerKick = 4f;
    [Tooltip("Apertura máxima en píxeles, para que la ráfaga no saque las líneas de la pantalla")]
    [Min(0f)] [SerializeField] private float maxSpread = 40f;
    [Tooltip("Velocidad con la que la apertura vuelve a 0 (decaimiento exponencial)")]
    [Min(0f)] [SerializeField] private float recoverySpeed = 10f;

    [Header("Hitmarker")]
    [SerializeField] private CanvasGroup hitmarker;
    [Tooltip("Imágenes de la X, para teñirlas según el tipo de objetivo")]
    [SerializeField] private Image[] hitmarkerImages;
    [Tooltip("Segundos que tarda la X en desvanecerse")]
    [Min(0.01f)] [SerializeField] private float hitmarkerDuration = 0.15f;
    [SerializeField] private Color hitColor = Color.white;
    [Tooltip("Color al acertar a un objetivo de penalización (puntos negativos)")]
    [SerializeField] private Color penaltyColor = new Color(1f, 0.2f, 0.2f);

    private CanvasGroup canvasGroup;
    private WeaponAmmo weaponAmmo;
    private WeaponController weaponController;
    private PlayerShoot playerShoot;
    private PlayerScope playerScope;
    private GameManager gameManager;

    private float spread;
    // Tiempo que le queda al hitmarker; 0 = oculto
    private float hitmarkerTimeLeft;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        weaponAmmo = FindFirstObjectByType<WeaponAmmo>();
        weaponController = FindFirstObjectByType<WeaponController>();
        playerShoot = FindFirstObjectByType<PlayerShoot>();
        playerScope = FindFirstObjectByType<PlayerScope>();
        gameManager = FindFirstObjectByType<GameManager>();
    }

    // El mismo evento de disparo que WeaponEffects: la cruceta se abre exactamente cuando
    // suena el disparo, no cuando se pulsa el botón (con el cargador vacío no se abre)
    private void OnEnable()
    {
        if (weaponAmmo != null) weaponAmmo.OnShot += HandleShot;
        if (playerShoot != null) playerShoot.ShotResolved += HandleShotResolved;
    }

    // El HUD se apaga entre niveles y en el Game Over: al volver, la cruceta debe estar
    // cerrada y sin una X a medio desvanecer de la partida anterior
    private void OnDisable()
    {
        if (weaponAmmo != null) weaponAmmo.OnShot -= HandleShot;
        if (playerShoot != null) playerShoot.ShotResolved -= HandleShotResolved;

        spread = 0f;
        hitmarkerTimeLeft = 0f;
        ApplyLines();
        ApplyHitmarker();
    }

    private void HandleShot()
    {
        WeaponData weapon = weaponController != null ? weaponController.CurrentWeapon : null;
        if (weapon == null)
            return;

        spread = Mathf.Min(spread + spreadPerKick * weapon.cameraKick, maxSpread);
    }

    private void HandleShotResolved(ShotResult shot)
    {
        if (!shot.HitTarget)
            return;

        bool penalty = shot.Target != null && shot.Target.IsPenalty;
        Color color = penalty ? penaltyColor : hitColor;
        if (hitmarkerImages != null)
            foreach (Image image in hitmarkerImages)
                if (image != null) image.color = color;

        hitmarkerTimeLeft = hitmarkerDuration;
    }

    private void Update()
    {
        // Ocultar con alfa y no con SetActive: así Update sigue corriendo y la apertura
        // vuelve al reposo aunque no se vea (p. ej. al quitar la mira tras un disparo)
        bool playing = gameManager != null && gameManager.CurrentState == GameManager.GameState.Playing;
        // Con ZoomAmount y no con IsOverlayVisible: con miras abiertas no hay overlay, pero el
        // guion ya marca el centro y la cruceta lo taparía. A mitad del zoom, cuando el arma
        // ya está casi en la cara
        bool scoped = playerScope != null && playerScope.ZoomAmount > 0.5f;
        canvasGroup.alpha = playing && !scoped ? 1f : 0f;

        // Exponencial con Exp(-k·dt), como PlayerScope: el mismo ritmo a cualquier FPS
        spread *= Mathf.Exp(-recoverySpeed * Time.deltaTime);
        hitmarkerTimeLeft = Mathf.Max(0f, hitmarkerTimeLeft - Time.deltaTime);

        ApplyLines();
        ApplyHitmarker();
    }

    private void ApplyLines()
    {
        float gap = baseGap + spread;
        // Cada línea se centra a gap + media longitud, para que gap sea la distancia al
        // extremo interior. La longitud sale de su tamaño en el Inspector
        if (top != null) top.anchoredPosition = new Vector2(0f, gap + top.rect.height * 0.5f);
        if (bottom != null) bottom.anchoredPosition = new Vector2(0f, -(gap + bottom.rect.height * 0.5f));
        if (left != null) left.anchoredPosition = new Vector2(-(gap + left.rect.width * 0.5f), 0f);
        if (right != null) right.anchoredPosition = new Vector2(gap + right.rect.width * 0.5f, 0f);
    }

    private void ApplyHitmarker()
    {
        if (hitmarker != null)
            hitmarker.alpha = hitmarkerTimeLeft / hitmarkerDuration;
    }
}
