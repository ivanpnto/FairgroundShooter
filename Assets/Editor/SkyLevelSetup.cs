using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Monta el nivel 3 (cielo al atardecer, I6.1). Menú: Tools > Fairground > Setup Sky Level.
// Igual que las herramientas del bosque y del mar: se puede repetir y rehace prefabs, carriles,
// iluminación, el LevelConfig y su registro en el LevelManager.
//
// Objetivos:
//   Águila     DiveMovement (picado)                40 pts
//   Bandada    4 pájaros pequeños en formación V      15 pts cada uno (SpawnEntry.groupSize)
//   Globos     suben desde detrás de las nubes        20 pts (SinkMovement con velocidad negativa)
//   Avioneta   cruza despacio con cartel de prohibido −40 pts (penalización)
//
// Banda LEJANA (más al fondo, z ≈ 4,8, y más pequeños): águila 70, bandada de 3 a 25 cada uno, globos 35.
// Más puntos por ser más difíciles de acertar; le dan uso al Barrett (mira y daño 3) y a la bala explosiva.
// Son ~1/3 de las apariciones normales: el nivel se puede jugar con el arma inicial.
public static class SkyLevelSetup
{
    private const string PrefabFolder = "Assets/Prefabs/Sky";
    private const string ModelsFolder = "Assets/Art/Models/Targets";
    private const string MatFolder = "Assets/Art/Materials/Sky";
    private const string TargetMatFolder = "Assets/Art/Materials/Targets";
    private const string EnvModel = "Assets/Art/Models/Environments/Env_Sky.fbx";
    private const string EnvPrefab = "Assets/Prefabs/Enviroments/Env_Sky.prefab";
    private const string LightingPrefab = "Assets/Prefabs/Enviroments/Lighting_Sky.prefab";
    private const string ProfilePath = "Assets/Settings/VP_Sky.asset";
    private const string Level2Path = "Assets/Data/Levels/Level2_FondoMarino.asset";
    private const string Level3Path = "Assets/Data/Levels/Level3_Cielo.asset";

    [MenuItem("Tools/Fairground/Setup Sky Level")]
    public static void Setup()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Sky");

        SetupBackdropMaterials();
        GameObject env = PlaceEnvironment();
        GameObject lighting = PlaceLighting();

        // ---- Objetivos
        GameObject eagle = BuildTarget("EagleTarget", "Target_Eagle", 40, 7f, typeof(DiveMovement), 1f, 0.8f);
        AddOscillator(eagle, "Target_Eagle_Wing", IdleOscillator.Channel.Rotation, Vector3.forward, 14f, 1.3f);

        // Pájaro pequeño de la bandada: el modelo del bosque, más pequeño y en azul
        GameObject flock = BuildTarget("FlockBirdTarget", "Target_Bird", 15, 6.5f, typeof(SenoMovement), 0.6f, 0.9f);
        SetFloats(flock.GetComponent<SenoMovement>(), ("horizontalSpeed", 2.8f), ("verticalAmplitude", 0.22f), ("frequency", 3.2f));
        AddOscillator(flock, "Target_Bird_Wing", IdleOscillator.Channel.Rotation, Vector3.forward, 34f, 5f);
        SwapMaterial(flock, "M_T_BirdPink", MaterialVariant("M_T_BirdBlue", "M_T_BirdPink", new Color(0.20f, 0.45f, 0.95f)));

        var balloons = new List<GameObject>();
        foreach (var (name, matName, color) in new[]
        {
            ("BalloonRedTarget", "M_T_BalloonRed", new Color(0.88f, 0.10f, 0.14f)),
            ("BalloonYellowTarget", "M_T_BalloonYellow", new Color(0.98f, 0.80f, 0.12f)),
            ("BalloonBlueTarget", "M_T_BalloonBlue", new Color(0.15f, 0.45f, 0.92f)),
        })
        {
            GameObject b = BuildTarget(name, "Target_Balloon", 20, 6.5f, typeof(SinkMovement), 1f, 0.85f, colliderTopFraction: 0.62f);
            // SinkMovement con velocidad negativa = sube; sin giro, con vaivén lateral
            SetFloats(b.GetComponent<SinkMovement>(), ("sinkSpeed", -0.55f), ("driftAmplitude", 0.22f), ("driftFrequency", 0.35f),
                      ("spinSpeed", 0f), ("minHeight", -10f));
            SwapMaterial(b, "M_T_BalloonRed", MaterialVariant(matName, "M_T_BalloonRed", color));
            balloons.Add(b);
        }

