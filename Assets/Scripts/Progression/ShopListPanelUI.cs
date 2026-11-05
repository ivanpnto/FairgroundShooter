using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Base de las pantallas de compra en forma de lista (Habilidades y Armería): créditos arriba, una fila
// por artículo clonada de una plantilla y VOLVER. Comprar lo hace ProfileManager; la lista se repinta
// entera con ProfileChanged, así que créditos, niveles y botones siempre son coherentes.
// Cada pantalla decide qué filas pone (AddRows). Son paneles separados a propósito: la Armería podrá
// cambiarse por una pantalla con las armas expuestas sin tocar Habilidades ni la navegación.
public abstract class ShopListPanelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private Transform content;
    [Tooltip("Fila de ejemplo (desactivada) que se clona por cada artículo")]
    [SerializeField] private ShopRowUI rowTemplate;
    [SerializeField] private Button backButton;

    protected ProfileManager profileManager;
    private MenuController menu;
    private readonly List<GameObject> rows = new List<GameObject>();

    protected virtual void Awake()
    {
        profileManager = FindFirstObjectByType<ProfileManager>();
        menu = FindFirstObjectByType<MenuController>();

        rowTemplate.gameObject.SetActive(false);
        backButton.onClick.AddListener(() => { if (menu != null) menu.Back(); });
    }

    private void OnEnable()
    {
        if (profileManager != null) profileManager.ProfileChanged += Rebuild;
        Rebuild();
    }

    private void OnDisable()
    {
        if (profileManager != null) profileManager.ProfileChanged -= Rebuild;
    }

    private void Rebuild()
    {
        foreach (GameObject row in rows)
            Destroy(row);
        rows.Clear();

        if (profileManager == null)
            return;

        int credits = profileManager.Credits;
        creditsText.text = $"TUS CRÉDITOS: {credits}";
        AddRows(credits);
    }

    // Añade las filas de esta pantalla con NewRow
    protected abstract void AddRows(int credits);

    protected ShopRowUI NewRow()
    {
        ShopRowUI row = Instantiate(rowTemplate, content);
        row.gameObject.SetActive(true);
        rows.Add(row.gameObject);
        return row;
    }
}
