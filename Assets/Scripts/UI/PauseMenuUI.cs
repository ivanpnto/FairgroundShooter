using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Menú de pausa (Fase 8): Esc pausa y reanuda. CONTINUAR, OPCIONES (volumen), TERMINAR PARTIDA y, desde el 25/11,
// SALIR DEL JUEGO (cobra los créditos como TERMINAR y cierra el juego).
//
// La pausa en sí es de GameManager (Pause / Resume / QuitRun); aquí solo están la tecla y el panel. El panel
// sigue al evento PauseChanged y no a la tecla, así que también se ve si otro sistema pausa el juego.
//
// Esc también cierra OPCIONES, en la pausa y en el menú de inicio: se gestiona en un solo sitio para que una
// misma pulsación no cierre las opciones y además reanude la partida en el mismo frame.
//
// En la build, perder el foco (Alt+Tab) pausa: volver y encontrarse sin corazones sería injusto. En el editor
// no, porque cada clic en otra ventana del editor pausaría la partida.
public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;      // fondo oscuro a pantalla completa
    [SerializeField] private GameObject card;       // tablero con los botones (se oculta mientras hay opciones)
    [SerializeField] private Button continueButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private TMP_Text quitText;
    [SerializeField] private Button exitButton;
    [SerializeField] private TMP_Text exitText;
    [SerializeField] private OptionsPanelUI options;

    private const string QuitLabel = "TERMINAR PARTIDA";
    private const string ConfirmLabel = "¿SEGURO? PULSA OTRA VEZ";
    private const string ExitLabel = "SALIR DEL JUEGO";

    private GameManager gameManager;
    private bool confirmingQuit;
    private bool confirmingExit;

    private void Awake()
    {
        gameManager = SceneCache<GameManager>.Get();
        if (continueButton != null) continueButton.onClick.AddListener(() => { if (gameManager != null) gameManager.Resume(); });
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        if (quitButton != null) quitButton.onClick.AddListener(HandleQuit);
        if (exitButton != null) exitButton.onClick.AddListener(HandleExit);
        if (panel != null) panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (gameManager != null) gameManager.PauseChanged += HandlePauseChanged;
        if (options != null) options.Closed += HandleOptionsClosed;
    }

    private void OnDisable()
    {
        if (gameManager != null) gameManager.PauseChanged -= HandlePauseChanged;
        if (options != null) options.Closed -= HandleOptionsClosed;
    }

    private void Update()
    {
        if (gameManager == null || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (options != null && options.IsOpen)
            options.Close();
        else if (gameManager.IsPaused)
            gameManager.Resume();
        else
            gameManager.Pause();   // no hace nada fuera de Playing
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && !Application.isEditor && gameManager != null)
            gameManager.Pause();
    }

    private void HandlePauseChanged(bool paused)
    {
        if (paused)
        {
            confirmingQuit = false;
            if (quitText != null) quitText.text = QuitLabel;
            confirmingExit = false;
            if (exitText != null) exitText.text = ExitLabel;
            if (card != null) card.SetActive(true);
        }
        else if (options != null && options.IsOpen)
        {
            options.Close();
        }

        if (panel != null) panel.SetActive(paused);
        // Sin esto, el botón pulsado queda seleccionado y Espacio o Intro lo volverían a pulsar ya jugando
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void OpenOptions()
    {
        if (options == null) return;
        if (card != null) card.SetActive(false);
        options.Open();
    }

    private void HandleOptionsClosed()
    {
        if (gameManager != null && gameManager.IsPaused && card != null)
            card.SetActive(true);
    }

    // Doble clic para terminar: un clic sin querer no debe tirar a la basura una partida de cuatro minutos
    private void HandleQuit()
    {
        if (!confirmingQuit)
        {
            confirmingQuit = true;
            if (quitText != null) quitText.text = ConfirmLabel;
            return;
        }
        if (gameManager != null) gameManager.QuitRun();
    }

    // Igual que TERMINAR (doble clic), y además cierra el juego. La partida se termina antes para que el perfil
    // guarde los créditos de lo conseguido: salir no debe castigar más que terminar
    private void HandleExit()
    {
        if (!confirmingExit)
        {
            confirmingExit = true;
            if (exitText != null) exitText.text = ConfirmLabel;
            return;
        }
        if (gameManager != null) gameManager.QuitRun();
        GameQuit.Quit();
    }
}
