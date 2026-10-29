using UnityEngine;

/// <summary>
/// Overlay de la mira telescópica (círculo negro con retícula) y ocultación de la cruceta.
/// Solo presentación: el estado lo decide PlayerScope. Va en el HUD.
/// </summary>
public class ScopeOverlayUI : MonoBehaviour
{
    [Tooltip("Raíz del overlay (círculo + bandas laterales)")]
    [SerializeField] private GameObject overlay;
    [Tooltip("Cruceta normal del HUD: con la mira sobra, porque la retícula ya marca el centro")]
    [SerializeField] private GameObject crosshair;

    private PlayerScope playerScope;

    private void Awake()
    {
        playerScope = FindFirstObjectByType<PlayerScope>();
    }

    // Todo o nada, como en la mayoría de juegos: un overlay a medio fundir sobre un FOV
    // que aún se está cerrando se ve peor que un corte limpio al final del zoom
    private void Update()
    {
        bool scoped = playerScope != null && playerScope.IsOverlayVisible;
        Show(scoped);
    }

    // El HUD se desactiva en el Game Over: si se apagase con la mira puesta, la siguiente
    // partida empezaría con el overlay visible y sin cruceta hasta el primer Update
    private void OnDisable() => Show(false);

    private void Show(bool scoped)
    {
        if (overlay != null && overlay.activeSelf != scoped)
            overlay.SetActive(scoped);

        if (crosshair != null && crosshair.activeSelf == scoped)
            crosshair.SetActive(!scoped);
    }
}