        // ---- Banda lejana: mismos modelos y movimientos, más pequeños y al fondo.
        // Tamaño mínimo orientativo: ~3° de ancho en pantalla para que se pueda acertar sin mira
        // (globos 0,85 y pájaros 0,5 con hitbox completa; con menos, apuntar sin mira se vuelve lotería)
        GameObject farEagle = BuildTarget("FarEagleTarget", "Target_Eagle", 70, 7.5f, typeof(DiveMovement), 0.6f, 0.9f);
        SetFloats(farEagle.GetComponent<DiveMovement>(), ("cruiseSpeed", 2.0f), ("diveDepth", 1.3f));
        AddOscillator(farEagle, "Target_Eagle_Wing", IdleOscillator.Channel.Rotation, Vector3.forward, 14f, 1.3f);

        GameObject farFlock = BuildTarget("FarFlockBirdTarget", "Target_Bird", 25, 7f, typeof(SenoMovement), 0.5f, 1f);
        SetFloats(farFlock.GetComponent<SenoMovement>(), ("horizontalSpeed", 2.4f), ("verticalAmplitude", 0.16f), ("frequency", 3.2f));
        AddOscillator(farFlock, "Target_Bird_Wing", IdleOscillator.Channel.Rotation, Vector3.forward, 34f, 5f);
        SwapMaterial(farFlock, "M_T_BirdPink", MaterialVariant("M_T_BirdBlue", "M_T_BirdPink", new Color(0.20f, 0.45f, 0.95f)));

        var farBalloons = new List<GameObject>();
        foreach (var (name, matName, color) in new[]
        {
            ("FarBalloonRedTarget", "M_T_BalloonRed", new Color(0.88f, 0.10f, 0.14f)),
            ("FarBalloonYellowTarget", "M_T_BalloonYellow", new Color(0.98f, 0.80f, 0.12f)),
            ("FarBalloonBlueTarget", "M_T_BalloonBlue", new Color(0.15f, 0.45f, 0.92f)),
        })
        {
            GameObject b = BuildTarget(name, "Target_Balloon", 35, 7f, typeof(SinkMovement), 0.85f, 1f, colliderTopFraction: 0.62f);
            SetFloats(b.GetComponent<SinkMovement>(), ("sinkSpeed", -0.5f), ("driftAmplitude", 0.18f), ("driftFrequency", 0.35f),
                      ("spinSpeed", 0f), ("minHeight", -10f));
            SwapMaterial(b, "M_T_BalloonRed", MaterialVariant(matName, "M_T_BalloonRed", color));
            farBalloons.Add(b);
        }

        GameObject plane = BuildTarget("PlaneTarget", "Target_Plane", -40, 9f, typeof(SenoMovement), 1f, 0.85f);
        SetFloats(plane.GetComponent<SenoMovement>(), ("horizontalSpeed", 1.5f), ("verticalAmplitude", 0.08f), ("frequency", 2f));
        AddOscillator(plane, "Target_Plane_Prop", IdleOscillator.Channel.Scale, Vector3.up, 0.45f, 11f);
        AddOscillator(plane, "Target_Plane_Banner", IdleOscillator.Channel.Rotation, Vector3.forward, 4f, 1.4f);

