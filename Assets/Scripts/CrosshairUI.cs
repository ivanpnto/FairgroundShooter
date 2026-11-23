using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cruceta dinámica del HUD: cuatro líneas a la distancia del cono de dispersión real del
/// disparo (PlayerShoot.CurrentSpread, que se abre con cada disparo y se cierra solo), más un
/// hitmarker (X) breve al acertar a un objetivo. Lo que se ve abierto es exactamente donde puede
/// caer la bala. Solo presentación: lee el estado de PlayerShoot, PlayerScope y GameManager.
/// Va en UI/HUD/Crosshair, que necesita un CanvasGroup.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CrosshairUI : MonoBehaviour
{
    [Header("Líneas")]
    [SerializeField] private RectTransform top;
    [SerializeField] private RectTransform bottom;
    [SerializeField] private RectTransform left;
    [SerializeField] private RectTransform right;
    // Mínimo para que, con dispersión 0, las líneas no se monten en el centro
    [Tooltip("Píxeles que se suman al radio del cono, para que con dispersión 0 las líneas no se toquen")]
    [Min(0f)] [SerializeField] private float baseGap = 4f;

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
    private PlayerShoot playerShoot;
    private PlayerScope playerScope;
    private GameManager gameManager;
    private Camera viewCamera;
    private RectTransform canvasRect;

    // Tiempo que le queda al hitmarker; 0 = oculto
    private float hitmarkerTimeLeft;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        playerShoot = FindFirstObjectByType<PlayerShoot>();
        playerScope = FindFirstObjectByType<PlayerScope>();
        gameManager = FindFirstObjectByType<GameManager>();
        viewCamera = playerShoot != null ? playerShoot.GetComponent<Camera>() : Camera.main;
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
    }

    private void OnEnable()
    {
        if (playerShoot != null) playerShoot.ShotResolved += HandleShotResolved;
    }

    // El HUD se apaga entre niveles y en el Game Over: al volver, sin una X a medio desvanecer
    // de la partida anterior (el cono lo lleva PlayerShoot, que ya lo cierra solo)
    private void OnDisable()
    {
        if (playerShoot != null) playerShoot.ShotResolved -= HandleShotResolved;

        hitmarkerTimeLeft = 0f;
        ApplyHitmarker();
    }

    private void HandleShotResolved(ShotResult shot)
    {
        if (!shot.HitTarget)
            return;

        bool penalty = shot.HitPenalty;   // con varios objetivos por disparo, basta con uno
        Color color = penalty ? penaltyColor : hitColor;
        if (hitmarkerImages != null)
            foreach (Image image in hitmarkerImages)
                if (image != null) image.color = color;

        hitmarkerTimeLeft = hitmarkerDuration;
    }

    private void Update()
    {
        // Ocultar con alfa y no con SetActive: así Update sigue corriendo y el hitmarker
        // se desvanece aunque no se vea (p. ej. al apuntar justo tras un acierto)
        bool playing = gameManager != null && gameManager.CurrentState == GameManager.GameState.Playing;
        // Con ZoomAmount y no con IsOverlayVisible: con miras abiertas no hay overlay, pero el
        // guion ya marca el centro y la cruceta lo taparía. A mitad del zoom, cuando el arma
        // ya está casi en la cara
        bool scoped = playerScope != null && playerScope.ZoomAmount > 0.5f;
        canvasGroup.alpha = playing && !scoped ? 1f : 0f;

        hitmarkerTimeLeft = Mathf.Max(0f, hitmarkerTimeLeft - Time.deltaTime);

        ApplyLines();
        ApplyHitmarker();
    }

    // Radio del cono en píxeles del Canvas, con el FOV actual (el zoom lo cambia mientras se apunta)
    private float SpreadPixels()
    {
        if (playerShoot == null || viewCamera == null || canvasRect == null)
            return 0f;
        return WeaponSpread.ToPixels(playerShoot.CurrentSpread, viewCamera.fieldOfView, canvasRect.rect.height * 0.5f);
    }

    private void ApplyLines()
    {
        float gap = baseGap + SpreadPixels();
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
