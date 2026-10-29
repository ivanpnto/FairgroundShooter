using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Monta el nivel 2 (fondo marino). Menú: Tools > Fairground > Setup Sea Level.
// Se puede repetir: rehace los prefabs generados, carriles y referencias.
public static class SeaLevelSetup
{
    private const string PrefabFolder = "Assets/Prefabs/Sea";
    private const string ModelsFolder = "Assets/Art/Models/Targets";
    private const string EnvModel = "Assets/Art/Models/Environments/Env_Sea.fbx";
    private const string EnvPrefab = "Assets/Prefabs/Enviroments/Env_Sea.prefab";
    private const string LightingPrefab = "Assets/Prefabs/Enviroments/Lighting_Sea.prefab";
    private const string ProfilePath = "Assets/Settings/VP_Sea.asset";
    private const string Level2Path = "Assets/Data/Levels/Level2_FondoMarino.asset";

    [MenuItem("Tools/Fairground/Setup Sea Level")]
    public static void Setup()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Sea");

        GameObject env = PlaceEnvironment();
        GameObject lighting = PlaceLighting();
        AddAtmosphereToNight();

        GameObject fish = BuildTarget("FishSwimTarget", "Target_Fish", 15, 6f, 1, typeof(SenoMovement), false);
        SetFloats(fish.GetComponent<SenoMovement>(), ("horizontalSpeed", 2.2f), ("verticalAmplitude", 0.3f), ("frequency", 2.5f));
        PrefabUtility.SavePrefabAsset(fish);

        GameObject jelly = BuildTarget("JellyTarget", "Target_Jelly", 25, 7f, 1, typeof(PulseMovement), true);
        GameObject turtle = BuildTarget("TurtleTarget", "Target_Turtle", 60, 10f, 3, typeof(HorizontalMovement), false);
        SetFloats(turtle.GetComponent<HorizontalMovement>(), ("speed", 1.0f), ("distance", 3.2f));
        PrefabUtility.SavePrefabAsset(turtle);
        GameObject tyre = BuildTarget("TyreTarget", "Target_Tyre", -30, 8f, 1, typeof(SinkMovement), false);

        CreateLanes();
        UpdateLevel2(fish, jelly, turtle, tyre);
        RegisterEnvironment(env, lighting);

