using UnityEngine;

/// <summary>
/// Overlay de la mira telescópica (círculo negro con retícula).
/// Solo presentación: el estado lo decide PlayerScope. Va en el HUD.
/// La cruceta se oculta sola con la mira (CrosshairUI): si este script la apagase con
/// SetActive, pararía también el Update de CrosshairUI.
/// </summary>
public class ScopeOverlayUI : MonoBehaviour
{
    [Tooltip("Raíz del overlay (círculo + bandas laterales)")]
    [SerializeField] private GameObject overlay;

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
    // partida empezaría con el overlay visible hasta el primer Update
    private void OnDisable() => Show(false);

    private void Show(bool scoped)
    {
        if (overlay != null && overlay.activeSelf != scoped)
            overlay.SetActive(scoped);
    }
}
