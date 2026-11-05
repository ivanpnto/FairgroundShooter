using UnityEngine;

// Navegación entre las pantallas de menú (fuera de la partida):
// Perfiles → Inicio (jugar, habilidades, armería, récords) → partida → Fin (otra ronda, habilidades, armería, récords) → …
// La partida (HUD, transición, Game Over) la sigue llevando GameManager; esto solo decide qué menú se
// ve. Va en el Canvas "UI". Los botones de cada panel se enlazan por código en su propio script
// (StartPanelUI, SkillsPanelUI…), así que los prefabs no necesitan referencias a la escena.
public class MenuController : MonoBehaviour
{
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject skillsPanel;
    [SerializeField] private GameObject armoryPanel;
    [SerializeField] private GameObject recordsPanel;
    [SerializeField] private GameObject gameOverPanel;

    // A dónde vuelven Habilidades, Armería y Récords: al Inicio o al Game Over, según desde dónde se abrieron
    private GameObject returnPanel;

    // Siempre se empieza eligiendo perfil: el último usado sale resaltado y basta un clic
    private void Start()
    {
        ShowProfiles();
    }

    public void ShowProfiles() => Show(profilePanel);
    public void ShowStart() => Show(startPanel);

    public void ShowSkills()
    {
        returnPanel = CurrentMainMenu();
        Show(skillsPanel);
    }

    // Hoy una lista como Habilidades; más adelante, la pantalla con las armas expuestas. Quien la
    // abre solo llama aquí, así que cambiar el panel no obliga a tocar Inicio ni Game Over
    public void ShowArmory()
    {
        returnPanel = CurrentMainMenu();
        Show(armoryPanel);
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
        SetActive(skillsPanel, panel == skillsPanel);
        SetActive(armoryPanel, panel == armoryPanel);
        SetActive(recordsPanel, panel == recordsPanel);
        SetActive(gameOverPanel, panel == gameOverPanel);
    }

    private static void SetActive(GameObject panel, bool active)
    {
        if (panel != null && panel.activeSelf != active)
            panel.SetActive(active);
    }
}