        // En el editor se ve el estanque; el LevelManager enciende el mar al llegar al nivel 2
        env.SetActive(false);
        lighting.SetActive(false);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[SeaLevelSetup] Nivel 2 (fondo marino) configurado.");
    }

    // ---------- Decorado ----------
    private static GameObject PlaceEnvironment()
    {
        GameObject existing = FindRoot("Env_Sea");
        if (existing != null)
            return existing;

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(EnvModel);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "Env_Sea";
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;   // misma razón: el FBX trae la raíz girada 180º

        // La superficie y las algas están en el camino de los focos: sin esto dejan en sombra la zona de juego
        foreach (MeshRenderer mr in instance.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.name.Contains("Surface") || mr.name.Contains("Kelp"))
                mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Estático todo salvo lo que se podría animar (algas y superficie)
        foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
        {
            bool animated = t.name.Contains("Kelp") || t.name.Contains("Surface");
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, animated ? 0 : StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(instance, EnvPrefab, InteractionMode.AutomatedAction);
        Undo.RegisterCreatedObjectUndo(instance, "Place Env_Sea");
        return instance;
    }

    // ---------- Iluminación submarina ----------
    private static GameObject PlaceLighting()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        Bloom bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(1.1f); bloom.intensity.Override(0.9f); bloom.scatter.Override(0.7f);
        GetOrAdd<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);
        ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
        color.contrast.Override(12f); color.saturation.Override(15f);
        color.colorFilter.Override(new Color(0.85f, 1f, 1.05f));   // tinte frío y verdoso de agua
        Vignette vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.Override(0.3f); vignette.smoothness.Override(0.45f);
        EditorUtility.SetDirty(profile);

        GameObject root = FindRoot("Lighting_Sea");
        if (root != null)
            Object.DestroyImmediate(root);

        root = new GameObject("Lighting_Sea");
        Color water = new Color(0.72f, 0.94f, 1f);
        Color bulb = new Color(1f, 0.62f, 0.3f);
        Color cyan = new Color(0.35f, 0.8f, 1f);

        // Mismos focos que la noche (ya ajustados) pero con luz fría de agua
        AddLight(root, "StageSpot_L", LightType.Spot, new Vector3(-3f, 3.9f, 0.5f), new Vector3(35f, 8f, 0f), water, 32f, 14f, LightShadows.Soft, 0.6f, 75f, 88f);
        AddLight(root, "StageSpot_R", LightType.Spot, new Vector3(3f, 3.9f, 0.5f), new Vector3(35f, -8f, 0f), water, 32f, 14f, LightShadows.Soft, 0.6f, 75f, 88f);
        // Las bombillas del puesto siguen siendo cálidas
        AddLight(root, "WallGlow_L1", LightType.Point, new Vector3(-4.6f, 3.6f, -2.5f), Vector3.zero, bulb, 2f, 6f, LightShadows.None);
        AddLight(root, "WallGlow_R1", LightType.Point, new Vector3(4.6f, 3.6f, -2.5f), Vector3.zero, bulb, 2f, 6f, LightShadows.None);
        // Resplandor de la superficie sobre la zona de juego
        AddLight(root, "SurfaceGlow_L", LightType.Point, new Vector3(-2.5f, 3.9f, 4f), Vector3.zero, cyan, 3f, 7f, LightShadows.None);
        AddLight(root, "SurfaceGlow_R", LightType.Point, new Vector3(2.5f, 3.9f, 4f), Vector3.zero, cyan, 3f, 7f, LightShadows.None);

        GameObject volumeGO = new GameObject("GlobalVolume");
        volumeGO.transform.SetParent(root.transform, false);
        Volume volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;

        EnvironmentAtmosphere atmosphere = root.AddComponent<EnvironmentAtmosphere>();
        SerializedObject so = new SerializedObject(atmosphere);
        so.FindProperty("skyColor").colorValue = new Color(0.05f, 0.16f, 0.26f);
        so.FindProperty("equatorColor").colorValue = new Color(0.04f, 0.10f, 0.16f);
        so.FindProperty("groundColor").colorValue = new Color(0.02f, 0.04f, 0.06f);
        so.FindProperty("fog").boolValue = true;   // la niebla azul hace que lo lejano se vea "bajo el agua"
        so.FindProperty("fogStart").floatValue = 9f;   // empieza tras los objetivos (8-10 m) para no lavarlos
        so.FindProperty("fogEnd").floatValue = 30f;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAssetAndConnect(root, LightingPrefab, InteractionMode.AutomatedAction);
        return root;
    }

    // Al volver al nivel 1 (reiniciar partida) hay que restaurar el ambiente nocturno y quitar la niebla
    private static void AddAtmosphereToNight()
    {
        GameObject night = FindRoot("Lighting_Night");
        if (night == null || night.GetComponent<EnvironmentAtmosphere>() != null)
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enviroments/Lighting_Night.prefab");
        GameObject contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
        contents.AddComponent<EnvironmentAtmosphere>();   // valores por defecto = ambiente nocturno actual, sin niebla
        PrefabUtility.SaveAsPrefabAsset(contents, AssetDatabase.GetAssetPath(prefab));
        PrefabUtility.UnloadPrefabContents(contents);
    }

    // ---------- Objetivos ----------
    private static GameObject BuildTarget(string prefabName, string modelName, int points, float lifetime, int hits,
                                          System.Type movementType, bool colliderTopOnly)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsFolder}/{modelName}.fbx");
        GameObject root = new GameObject(prefabName);
        GameObject child = (GameObject)PrefabUtility.InstantiatePrefab(model);
        child.transform.SetParent(root.transform, false);
        // El FBX trae su raíz girada 180º en Y: sin esto el recorte mira hacia atrás y se ve
        // la capa del contorno (oscura) en lugar de la cara pintada
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;

        // Collider a partir del tamaño del modelo (algo más estrecho que la silueta, como en los del nivel 1)
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        foreach (Renderer r in child.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }

        Vector3 size = new Vector3(b.size.x * 0.85f, b.size.y * 0.85f, 0.2f);
        Vector3 center = new Vector3(b.center.x, b.center.y, 0f);

        if (colliderTopOnly)   // medusa: solo el sombrero, no los tentáculos
        {
            size.y = b.size.y * 0.5f;
            center.y = b.max.y - size.y * 0.5f;
        }

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.size = size;
        box.center = center;

        Target target = root.AddComponent<Target>();
        SerializedObject so = new SerializedObject(target);
        so.FindProperty("points").intValue = points;
        so.FindProperty("lifetime").floatValue = lifetime;
        so.FindProperty("hitsToKill").intValue = hits;
        so.FindProperty("haloScale").floatValue = 1.12f;
        so.ApplyModifiedPropertiesWithoutUndo();

        root.AddComponent(movementType);

        string path = $"{PrefabFolder}/{prefabName}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void SetFloats(Object component, params (string name, float value)[] values)
    {
        SerializedObject so = new SerializedObject(component);
        foreach (var (name, value) in values)
            so.FindProperty(name).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Carriles ----------
    private static readonly (string id, Vector3[] points)[] Lanes =
    {
        ("Sea_Fish",   new[] { new Vector3(-6.8f, 1.9f, 3.3f), new Vector3(6.8f, 2.5f, 3.3f) }),
        ("Sea_Jelly",  new[] { new Vector3(-4f, 0.6f, 3.9f), new Vector3(-1.5f, 0.6f, 3.9f), new Vector3(1.5f, 0.6f, 3.9f), new Vector3(4f, 0.6f, 3.9f) }),
        ("Sea_Turtle", new[] { new Vector3(-1.5f, 1.7f, 3.05f), new Vector3(1.5f, 1.7f, 3.05f) }),
        ("Sea_Tyre",   new[] { new Vector3(-3.5f, 3.35f, 2.9f), new Vector3(0f, 3.35f, 2.9f), new Vector3(3.5f, 3.35f, 2.9f) }),
    };

    private static void CreateLanes()
    {
        GameObject spawnRoot = FindRoot("TargetSpawnPoints");
        Transform seaRoot = spawnRoot.transform.Find("Sea");

        if (seaRoot != null)
            Object.DestroyImmediate(seaRoot.gameObject);

        seaRoot = new GameObject("Sea").transform;
        seaRoot.SetParent(spawnRoot.transform, false);

        foreach (var (id, positions) in Lanes)
        {
            GameObject laneGO = new GameObject("Lane_" + id);
            laneGO.transform.SetParent(seaRoot, false);

            List<Transform> points = new List<Transform>();
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject p = new GameObject($"SP_{id}_{i + 1}");
                p.transform.SetParent(laneGO.transform, false);
                p.transform.position = positions[i];
                points.Add(p.transform);
            }

            SpawnLane lane = laneGO.AddComponent<SpawnLane>();
            SerializedObject so = new SerializedObject(lane);
            so.FindProperty("laneId").stringValue = id;
            SerializedProperty prop = so.FindProperty("points");
            prop.arraySize = points.Count;
            for (int i = 0; i < points.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ---------- Nivel 2 ----------
    // El LevelConfig del nivel 2 era provisional (estanque): aquí se rehace con el mar y sus objetivos
    private static void UpdateLevel2(GameObject fish, GameObject jelly, GameObject turtle, GameObject tyre)
    {
        LevelConfig level = AssetDatabase.LoadAssetAtPath<LevelConfig>(Level2Path);
        if (level == null)
        {
            Debug.LogError("[SeaLevelSetup] No existe Level2_FondoMarino; ejecuta antes Setup Levels.");
            return;
        }

        level.levelName = "Fondo marino";
        level.environmentId = "Sea";
        level.spawnInterval = 1.1f;
        level.maxActiveTargets = 6;
        level.pointsMultiplier = 1.5f;
        level.speedMultiplier = 1.15f;
        level.lifetimeMultiplier = 0.9f;
        level.timeBonusChance = 0.04f;
        level.ammoBonusChance = 0.04f;
        level.timeBonusSeconds = 3f;
        level.entries = new[]
        {
            new LevelConfig.SpawnEntry { label = "Pez payaso", prefab = fish,   laneId = "Sea_Fish",   weight = 3f,   maxActive = 2 },
            new LevelConfig.SpawnEntry { label = "Medusa",     prefab = jelly,  laneId = "Sea_Jelly",  weight = 2.5f, maxActive = 2 },
            new LevelConfig.SpawnEntry { label = "Tortuga",    prefab = turtle, laneId = "Sea_Turtle", weight = 1f,   maxActive = 1 },
            new LevelConfig.SpawnEntry { label = "Neumatico",  prefab = tyre,   laneId = "Sea_Tyre",   weight = 1.5f, maxActive = 1 },
        };
        EditorUtility.SetDirty(level);
    }

    private static void RegisterEnvironment(GameObject env, GameObject lighting)
    {
        LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
        SerializedObject so = new SerializedObject(lm);
        SerializedProperty envs = so.FindProperty("environments");

        int index = -1;
        for (int i = 0; i < envs.arraySize; i++)
            if (envs.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == "Sea") index = i;

        if (index < 0)
        {
            index = envs.arraySize;
            envs.arraySize++;
        }

        SerializedProperty sea = envs.GetArrayElementAtIndex(index);
        sea.FindPropertyRelative("id").stringValue = "Sea";
        SerializedProperty objs = sea.FindPropertyRelative("objects");
        objs.arraySize = 2;
        objs.GetArrayElementAtIndex(0).objectReferenceValue = env;
        objs.GetArrayElementAtIndex(1).objectReferenceValue = lighting;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Utilidades ----------
    private static GameObject FindRoot(string name)
    {
        foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component))
            return component;

        component = profile.Add<T>(false);
        component.name = typeof(T).Name;
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
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
        if (type != LightType.Directional) light.range = range;
        if (type == LightType.Spot)
        {
            light.spotAngle = outerAngle;       // primero el exterior (ver NightLightingSetup)
            light.innerSpotAngle = innerAngle;
        }
    }
}
