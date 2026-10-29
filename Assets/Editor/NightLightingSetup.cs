using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Herramienta de editor: monta la iluminación nocturna del puesto de forma reproducible.
// Menú: Tools > Fairground > Setup Night Lighting. Se puede volver a ejecutar: rehace el prefab desde cero.
public static class NightLightingSetup
{
    private const string PrefabPath = "Assets/Prefabs/Enviroments/Lighting_Night.prefab";
    private const string ProfilePath = "Assets/Settings/VP_Night.asset";

    [MenuItem("Tools/Fairground/Setup Night Lighting")]
    public static void Setup()
    {
        VolumeProfile profile = CreateOrUpdateProfile();
        BuildLightingPrefab(profile);
        ConfigureSceneAndCamera();
        Debug.Log("[NightLightingSetup] Iluminación nocturna configurada.");
    }

    // ---------- 1. Perfil de post-procesado ----------
    private static VolumeProfile CreateOrUpdateProfile()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        Bloom bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(1.1f);   // solo brilla lo que pasa de 1 (bombillas HDR)
        bloom.intensity.Override(0.8f);
        bloom.scatter.Override(0.65f);

        Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.Neutral);   // Neutral respeta los colores planos; ACES los desplaza

        ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
        color.contrast.Override(10f);
        color.saturation.Override(10f);

        Vignette vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.Override(0.25f);
        vignette.smoothness.Override(0.4f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component))
            return component;

        component = profile.Add<T>(false);
        component.name = typeof(T).Name;
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile);   // los efectos viven como sub-assets del perfil
        return component;
    }

    // ---------- 2. Prefab con las luces y el volumen ----------
    private static void BuildLightingPrefab(VolumeProfile profile)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        // Idempotente: borra lo anterior y lo vuelve a crear
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

        Color stageWarm = new Color(1f, 0.86f, 0.68f);
        Color bulbWarm = new Color(1f, 0.62f, 0.3f);
        Color moonCool = new Color(0.6f, 0.7f, 1f);

        // Focos de escenario: detrás del faldón (z = 0) para que no les haga sombra.
        // Apuntan recto (x = ±3) con cono amplio para cubrir también los carriles de los extremos (±5,3 m)
        AddLight(root, "StageSpot_L", LightType.Spot, new Vector3(-3f, 3.9f, 0.5f), new Vector3(35f, 8f, 0f),
                 stageWarm, 40f, 14f, LightShadows.Soft, 0.6f, 75f, 88f);
        AddLight(root, "StageSpot_R", LightType.Spot, new Vector3(3f, 3.9f, 0.5f), new Vector3(35f, -8f, 0f),
                 stageWarm, 40f, 14f, LightShadows.Soft, 0.6f, 75f, 88f);

        // Relleno cálido junto a las bombillas (la emisión no ilumina en tiempo real).
        // Separadas 1,4 m de la pared para no quemarla; las delanteras iluminan además los banderines por delante
        AddLight(root, "WallGlow_L1", LightType.Point, new Vector3(-4.6f, 3.6f, -2.5f), Vector3.zero, bulbWarm, 2f, 6f, LightShadows.None);
        AddLight(root, "WallGlow_R1", LightType.Point, new Vector3(4.6f, 3.6f, -2.5f), Vector3.zero, bulbWarm, 2f, 6f, LightShadows.None);
        AddLight(root, "WallGlow_L2", LightType.Point, new Vector3(-4.6f, 3.7f, 3.5f), Vector3.zero, bulbWarm, 2f, 6f, LightShadows.None);
        AddLight(root, "WallGlow_R2", LightType.Point, new Vector3(4.6f, 3.7f, 3.5f), Vector3.zero, bulbWarm, 2f, 6f, LightShadows.None);

        // Halo suave alrededor de la luna del decorado
        AddLight(root, "MoonGlow", LightType.Point, new Vector3(3.9f, 3.55f, 5.5f), Vector3.zero, moonCool, 0.8f, 3f, LightShadows.None);

        // Luz de luna: el techo le hace sombra, así que solo afecta al exterior
        AddLight(root, "Moonlight", LightType.Directional, Vector3.zero, new Vector3(40f, -35f, 0f),
                 new Color(0.55f, 0.65f, 1f), 0.25f, 0f, LightShadows.Soft);

        // Volumen global con el perfil nocturno
        GameObject volumeGO = new GameObject("GlobalVolume");
        volumeGO.transform.SetParent(root.transform, false);
        Volume volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void AddLight(GameObject parent, string name, LightType type, Vector3 position, Vector3 euler,
                                 Color color, float intensity, float range, LightShadows shadows,
                                 float shadowStrength = 1f, float innerAngle = 0f, float outerAngle = 0f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = position;
        go.transform.localEulerAngles = euler;

        Light light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.shadows = shadows;
        light.shadowStrength = shadowStrength;

        if (type != LightType.Directional)
            light.range = range;

        if (type == LightType.Spot)
        {
            // Primero el exterior: Unity limita el ángulo interior al exterior actual,
            // y si se asigna antes queda recortado al valor por defecto (30°)
            light.spotAngle = outerAngle;
            light.innerSpotAngle = innerAngle;
        }
    }

    // ---------- 3. Escena: ambiente y cámara ----------
    private static void ConfigureSceneAndCamera()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.10f, 0.12f, 0.24f);
        RenderSettings.ambientEquatorColor = new Color(0.10f, 0.08f, 0.14f);
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.03f, 0.04f);

        Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.09f);

            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;

            EditorUtility.SetDirty(cam);
            EditorUtility.SetDirty(data);
            Debug.Log($"[NightLightingSetup] Cámara configurada: {cam.name}");
        }
        else
        {
            Debug.LogWarning("[NightLightingSetup] No se ha encontrado ninguna cámara en la escena.");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
