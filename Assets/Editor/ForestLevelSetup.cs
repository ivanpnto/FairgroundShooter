using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Monta el nivel 1 (bosque nocturno) y retira el estanque. Menú: Tools > Fairground > Setup Forest Level.
// Se puede repetir: rehace los prefabs generados, carriles y referencias.
public static class ForestLevelSetup
{
    private const string PrefabFolder = "Assets/Prefabs/Forest";
    private const string ModelsFolder = "Assets/Art/Models/Targets";
    private const string EnvModel = "Assets/Art/Models/Environments/Env_Forest.fbx";
    private const string EnvPrefab = "Assets/Prefabs/Enviroments/Env_Forest.prefab";
    private const string Level1Path = "Assets/Data/Levels/Level1_Estanque.asset";
    private const string BirdPrefab = "Assets/Prefabs/SenoTarget.prefab";

    [MenuItem("Tools/Fairground/Setup Forest Level")]
    public static void Setup()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Forest");

        GameObject env = PlaceEnvironment();

        GameObject fox = BuildTarget("FoxTarget", "Target_Fox", 10, 6f, 1, typeof(SenoMovement), 1f);
        SetFloats(fox.GetComponent<SenoMovement>(), ("horizontalSpeed", 2.6f), ("verticalAmplitude", 0.06f), ("frequency", 14f));
        PrefabUtility.SavePrefabAsset(fox);

        // Zorro lejano: mismo modelo más pequeño (y a 10 m) = más difícil y más puntos
        GameObject farFox = BuildTarget("FarFoxTarget", "Target_Fox", 25, 7.5f, 1, typeof(SenoMovement), 0.8f);
        SetFloats(farFox.GetComponent<SenoMovement>(), ("horizontalSpeed", 2.0f), ("verticalAmplitude", 0.05f), ("frequency", 12f));
        PrefabUtility.SavePrefabAsset(farFox);

        GameObject rabbit = BuildTarget("RabbitTarget", "Target_Rabbit", 15, 7f, 1, typeof(JumpMovement), 1f);
        SetFloats(rabbit.GetComponent<JumpMovement>(), ("speed", 1.8f), ("jumpHeight", 1.2f), ("jumpDistance", 1.6f), ("movementLimit", 4f));
        PrefabUtility.SavePrefabAsset(rabbit);

        GameObject owl = BuildTarget("OwlTarget", "Target_Owl", 25, 6f, 1, typeof(PeekMovement), 1f);

        GameObject skunk = BuildTarget("SkunkTarget", "Target_Skunk", -30, 8f, 1, typeof(HorizontalMovement), 1f);
        SetFloats(skunk.GetComponent<HorizontalMovement>(), ("speed", 1.1f), ("distance", 3.6f));
        PrefabUtility.SavePrefabAsset(skunk);

        GameObject bird = AssetDatabase.LoadAssetAtPath<GameObject>(BirdPrefab);

