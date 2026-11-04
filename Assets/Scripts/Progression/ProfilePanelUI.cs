using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pantalla de perfiles: lista de los existentes (un clic lo elige) y campo para crear uno nuevo.
// Sustituye al antiguo campo de nombre del Inicio. Solo UI: crear y elegir lo hace ProfileManager.
public class ProfilePanelUI : MonoBehaviour
{
    [SerializeField] private Transform listContent;
    [Tooltip("Botón de ejemplo (desactivado) que se clona por cada perfil")]
    [SerializeField] private Button entryTemplate;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button createButton;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text emptyText;

    [SerializeField, Min(1)] private int maxNameLength = 16;
    [SerializeField] private Color currentColor = new Color(1f, 0.82f, 0.29f);

    private ProfileManager profileManager;
    private MenuController menu;
    private readonly List<GameObject> entries = new List<GameObject>();

    private void Awake()
    {
        profileManager = FindFirstObjectByType<ProfileManager>();
        menu = FindFirstObjectByType<MenuController>();

        entryTemplate.gameObject.SetActive(false);
        createButton.onClick.AddListener(Create);
        nameInput.characterLimit = maxNameLength;
        nameInput.onSubmit.AddListener(_ => Create());   // Intro también crea
    }

    private void OnEnable()
    {
        SetMessage("");
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (GameObject entry in entries)
            Destroy(entry);
        entries.Clear();

        if (profileManager == null)
            return;

        IReadOnlyList<PlayerProfile> profiles = profileManager.AllProfiles;
        string current = profileManager.Current != null ? profileManager.Current.name : null;

        if (emptyText != null)
            emptyText.gameObject.SetActive(profiles.Count == 0);

        foreach (PlayerProfile profile in profiles)
        {
            Button button = Instantiate(entryTemplate, listContent);
            button.gameObject.SetActive(true);
            button.name = "Profile_" + profile.name;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            label.text = $"{profile.name}   ·   récord {profile.bestScore}   ·   {profile.credits} créditos";
            if (profile.name == current)
                label.color = currentColor;   // el último usado: el jugador lo encuentra de un vistazo

            string profileName = profile.name;   // copia para el cierre: no capturar la variable del bucle
            button.onClick.AddListener(() => Select(profileName));
            entries.Add(button.gameObject);
        }
    }

    private void Select(string profileName)
    {
        if (profileManager != null && profileManager.SelectProfile(profileName) && menu != null)
            menu.ShowStart();
    }

    private void Create()
    {
        if (profileManager == null)
            return;

        string profileName = nameInput.text.Trim();

        if (profileName.Length == 0)
        {
            SetMessage("Escribe un nombre");
            return;
        }

        // La fuente de los carteles (Rye) solo trae ASCII y las letras del español: otro carácter
        // saldría como un cuadrado en el Inicio y en la lista
        if (!LabelOf(entryTemplate).font.HasCharacters(profileName))
        {
            SetMessage("Usa solo letras, números y espacios");
            return;
        }

        if (!profileManager.CreateProfile(profileName))
        {
            SetMessage("Ese nombre ya existe");
            return;
        }

        nameInput.text = "";
        if (menu != null)
            menu.ShowStart();
    }

    private static TMP_Text LabelOf(Button button) => button.GetComponentInChildren<TMP_Text>(true);

    private void SetMessage(string message)
    {
        if (messageText != null)
            messageText.text = message;
    }
}