        CreateLanes();
        LevelConfig level = CreateLevel3(eagle, flock, balloons, plane, farEagle, farFlock, farBalloons);
        RegisterLevel(level, env, lighting);
        AddToSceneryColliders();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[SkyLevelSetup] Nivel 3 (cielo) configurado.");
    }

    // ---------- Materiales del telón: Unlit, para que los focos no lo manchen (como el cielo del bosque) ----------
    private static void SetupBackdropMaterials()
    {
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        foreach (string name in new[] { "M_K_Sky1", "M_K_Sky2", "M_K_Sky3", "M_K_Sky4", "M_K_Sky5", "M_K_Sun", "M_K_SunHalo", "M_K_Star", "M_K_HillFar" })
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/{name}.mat");
            if (m == null) continue;
            Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
            if (m.shader != unlit) m.shader = unlit;
            // El sol y su halo con brillo HDR: el Bloom los convierte en resplandor
            float boost = name == "M_K_Sun" ? 2.2f : name == "M_K_SunHalo" ? 1.3f : name == "M_K_Star" ? 1.8f : 1f;
            m.SetColor("_BaseColor", new Color(Mathf.Min(c.r, 1f) * boost, Mathf.Min(c.g, 1f) * boost, Mathf.Min(c.b, 1f) * boost, 1f));
            EditorUtility.SetDirty(m);
        }
    }

    // ---------- Decorado ----------
    private static GameObject PlaceEnvironment()
    {
        GameObject existing = FindRoot("Env_Sky");
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(EnvModel);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "Env_Sky";
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;   // el FBX trae la raíz girada 180º

        foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
        {
            bool moving = t.name.StartsWith("Sky_Balloon");
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, moving ? 0 : StaticEditorFlags.BatchingStatic);
        }

        foreach (MeshRenderer mr in instance.GetComponentsInChildren<MeshRenderer>(true))
        {
            // Las nubes de arriba están en el camino de los focos: sin sombras sobre la zona de juego.
            // El telón y lo lejano tampoco proyectan (no hay nada detrás que sombrear)
            if (!mr.name.Contains("FrontClouds") && !mr.name.Contains("MidClouds"))
                mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Globos aerostáticos del decorado: suben y bajan despacio, cada uno a su ritmo
        float[] amp = { 0.12f, 0.09f, 0.06f };
        float[] freq = { 0.22f, 0.3f, 0.27f };
        for (int i = 0; i < 3; i++)
        {
            Transform b = FindDeep(instance.transform, $"Sky_Balloon_{i + 1}");
            if (b == null) continue;
            IdleOscillator osc = b.gameObject.AddComponent<IdleOscillator>();
            SerializedObject so = new SerializedObject(osc);
            so.FindProperty("channel").enumValueIndex = (int)IdleOscillator.Channel.Position;
            so.FindProperty("axis").vector3Value = Vector3.up;
            so.FindProperty("amplitude").floatValue = amp[i];
            so.FindProperty("frequency").floatValue = freq[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        PrefabUtility.SaveAsPrefabAssetAndConnect(instance, EnvPrefab, InteractionMode.AutomatedAction);
        instance.SetActive(false);   // el prefab queda activo; la instancia de la escena la enciende el LevelManager en el nivel 3
        return instance;
    }

    // ---------- Iluminación de atardecer ----------
    private static GameObject PlaceLighting()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        Bloom bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(1.05f); bloom.intensity.Override(1.0f); bloom.scatter.Override(0.75f);
        GetOrAdd<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);
        ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
        color.contrast.Override(10f); color.saturation.Override(12f);
        color.colorFilter.Override(new Color(1f, 0.93f, 0.88f));   // tinte cálido de atardecer
        Vignette vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.Override(0.28f); vignette.smoothness.Override(0.45f);
        EditorUtility.SetDirty(profile);

        GameObject root = FindRoot("Lighting_Sky");
        if (root != null)
            Object.DestroyImmediate(root);

        root = new GameObject("Lighting_Sky");
        Color warm = new Color(1f, 0.80f, 0.62f);
        Color bulb = new Color(1f, 0.62f, 0.3f);
        Color sunset = new Color(1f, 0.55f, 0.35f);

        // Mismos focos que los otros niveles pero más suaves (20 en vez de 32): las nubes son claras y se quemaban
        AddLight(root, "StageSpot_L", LightType.Spot, new Vector3(-3f, 3.9f, 0.5f), new Vector3(35f, 8f, 0f), warm, 20f, 14f, LightShadows.Soft, 0.55f, 75f, 88f);
        AddLight(root, "StageSpot_R", LightType.Spot, new Vector3(3f, 3.9f, 0.5f), new Vector3(35f, -8f, 0f), warm, 20f, 14f, LightShadows.Soft, 0.55f, 75f, 88f);
        AddLight(root, "WallGlow_L1", LightType.Point, new Vector3(-4.6f, 3.6f, -2.5f), Vector3.zero, bulb, 2f, 6f, LightShadows.None);
        AddLight(root, "WallGlow_R1", LightType.Point, new Vector3(4.6f, 3.6f, -2.5f), Vector3.zero, bulb, 2f, 6f, LightShadows.None);
        // Contraluz del sol: tiñe de naranja lo que está cerca del telón
        AddLight(root, "SunBacklight", LightType.Point, new Vector3(1.7f, 1.4f, 5.2f), Vector3.zero, sunset, 4f, 6f, LightShadows.None);

        GameObject volumeGO = new GameObject("GlobalVolume");
        volumeGO.transform.SetParent(root.transform, false);
        Volume volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;

        EnvironmentAtmosphere atmosphere = root.AddComponent<EnvironmentAtmosphere>();
        SerializedObject so = new SerializedObject(atmosphere);
        so.FindProperty("skyColor").colorValue = new Color(0.42f, 0.26f, 0.42f);
        so.FindProperty("equatorColor").colorValue = new Color(0.50f, 0.30f, 0.28f);
        so.FindProperty("groundColor").colorValue = new Color(0.12f, 0.07f, 0.10f);
        so.FindProperty("fog").boolValue = true;               // bruma rosada: da profundidad a lo lejano
        so.FindProperty("fogColor").colorValue = new Color(0.85f, 0.55f, 0.55f);
        so.FindProperty("fogStart").floatValue = 12f;           // empieza tras la banda lejana (~10 m) para no lavarla
        so.FindProperty("fogEnd").floatValue = 40f;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAssetAndConnect(root, LightingPrefab, InteractionMode.AutomatedAction);
        root.SetActive(false);
        return root;
    }

    // ---------- Objetivos ----------
    private static GameObject BuildTarget(string prefabName, string modelName, int points, float lifetime,
                                          System.Type movementType, float scale, float colliderFraction, float colliderTopFraction = 1f)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsFolder}/{modelName}.fbx");
        GameObject root = new GameObject(prefabName);
        GameObject child = (GameObject)PrefabUtility.InstantiatePrefab(model);
        child.transform.SetParent(root.transform, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;   // el FBX trae la raíz girada 180º

        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        foreach (Renderer r in child.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }

        // Algo más estrecho que la silueta, como en el resto de niveles. Los globos solo por el globo, no por la cuerda
        Vector3 size = new Vector3(b.size.x * colliderFraction, b.size.y * colliderFraction * colliderTopFraction, 0.2f);
        Vector3 center = new Vector3(b.center.x, colliderTopFraction < 1f ? b.max.y - size.y * 0.5f - b.size.y * 0.04f : b.center.y, 0f);

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.size = size;
        box.center = center;

        Target target = root.AddComponent<Target>();
        SerializedObject so = new SerializedObject(target);
        so.FindProperty("points").intValue = points;
        so.FindProperty("lifetime").floatValue = lifetime;
        so.FindProperty("health").intValue = 1;
        so.FindProperty("haloScale").floatValue = 1.12f;
        so.FindProperty("destroyDelay").floatValue = 1.1f;   // tiempo para TargetFall
        so.ApplyModifiedPropertiesWithoutUndo();

        root.AddComponent(movementType);
        root.AddComponent<TargetFall>();
        root.AddComponent<TargetAppear>();
        root.transform.localScale = Vector3.one * scale;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{prefabName}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void SetFloats(Component component, params (string name, float value)[] values)
    {
        string path = AssetDatabase.GetAssetPath(component.gameObject);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Component c = root.GetComponent(component.GetType());
        SerializedObject so = new SerializedObject(c);
        foreach (var (name, value) in values)
            so.FindProperty(name).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void AddOscillator(GameObject prefab, string partName, IdleOscillator.Channel channel, Vector3 axis, float amplitude, float frequency)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform part = FindDeep(root.transform, partName);
        if (part != null)
        {
            IdleOscillator osc = part.GetComponent<IdleOscillator>() ?? part.gameObject.AddComponent<IdleOscillator>();
            SerializedObject so = new SerializedObject(osc);
            so.FindProperty("channel").enumValueIndex = (int)channel;
            so.FindProperty("axis").vector3Value = axis;
            so.FindProperty("amplitude").floatValue = amplitude;
            so.FindProperty("frequency").floatValue = frequency;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else Debug.LogWarning($"[SkyLevelSetup] {prefab.name} no tiene la pieza {partName}.");
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // Copia de un material del objetivo con otro color (globo amarillo/azul, pájaro azul)
    private static Material MaterialVariant(string name, string source, Color color)
    {
        string path = $"{TargetMatFolder}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Material src = AssetDatabase.LoadAssetAtPath<Material>($"{TargetMatFolder}/{source}.mat");
            m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void SwapMaterial(GameObject prefab, string from, Material to)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name == from) { mats[i] = to; changed = true; }
            if (changed) r.sharedMaterials = mats;
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ---------- Carriles ----------
    private static readonly (string id, Vector3[] points)[] Lanes =
    {
        // Águila: entra alta por las paredes y cae en picado (hasta ~1,6 m más abajo)
        ("Sky_Eagle",   new[] { new Vector3(-6.8f, 3.0f, 3.4f), new Vector3(6.8f, 2.8f, 3.4f) }),
        // Bandada: más lejos (detrás de las nubes medias de los lados)
        ("Sky_Flock",   new[] { new Vector3(-6.8f, 2.3f, 4.4f), new Vector3(6.8f, 2.6f, 4.4f) }),
        // Avioneta: baja y despacio, por delante
        ("Sky_Plane",   new[] { new Vector3(-7.4f, 1.85f, 3.0f), new Vector3(7.4f, 2.05f, 3.0f) }),
        // Globos: suben desde detrás del banco de nubes delantero
        ("Sky_Balloon", new[] { new Vector3(-4f, 0.15f, 3.1f), new Vector3(-1.5f, 0.15f, 3.1f), new Vector3(1.5f, 0.15f, 3.1f), new Vector3(4f, 0.15f, 3.1f) }),
        // Banda lejana: delante de las nubes del fondo (5,0) y entre los globos de decorado (4,75–4,87)
        ("Sky_FarEagle",   new[] { new Vector3(-6.8f, 3.05f, 4.8f), new Vector3(6.8f, 2.9f, 4.8f) }),
        ("Sky_FarFlock",   new[] { new Vector3(-6.8f, 2.0f, 4.8f), new Vector3(6.8f, 2.2f, 4.8f) }),
        // Desde y 0: a esa profundidad, el banco de nubes delantero tapa todo lo que está por debajo de ~0,5 m
        ("Sky_FarBalloon", new[] { new Vector3(-3f, 0.0f, 4.8f), new Vector3(0f, 0.0f, 4.8f), new Vector3(3f, 0.0f, 4.8f) }),
    };

    private static void CreateLanes()
    {
        GameObject spawnRoot = FindRoot("TargetSpawnPoints");
        Transform skyRoot = spawnRoot.transform.Find("Sky");
        if (skyRoot != null)
            Object.DestroyImmediate(skyRoot.gameObject);

        skyRoot = new GameObject("Sky").transform;
        skyRoot.SetParent(spawnRoot.transform, false);

        foreach (var (id, positions) in Lanes)
        {
            GameObject laneGO = new GameObject("Lane_" + id);
            laneGO.transform.SetParent(skyRoot, false);

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

    // ---------- Nivel 3 ----------
    private static LevelConfig CreateLevel3(GameObject eagle, GameObject flock, List<GameObject> balloons, GameObject plane,
                                            GameObject farEagle, GameObject farFlock, List<GameObject> farBalloons)
    {
        LevelConfig level = AssetDatabase.LoadAssetAtPath<LevelConfig>(Level3Path);
        if (level == null)
        {
            level = ScriptableObject.CreateInstance<LevelConfig>();
            AssetDatabase.CreateAsset(level, Level3Path);
        }
        LevelConfig l2 = AssetDatabase.LoadAssetAtPath<LevelConfig>(Level2Path);

        level.levelName = "Cielo";
        level.environmentId = "Sky";
        level.duration = 60f;
        level.spawnInterval = 1.0f;
        level.endSpawnIntervalFactor = 0.55f;
        level.maxActiveTargets = 7;
        level.pointsMultiplier = 2f;
        level.speedMultiplier = 1.1f;   // 1,2 → 1,1 al añadir la banda lejana: no sumar tres dificultades a la vez
        level.lifetimeMultiplier = 0.85f;
        // 1,75: con el arma inicial hace falta la mejora de daño al máximo (1,75) para derribar de un disparo
        level.healthMultiplier = 1.75f;
        if (l2 != null)
        {
            level.timeBonusChance = l2.timeBonusChance;
            level.ammoBonusChance = l2.ammoBonusChance;
            level.timeBonusSeconds = l2.timeBonusSeconds;
        }
        level.entries = new[]
        {
            new LevelConfig.SpawnEntry { label = "Aguila",        prefab = eagle,       laneId = "Sky_Eagle",   weight = 2.2f, maxActive = 1 },
            new LevelConfig.SpawnEntry { label = "Bandada",       prefab = flock,       laneId = "Sky_Flock",   weight = 1.4f, maxActive = 4, groupSize = 4, groupSpacing = new Vector3(0.75f, 0.28f, 0f) },
            new LevelConfig.SpawnEntry { label = "Globo rojo",    prefab = balloons[0], laneId = "Sky_Balloon", weight = 1.1f, maxActive = 1 },
            new LevelConfig.SpawnEntry { label = "Globo amarillo",prefab = balloons[1], laneId = "Sky_Balloon", weight = 1.1f, maxActive = 1 },
            new LevelConfig.SpawnEntry { label = "Globo azul",    prefab = balloons[2], laneId = "Sky_Balloon", weight = 1.1f, maxActive = 1 },
            new LevelConfig.SpawnEntry { label = "Avioneta",      prefab = plane,       laneId = "Sky_Plane",   weight = 1.0f, maxActive = 1 },
            // Banda lejana (~35 % de las apariciones de objetivos normales)
            new LevelConfig.SpawnEntry { label = "Aguila lejana",  prefab = farEagle,       laneId = "Sky_FarEagle",   weight = 1.3f, maxActive = 1 },
            new LevelConfig.SpawnEntry { label = "Bandada lejana", prefab = farFlock,       laneId = "Sky_FarFlock",   weight = 0.9f, maxActive = 3, groupSize = 3, groupSpacing = new Vector3(0.55f, 0.2f, 0f) },
            new LevelConfig.SpawnEntry { label = "Globo lejano rojo",     prefab = farBalloons[0], laneId = "Sky_FarBalloon", weight = 0.4f, maxActive = 1 },
            new LevelConfig.SpawnEntry { label = "Globo lejano amarillo", prefab = farBalloons[1], laneId = "Sky_FarBalloon", weight = 0.4f, maxActive = 1 },
            new LevelConfig.SpawnEntry { label = "Globo lejano azul",     prefab = farBalloons[2], laneId = "Sky_FarBalloon", weight = 0.4f, maxActive = 1 },
        };
        EditorUtility.SetDirty(level);
        return level;
    }

    private static void RegisterLevel(LevelConfig level, GameObject env, GameObject lighting)
    {
        LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
        SerializedObject so = new SerializedObject(lm);

        // Nivel al final de la lista (se juega siempre, después del fondo marino)
        SerializedProperty levels = so.FindProperty("levels");
        bool present = false;
        for (int i = 0; i < levels.arraySize; i++)
            if (levels.GetArrayElementAtIndex(i).objectReferenceValue == level) present = true;
        if (!present)
        {
            levels.arraySize++;
            levels.GetArrayElementAtIndex(levels.arraySize - 1).objectReferenceValue = level;
        }

        SerializedProperty envs = so.FindProperty("environments");
        for (int i = envs.arraySize - 1; i >= 0; i--)
            if (envs.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == "Sky")
                envs.DeleteArrayElementAtIndex(i);
        envs.arraySize++;
        SerializedProperty sky = envs.GetArrayElementAtIndex(envs.arraySize - 1);
        sky.FindPropertyRelative("id").stringValue = "Sky";
        SerializedProperty objs = sky.FindPropertyRelative("objects");
        objs.arraySize = 2;
        objs.GetArrayElementAtIndex(0).objectReferenceValue = env;
        objs.GetArrayElementAtIndex(1).objectReferenceValue = lighting;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Hitbox del decorado (I4.5): misma herramienta que el resto de entornos
    private static void AddToSceneryColliders()
    {
        SceneryCollidersSetup.Setup();
    }

    // ---------- Utilidades ----------
    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
            component = profile.Add<T>(true);
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
            light.spotAngle = outerAngle;
            light.innerSpotAngle = innerAngle;
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static GameObject FindRoot(string name)
    {
        foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }
}
