using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bombillas de un cartel de feria: se encienden en cadena, como en las marquesinas.
/// Solo presentación. Va en el objeto que agrupa las bombillas (Image hijas).
/// </summary>
public class MarqueeLights : MonoBehaviour
{
    [SerializeField] private Color onColor = new Color(1f, 0.92f, 0.65f);
    [SerializeField] private Color offColor = new Color(0.45f, 0.3f, 0.15f);
    [Tooltip("Segundos entre cada paso de la cadena")]
    [SerializeField] private float stepTime = 0.25f;
    [Tooltip("Cada cuántas bombillas se repite el patrón: con 3, una de cada tres apagada")]
    [Min(2)] [SerializeField] private int groupSize = 3;

    private Image[] bulbs;

    // Se recogen solas las Image hijas: así el número de bombillas se cambia en el
    // editor (duplicando o borrando) sin reasignar ningún array
    private void Awake()
    {
        bulbs = GetComponentsInChildren<Image>(true);
    }

    private void Update()
    {
        // Tiempo sin escalar: los menús se ven fuera de la partida y no deben depender
        // de Time.timeScale si algún día se pausa el juego con él
        int step = Mathf.FloorToInt(Time.unscaledTime / stepTime);

        // La bombilla apagada "avanza" una posición por paso: da la sensación de que
        // las luces corren alrededor del cartel
        for (int i = 0; i < bulbs.Length; i++)
            bulbs[i].color = (i + step) % groupSize == 0 ? offColor : onColor;
    }
}
