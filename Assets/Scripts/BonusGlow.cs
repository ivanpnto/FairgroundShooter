using UnityEngine;

// Hace "latir" el halo de los objetivos especiales: un brillo que cambia llama más la atención que uno fijo.
// Usa un MaterialPropertyBlock para variar el color por objeto sin crear copias del material.
public class BonusGlow : MonoBehaviour
{
    [SerializeField] private float pulseSpeed = 3.5f;
    [SerializeField] private float minBrightness = 0.5f;
    [SerializeField] private float maxBrightness = 1f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Renderer[] renderers;
    private Color baseColor;
    private MaterialPropertyBlock block;

    public void Init(Renderer[] haloRenderers, Color color)
    {
        renderers = haloRenderers;
        baseColor = color;
        block = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (renderers == null)
            return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        block.SetColor(BaseColorId, baseColor * Mathf.Lerp(minBrightness, maxBrightness, t));

        foreach (Renderer rend in renderers)
        {
            if (rend != null)
                rend.SetPropertyBlock(block);
        }
    }
}
