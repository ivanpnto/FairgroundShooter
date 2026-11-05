using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Parte nueva del Inicio (Fase 5): quién juega, sus créditos y los botones de habilidades, armería, récords y cambiar
// de perfil. ¡A JUGAR! sigue llamando a GameManager.StartGame desde su OnClick en la escena.
public class StartPanelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text profileText;
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private Button skillsButton;
    // Era el botón TIENDA: el mismo ticket pasa a ser ARMERÍA, así no se pierde la referencia
    [FormerlySerializedAs("shopButton")]
    [SerializeField] private Button armoryButton;
    [SerializeField] private Button recordsButton;
    [SerializeField] private Button changeProfileButton;

    private ProfileManager profileManager;
    private MenuController menu;

    private void Awake()
    {
        profileManager = FindFirstObjectByType<ProfileManager>();
        menu = FindFirstObjectByType<MenuController>();

        skillsButton.onClick.AddListener(() => { if (menu != null) menu.ShowSkills(); });
        armoryButton.onClick.AddListener(() => { if (menu != null) menu.ShowArmory(); });
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
