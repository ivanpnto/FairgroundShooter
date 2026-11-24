using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Selector de arma al darle a jugar (24/11). Sustituye a los selectores ◀ ▶ que había en el inicio y en el Game Over:
// para comprar y equipar está la armería, y aquí solo se confirma con qué arma se juega esta partida.
//
// - EMPEZAR llama a RequestStart y OTRA RONDA a RequestRestart (enlazados por WeaponPickerBuilder).
// - Si el perfil solo tiene un arma (perfil nuevo), no se pregunta: se empieza directamente.
// - ¡A JUGAR! sigue como antes: desde el inicio pasa por HowToPlayUI (que sale sola si el perfil no ha visto la
//   versión actual de CÓMO JUGAR) y desde el Game Over llama a GameManager.RestartGame.
// - VOLVER o Esc cierran el selector sin jugar.
// El selector de dentro es el WeaponSelectorUI de siempre: equipa al momento, así que la partida no necesita saber
// nada de esta pantalla.
public class WeaponPickerUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button playButton;
    [SerializeField] private Button backButton;

    private GameManager gameManager;
    private WeaponController weapons;
    private HowToPlayUI howToPlay;
    private bool isRestart;

    public bool IsOpen => panel != null && panel.activeSelf;

    private void Awake()
    {
        gameManager = SceneCache<GameManager>.Get();
        weapons = SceneCache<WeaponController>.Get();
        howToPlay = SceneCache<HowToPlayUI>.Get();
        if (playButton != null) playButton.onClick.AddListener(Play);
        if (backButton != null) backButton.onClick.AddListener(Close);
        if (panel != null) panel.SetActive(false);
    }

    // Botón EMPEZAR del inicio
    public void RequestStart() => Request(restart: false);

    // Botón OTRA RONDA del Game Over
    public void RequestRestart() => Request(restart: true);

    private void Request(bool restart)
    {
        isRestart = restart;
        if (weapons == null || weapons.UnlockedCount <= 1 || panel == null)
        {
            Play();
            return;
        }
        panel.SetActive(true);
    }

    private void Update()
    {
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    private void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void Play()
    {
        Close();
        if (gameManager == null)
            return;

        if (isRestart)
            gameManager.RestartGame();
        else if (howToPlay != null)
            howToPlay.RequestStart();
        else
            gameManager.StartGame();
    }
}
