using UnityEngine;
using UnityEngine.UI;

// Botón SALIR del menú de inicio (25/11): cierra el juego. Antes no había forma de salir de la build a pantalla
// completa salvo Alt+F4. En la pausa, SALIR DEL JUEGO lo gestiona PauseMenuUI (primero cobra los créditos).
[RequireComponent(typeof(Button))]
public class QuitButtonUI : MonoBehaviour
{
    private void Awake() => GetComponent<Button>().onClick.AddListener(GameQuit.Quit);
}

// Un solo sitio para cerrar el juego. Application.Quit no hace nada en el editor: ahí se sale del modo Play
public static class GameQuit
{
    public static void Quit()
    {
        GameLog.Info("Salir del juego");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