        CreateLanes();
        UpdateLevel1(fox, farFox, rabbit, owl, skunk, bird);
        RegisterEnvironment(env);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[ForestLevelSetup] Nivel 1 (bosque nocturno) configurado.");
    }

    // ---------- Decorado ----------
    private static GameObject PlaceEnvironment()
    {
        GameObject existing = FindRoot("Env_Forest");
        if (existing != null)
            return existing;

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(EnvModel);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "Env_Forest";
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;   // el FBX trae la raíz girada 180º

        foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
        {
            bool animated = t.name.Contains("Fireflies") || t.name.Contains("Canopy");
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, animated ? 0 : StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        // Ramas y luciérnagas están en el camino de los focos: sin sombras para no oscurecer la zona de juego
        foreach (MeshRenderer mr in instance.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.name.Contains("Canopy") || mr.name.Contains("Fireflies") || mr.name.Contains("BigTrees"))
                mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        PrefabUtility.SaveAsPrefabAssetAndConnect(instance, EnvPrefab, InteractionMode.AutomatedAction);
        return instance;
    }

    // ---------- Objetivos ----------
    private static GameObject BuildTarget(string prefabName, string modelName, int points, float lifetime, int hits,
                                          System.Type movementType, float scale)
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

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(b.size.x * 0.8f, b.size.y * 0.85f, 0.2f);
        box.center = new Vector3(b.center.x, b.center.y, 0f);

        Target target = root.AddComponent<Target>();
        SerializedObject so = new SerializedObject(target);
        so.FindProperty("points").intValue = points;
        so.FindProperty("lifetime").floatValue = lifetime;
        so.FindProperty("hitsToKill").intValue = hits;
        so.FindProperty("haloScale").floatValue = 1.12f;
        so.ApplyModifiedPropertiesWithoutUndo();

        root.AddComponent(movementType);
        root.transform.localScale = Vector3.one * scale;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{prefabName}.prefab");
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
        ("Forest_Fox",    new[] { new Vector3(-6.8f, 0.9f, 3.0f), new Vector3(6.8f, 0.9f, 3.0f) }),
        ("Forest_Rabbit", new[] { new Vector3(-1.5f, 0.55f, 3.3f), new Vector3(1.5f, 0.55f, 3.3f) }),
        ("Forest_Owl",    new[] { new Vector3(-3.6f, 0.55f, 3.75f), new Vector3(-2.1f, 0.55f, 3.75f), new Vector3(-0.7f, 0.55f, 3.75f),
                                  new Vector3(2.3f, 0.55f, 3.75f), new Vector3(3.8f, 0.55f, 3.75f) }),
        ("Forest_Skunk",  new[] { new Vector3(-1.5f, 0.85f, 3.15f), new Vector3(1.5f, 0.85f, 3.15f) }),
        ("Forest_FarFox", new[] { new Vector3(-6.8f, 1.55f, 4.95f), new Vector3(6.8f, 1.55f, 4.95f) }),
    };

    private static void CreateLanes()
    {
        GameObject spawnRoot = FindRoot("TargetSpawnPoints");

        // Carriles del estanque que ya no se usan (el del pájaro se conserva)
        foreach (string old in new[] { "Lane_Duck", "Lane_Fish", "Lane_FarDuck" })
        {
            Transform t = spawnRoot.transform.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        Transform forestRoot = spawnRoot.transform.Find("Forest");
        if (forestRoot != null)
            Object.DestroyImmediate(forestRoot.gameObject);

        forestRoot = new GameObject("Forest").transform;
        forestRoot.SetParent(spawnRoot.transform, false);

        foreach (var (id, positions) in Lanes)
        {
            GameObject laneGO = new GameObject("Lane_" + id);
            laneGO.transform.SetParent(forestRoot, false);

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

    // ---------- Nivel 1 ----------
    private static void UpdateLevel1(GameObject fox, GameObject farFox, GameObject rabbit, GameObject owl, GameObject skunk, GameObject bird)
    {
        LevelConfig level = AssetDatabase.LoadAssetAtPath<LevelConfig>(Level1Path);
        if (level == null)
        {
            Debug.LogError("[ForestLevelSetup] No existe Level1_Estanque; ejecuta antes Setup Levels.");
            return;
        }

        level.levelName = "Bosque";
        level.environmentId = "Forest";
        level.spawnInterval = 1.3f;
        level.maxActiveTargets = 5;
        level.pointsMultiplier = 1f;
        level.speedMultiplier = 1f;
        level.lifetimeMultiplier = 1f;
        level.entries = new[]
        {
            new LevelConfig.SpawnEntry { label = "Zorro",         prefab = fox,    laneId = "Forest_Fox",    weight = 3f,   maxActive = 2 },
            new LevelConfig.SpawnEntry { label = "Conejo",        prefab = rabbit, laneId = "Forest_Rabbit", weight = 2.5f, maxActive = 1 },
            new LevelConfig.SpawnEntry { label = "Buho",          prefab = owl,    laneId = "Forest_Owl",    weight = 2f,   maxActive = 1 },
            new LevelConfig.SpawnEntry { label = "Pajaro",        prefab = bird,   laneId = "Bird",          weight = 2f,   maxActive = 1 },
            new LevelConfig.SpawnEntry { label = "Zorro lejano",  prefab = farFox, laneId = "Forest_FarFox", weight = 1.2f, maxActive = 1 },
            new LevelConfig.SpawnEntry { label = "Mofeta",        prefab = skunk,  laneId = "Forest_Skunk",  weight = 1.2f, maxActive = 1 },
        };
        EditorUtility.SetDirty(level);
    }

    // El entorno "Pond" (estanque) se sustituye por "Forest": decorado del bosque + la iluminación nocturna
    private static void RegisterEnvironment(GameObject env)
    {
        LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
        SerializedObject so = new SerializedObject(lm);
        SerializedProperty envs = so.FindProperty("environments");
        GameObject night = FindRoot("Lighting_Night");

        for (int i = envs.arraySize - 1; i >= 0; i--)
        {
            string id = envs.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue;
            if (id == "Pond" || id == "Forest")
                envs.DeleteArrayElementAtIndex(i);
        }

        envs.InsertArrayElementAtIndex(0);
        SerializedProperty forest = envs.GetArrayElementAtIndex(0);
        forest.FindPropertyRelative("id").stringValue = "Forest";
        SerializedProperty objs = forest.FindPropertyRelative("objects");
        objs.arraySize = 2;
        objs.GetArrayElementAtIndex(0).objectReferenceValue = env;
        objs.GetArrayElementAtIndex(1).objectReferenceValue = night;
        so.ApplyModifiedPropertiesWithoutUndo();

        env.SetActive(true);
        if (night != null) night.SetActive(true);
    }

    private static GameObject FindRoot(string name)
    {
        foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }
}
