using UnityEngine;

// Baja y sube las luces del escenario (Fase 8): con el telón cerrado entre niveles, el puesto queda en penumbra
// y vuelve a iluminarse al abrirse con el decorado nuevo.
//
// Trabaja con TODAS las luces de los tres montajes de iluminación (Lighting_Night, _Sea, _Sky): al cambiar de
// nivel detrás del telón se enciende otro montaje, y debe aparecer ya en penumbra, no a plena luz.
//
// Solo escribe las intensidades mientras está atenuado o en un fundido. A plena luz no toca nada, para no
// pelearse con BossReveal, que apaga y enciende esas mismas luces en la revelación del boss.
public class StageDimmer : MonoBehaviour
{
    [SerializeField] private Light[] lights;
    [Tooltip("Segundos de un fundido completo (de plena luz a la penumbra o al revés)")]
    [SerializeField, Min(0.05f)] private float fadeTime = 0.8f;

    private float[] baseIntensity;
    private float level = 1f;

    // 1 = plena luz; 0,3 = penumbra. Se llega con un fundido
    public float Target { get; set; } = 1f;

    private void Awake()
    {
        if (lights == null) lights = new Light[0];
        baseIntensity = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++)
            baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
    }

    private void Update()
    {
        if (Mathf.Approximately(level, Target) && level >= 1f)
            return;   // a plena luz y quieto: no se toca nada

        level = Mathf.MoveTowards(level, Target, Time.deltaTime / fadeTime);
        if (level >= 1f && Target >= 1f) level = 1f;
        Apply();
    }

    // Vuelve a plena luz al instante (partida nueva)
    public void ResetToFull()
    {
        Target = 1f;
        level = 1f;
        Apply();
    }

    private void Apply()
    {
        for (int i = 0; i < lights.Length; i++)
            if (lights[i] != null) lights[i].intensity = baseIntensity[i] * level;
    }
}
