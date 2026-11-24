using TMPro;
using UnityEngine;

// Piezas comunes de los efectos que se montan por código (BossHonkFX, BossWaterJet, WindGusts, ShyTarget): líneas,
// anillos, sistemas de partículas sencillos y carteles en pantalla. Así cada efecto no repite la misma
// configuración y no hay que montar nada en la escena salvo el material.
//
// Material: se pasa uno aditivo del proyecto (p. ej. MuzzleFlash_Additive o Tracer_Additive) como plantilla.
// Para las partículas se hace una copia con una textura de punto suave generada aquí: un material de partículas
// sin textura dibuja cuadrados.
public static class FxUtil
{
    private static Texture2D softDot;

    // Punto blanco con borde suave (degradado radial), generado una vez
    public static Texture2D SoftDot
    {
        get
        {
            if (softDot != null) return softDot;
            const int size = 64;
            softDot = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "FxSoftDot", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);   // SmoothStep: centro lleno, borde que se desvanece
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            softDot.SetPixels32(pixels);
            softDot.Apply(false, true);
            return softDot;
        }
    }

    // Copia del material con el punto suave como textura (_BaseMap en URP, _MainTex en los antiguos)
    public static Material ParticleMaterial(Material template)
    {
        if (template == null) return null;
        var mat = new Material(template) { name = template.name + " (Fx)" };
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", SoftDot);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", SoftDot);
        return mat;
    }

    public static LineRenderer NewLine(Transform parent, string name, Material material, float width, bool loop = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = 2;
        line.widthMultiplier = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        if (material != null) line.sharedMaterial = material;
        line.enabled = false;
        return line;
    }

    // Arco de circunferencia en el plano perpendicular a normal (fill 1 = vuelta completa, empezando arriba y en
    // el sentido de las agujas del reloj visto desde normal)
    public static void SetArc(LineRenderer line, Vector3 center, Vector3 normal, float radius, float fill, int segments = 48)
    {
        fill = Mathf.Clamp01(fill);
        int count = Mathf.Max(2, Mathf.CeilToInt(segments * fill) + 1);
        line.positionCount = count;
        Vector3 n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.forward;
        Vector3 up = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        Vector3 right = Vector3.Cross(up, n).normalized;
        up = Vector3.Cross(n, right).normalized;
        for (int i = 0; i < count; i++)
        {
            float a = (i / (float)(count - 1)) * fill * Mathf.PI * 2f;
            line.SetPosition(i, center + (up * Mathf.Cos(a) + right * Mathf.Sin(a)) * radius);
        }
    }

    public static void SetColor(LineRenderer line, Color color)
    {
        line.startColor = line.endColor = color;
    }

    // Sistema de partículas mínimo, en espacio del mundo y sin emitir solo: quien lo usa llama a Emit o
    // cambia emission.rateOverTime
    public static ParticleSystem NewParticles(Transform parent, string name, Material material, int maxParticles = 200)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;
        main.startSpeed = 0f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ps.Play();
        return ps;
    }

    // Cartel centrado en pantalla, en un lienzo propio por encima del HUD (no hay que tocar el Canvas de la escena)
    public static TMP_Text NewScreenHint(Transform parent, string name, TMP_FontAsset font, Color color, float y, float fontSize = 64f, int sortingOrder = 60)
    {
        var canvasGo = new GameObject(name + "Canvas");
        canvasGo.transform.SetParent(parent, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var textGo = new GameObject(name);
        textGo.transform.SetParent(canvasGo.transform, false);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.outlineWidth = 0.25f;
        text.outlineColor = Color.black;
        text.raycastTarget = false;
        RectTransform rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(1400f, 140f);
        text.enabled = false;
        return text;
    }
}
