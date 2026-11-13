using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pantalla «Cómo jugar» (bloque 2): controles y significado de los colores.
//
// - Se abre desde el botón CÓMO JUGAR del inicio (Open), y se cierra con ENTENDIDO.
// - La PRIMERA partida de cada perfil sale sola: el botón EMPEZAR llama a RequestStart en vez de a
//   GameManager.StartGame; si el perfil no la ha visto, se muestra con el botón ¡A JUGAR!, que la marca como
//   vista en el perfil y empieza la partida. Las siguientes veces RequestStart empieza directamente.
// - La fila del contorno rosa (+1 corazón) solo aparece si el perfil ya ha visto al boss: si no, destriparía
//   la sorpresa del nivel 4 (§27.1).
public class HowToPlayUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject pinkRow;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text closeButtonText;
    [SerializeField] private Button openButton;

    private GameManager gameManager;
    private ProfileManager profiles;
    private bool startAfterClose;

    private void Awake()
    {
        gameManager = SceneCache<GameManager>.Get();
        profiles = SceneCache<ProfileManager>.Get();
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (panel != null) panel.SetActive(false);
    }

    // Botón CÓMO JUGAR
    public void Open() => Show(startGameOnClose: false);

    // Botón EMPEZAR del inicio
    public void RequestStart()
    {
        PlayerProfile current = profiles != null ? profiles.Current : null;
        if (current != null && !current.seenHowToPlay)
        {
            Show(startGameOnClose: true);
            return;
        }
        if (gameManager != null) gameManager.StartGame();
    }

    private void Show(bool startGameOnClose)
    {
        startAfterClose = startGameOnClose;
        PlayerProfile current = profiles != null ? profiles.Current : null;
        if (pinkRow != null) pinkRow.SetActive(current != null && current.bossEncountered);
        if (closeButtonText != null) closeButtonText.text = startGameOnClose ? "¡A JUGAR!" : "ENTENDIDO";
        panel.SetActive(true);
    }

    private void Close()
    {
        panel.SetActive(false);
        if (profiles != null) profiles.MarkHowToPlaySeen();
        if (startAfterClose && gameManager != null) gameManager.StartGame();
        startAfterClose = false;
    }
}
