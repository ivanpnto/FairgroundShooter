using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Parte nueva del Inicio (Fase 5): quién juega, sus créditos y los botones de tienda, récords y cambiar
// de perfil. ¡A JUGAR! sigue llamando a GameManager.StartGame desde su OnClick en la escena.
public class StartPanelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text profileText;
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button recordsButton;
    [SerializeField] private Button changeProfileButton;

    private ProfileManager profileManager;
    private MenuController menu;

    private void Awake()
    {
        profileManager = FindFirstObjectByType<ProfileManager>();
        menu = FindFirstObjectByType<MenuController>();

        shopButton.onClick.AddListener(() => { if (menu != null) menu.ShowShop(); });
        recordsButton.onClick.AddListener(() => { if (menu != null) menu.ShowRecords(); });
        changeProfileButton.onClick.AddListener(() => { if (menu != null) menu.ShowProfiles(); });
    }

    private void OnEnable()
    {
        if (profileManager != null) profileManager.ProfileChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (profileManager != null) profileManager.ProfileChanged -= Refresh;
    }

    private void Refresh()
    {
        PlayerProfile profile = profileManager != null ? profileManager.Current : null;
        profileText.text = profile != null ? $"TIRADOR: {profile.name}" : "SIN TIRADOR";
        creditsText.text = profile != null ? $"{profile.credits} CRÉDITOS · RÉCORD {profile.bestScore}" : "";
    }
}
