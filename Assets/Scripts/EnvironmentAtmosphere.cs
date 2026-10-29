using UnityEngine;
using UnityEngine.Rendering;

// Luz ambiental y niebla de un entorno. RenderSettings es global de la escena, así que cada
// entorno lo aplica al activarse: el LevelManager apaga todos los entornos y enciende el del nivel.
public class EnvironmentAtmosphere : MonoBehaviour
{
    [Header("Luz ambiental (degradado)")]
    [SerializeField] private Color skyColor = new Color(0.10f, 0.12f, 0.24f);
    [SerializeField] private Color equatorColor = new Color(0.10f, 0.08f, 0.14f);
    [SerializeField] private Color groundColor = new Color(0.04f, 0.03f, 0.04f);

    [Header("Niebla (lineal)")]
    [SerializeField] private bool fog = false;
    [SerializeField] private Color fogColor = new Color(0.03f, 0.18f, 0.30f);
    [SerializeField] private float fogStart = 6f;
    [SerializeField] private float fogEnd = 22f;

    private void OnEnable()
    {
        Apply();
    }

    public void Apply()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyColor;
        RenderSettings.ambientEquatorColor = equatorColor;
        RenderSettings.ambientGroundColor = groundColor;

        RenderSettings.fog = fog;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
    }
}
