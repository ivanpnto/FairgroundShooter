#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

// Menú de depuración de la progresión (F1): dar 500 créditos, reiniciar el perfil actual y desbloquear
// todo. Como el cambio de arma con 1–5, solo existe en el editor y en development builds: en una build
// normal no se compila, así que no sirve para hacer trampas.
//
// Se crea solo al arrancar (RuntimeInitializeOnLoadMethod) para no añadir nada a Main.unity.
// Usa IMGUI (OnGUI) porque es una herramienta interna: no necesita prefab ni estilo de feria.
public class ProgressionDebugMenu : MonoBehaviour
{
    private const int CreditsGift = 500;

    private ProfileManager profileManager;
    private WeaponController weaponController;
    private bool visible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        GameObject menu = new GameObject("ProgressionDebugMenu");
        menu.AddComponent<ProgressionDebugMenu>();
        DontDestroyOnLoad(menu);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            visible = !visible;
    }

    private void OnGUI()
    {
        if (!visible)
            return;

        // Se buscan al abrir y no en Awake: este objeto nace antes de que existan en algunas cargas
        if (profileManager == null) profileManager = FindFirstObjectByType<ProfileManager>();
        if (weaponController == null) weaponController = FindFirstObjectByType<WeaponController>();

        GUILayout.BeginArea(new Rect(20, 20, 300, 220), "Depuración · progresión (F1)", GUI.skin.window);

        if (profileManager == null || profileManager.Current == null)
        {
            GUILayout.Label("No hay perfil elegido.");
        }
        else
        {
            PlayerProfile p = profileManager.Current;
            GUILayout.Label($"{p.name}: {p.credits} créditos · récord {p.bestScore} · {p.gamesPlayed} partidas");

            if (GUILayout.Button($"+{CreditsGift} créditos"))
                profileManager.AddCredits(CreditsGift);

            if (GUILayout.Button("Reiniciar el perfil actual"))
                profileManager.ResetCurrentProfile();

            if (GUILayout.Button("Desbloquear todo (armas y mejoras)"))
                profileManager.UnlockEverything(weaponController != null ? weaponController.AvailableWeapons : null);
        }

        GUILayout.EndArea();
    }
}
#endif
