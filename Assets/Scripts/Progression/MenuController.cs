using UnityEngine;

// Navegación entre las pantallas de menú (fuera de la partida):
// Perfiles → Inicio (jugar, tienda, récords) → partida → Fin (tienda, otra ronda, récords) → …
// La partida (HUD, transición, Game Over) la sigue llevando GameManager; esto solo decide qué menú se
// ve. Va en el Canvas "UI". Los botones de cada panel se enlazan por código en su propio script
// (StartPanelUI, ShopPanelUI…), así que los prefabs no necesitan referencias a la escena.
public class MenuController : MonoBehaviour
{
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject recordsPanel;
    [SerializeField] private GameObject gameOverPanel;

    // A dónde vuelven Tienda y Récords: al Inicio o al Game Over, según desde dónde se abrieron
    private GameObject returnPanel;

    // Siempre se empieza eligiendo perfil: el último usado sale resaltado y basta un clic
    private void Start()
    {
        ShowProfiles();
    }

    public void ShowProfiles() => Show(profilePanel);
    public void ShowStart() => Show(startPanel);

    public void ShowShop()
    {
        returnPanel = CurrentMainMenu();
        Show(shopPanel);
    }

    public void ShowRecords()
    {
        returnPanel = CurrentMainMenu();
        Show(recordsPanel);
    }

    public void Back() => Show(returnPanel != null ? returnPanel : startPanel);

    private GameObject CurrentMainMenu()
    {
        if (gameOverPanel != null && gameOverPanel.activeSelf)
            return gameOverPanel;
        return startPanel;
    }

    private void Show(GameObject panel)
    {
        SetActive(profilePanel, panel == profilePanel);
        SetActive(startPanel, panel == startPanel);
        SetActive(shopPanel, panel == shopPanel);
        SetActive(recordsPanel, panel == recordsPanel);
        SetActive(gameOverPanel, panel == gameOverPanel);
    }

    private static void SetActive(GameObject panel, bool active)
    {
        if (panel != null && panel.activeSelf != active)
            panel.SetActive(active);
    }
}
