using UnityEngine;
using UnityEngine.UI;

// Botón VOLVER de Tienda y Récords: vuelve a la pantalla desde la que se abrieron (Inicio o Game Over)
[RequireComponent(typeof(Button))]
public class MenuBackButton : MonoBehaviour
{
    private void Awake()
    {
        MenuController menu = FindFirstObjectByType<MenuController>();
        GetComponent<Button>().onClick.AddListener(() => { if (menu != null) menu.Back(); });
    }
}
