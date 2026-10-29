using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Monta el sistema de niveles en la escena actual. Menú: Tools > Fairground > Setup Levels.
// Se puede repetir: los LevelConfig y materiales que ya existen NO se sobrescriben
// (para no perder ajustes hechos a mano); carriles y referencias sí se vuelven a enlazar.
public static class LevelsSetup
{
    private const string LevelsFolder = "Assets/Data/Levels";
    private const string MaterialsFolder = "Assets/Art/Materials/Targets";

    [MenuItem("Tools/Fairground/Setup Levels")]
    public static void Setup()
    {
        Material gold = GetOrCreateOutline("M_T_Outline_Gold", GoldColor);
        Material red = GetOrCreateOutline("M_T_Outline_Red", RedColor);

        CreateLanes();

        LevelConfig level1 = GetOrCreateLevel("Level1_Estanque", level =>
        {
            level.levelName = "Estanque";
            level.environmentId = "Pond";
            level.spawnInterval = 1.3f;
            level.maxActiveTargets = 5;
            level.timeBonusChance = 0.05f;
            level.ammoBonusChance = 0.05f;
            level.entries = PondEntries();
        });

        // Provisional: mismo entorno y objetivos que el nivel 1, pero más difícil,
        // hasta que estén el decorado marino y sus objetivos
        LevelConfig level2 = GetOrCreateLevel("Level2_FondoMarino", level =>
        {
            level.levelName = "Fondo marino";
            level.environmentId = "Pond";
            level.spawnInterval = 1.1f;
            level.maxActiveTargets = 6;
            level.pointsMultiplier = 1.5f;
            level.speedMultiplier = 1.25f;
            level.lifetimeMultiplier = 0.9f;
            level.timeBonusChance = 0.04f;
            level.ammoBonusChance = 0.04f;
            level.entries = PondEntries();
        });

        SetupManagers(new[] { level1, level2 }, gold, red);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[LevelsSetup] Sistema de niveles configurado.");
    }

    // ---------- Materiales de contorno especial ----------
    // Unlit: el color no depende de las luces (con Lit, el foco cálido lo lavaba y el dorado se confundía
    // con el amarillo del pato). Color HDR > 1: el Bloom lo convierte en un halo visible de lejos.
    private static readonly Color GoldColor = new Color(1f, 0.7f, 0.08f);
    private static readonly Color RedColor = new Color(1f, 0.08f, 0.04f);
    private const float BonusIntensity = 2f;

    [MenuItem("Tools/Fairground/Update Bonus Materials")]
    public static void UpdateBonusMaterials()
    {
        GetOrCreateOutline("M_T_Outline_Gold", GoldColor);
        GetOrCreateOutline("M_T_Outline_Red", RedColor);
        AssetDatabase.SaveAssets();
        Debug.Log("[LevelsSetup] Materiales de bonus actualizados (Unlit, HDR).");
    }

    // Estos dos materiales son generados: se reconfiguran siempre, exista o no el asset
    private static Material GetOrCreateOutline(string name, Color color)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");

        if (mat == null)
        {
            mat = new Material(unlit);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = unlit;
        }

        mat.SetColor("_BaseColor", color * BonusIntensity);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------- Carriles ----------
    private static readonly (string id, string[] points)[] Lanes =
    {
        ("Duck",    new[] { "SP_Duck_L", "SP_Duck_R" }),
        ("Fish",    new[] { "SP_Fish_L", "SP_Fish_R" }),
        ("Bird",    new[] { "SP_Bird_L", "SP_Bird_R" }),
        ("FarDuck", new[] { "SP_FarDuck_L", "SP_FarDuck_R" }),
    };

    private static void CreateLanes()
    {
        GameObject root = GameObject.Find("TargetSpawnPoints");

        if (root == null)
        {
            Debug.LogError("[LevelsSetup] No se encuentra 'TargetSpawnPoints' en la escena.");
            return;
        }

        foreach (var (id, pointNames) in Lanes)
        {
            Transform laneT = root.transform.Find("Lane_" + id);

            if (laneT == null)
            {
                GameObject laneGO = new GameObject("Lane_" + id);
                Undo.RegisterCreatedObjectUndo(laneGO, "Create lane");
                laneGO.transform.SetParent(root.transform, false);
                laneT = laneGO.transform;
            }

            List<Transform> points = new List<Transform>();

            foreach (string pointName in pointNames)
            {
                Transform point = FindDeep(root.transform, pointName);

                if (point == null)
                {
                    Debug.LogWarning($"[LevelsSetup] No se encuentra el punto '{pointName}'.");
                    continue;
                }

                if (point.parent != laneT)
                    Undo.SetTransformParent(point, laneT, "Move spawn point to lane");   // conserva la posición en el mundo

                points.Add(point);
            }

            SpawnLane lane = laneT.GetComponent<SpawnLane>();
            if (lane == null)
                lane = Undo.AddComponent<SpawnLane>(laneT.gameObject);

            SerializedObject so = new SerializedObject(lane);
            so.FindProperty("laneId").stringValue = id;

            SerializedProperty pointsProp = so.FindProperty("points");
            pointsProp.arraySize = points.Count;
            for (int i = 0; i < points.Count; i++)
                pointsProp.GetArrayElementAtIndex(i).objectReferenceValue = points[i];

            so.ApplyModifiedProperties();
        }
    }

    // ---------- Niveles ----------
    private static LevelConfig.SpawnEntry[] PondEntries()
    {
        return new[]
        {
            Entry("Pato",        "Assets/Prefabs/HorizontalTarget.prefab",     "Duck",    3f,   2),
            Entry("Pez",         "Assets/Prefabs/JumpTarget.prefab",           "Fish",    2f,   1),
            Entry("Pajaro",      "Assets/Prefabs/SenoTarget.prefab",           "Bird",    2f,   1),
            Entry("Pato lejano", "Assets/Prefabs/HorizontalTarget_Far.prefab", "FarDuck", 1.5f, 1),
        };
    }

    private static LevelConfig.SpawnEntry Entry(string label, string prefabPath, string lane, float weight, int max)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
            Debug.LogWarning($"[LevelsSetup] No se encuentra el prefab '{prefabPath}'.");

        return new LevelConfig.SpawnEntry { label = label, prefab = prefab, laneId = lane, weight = weight, maxActive = max };
    }

    private static LevelConfig GetOrCreateLevel(string assetName, System.Action<LevelConfig> init)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(LevelsFolder))
            AssetDatabase.CreateFolder("Assets/Data", "Levels");

        string path = $"{LevelsFolder}/{assetName}.asset";
        LevelConfig level = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);

        if (level != null)
            return level;   // ya existe: respetamos los ajustes que tenga

        level = ScriptableObject.CreateInstance<LevelConfig>();
        init(level);
        AssetDatabase.CreateAsset(level, path);
        return level;
    }

    // ---------- LevelManager y spawner ----------
    private static void SetupManagers(LevelConfig[] levels, Material gold, Material red)
    {
        GameManager gm = Object.FindFirstObjectByType<GameManager>();

        if (gm == null)
        {
            Debug.LogError("[LevelsSetup] No se encuentra el GameManager.");
            return;
        }

        LevelManager lm = gm.GetComponent<LevelManager>();
        if (lm == null)
            lm = Undo.AddComponent<LevelManager>(gm.gameObject);

        SerializedObject so = new SerializedObject(lm);

        SerializedProperty levelsProp = so.FindProperty("levels");
        levelsProp.arraySize = levels.Length;
        for (int i = 0; i < levels.Length; i++)
            levelsProp.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];

        // Entorno del estanque: el decorado interior del puesto y su iluminación
        List<GameObject> pond = new List<GameObject>();
        GameObject booth = GameObject.Find("Booth");

        if (booth != null)
        {
            foreach (string child in new[] { "Booth_Scenery", "Booth_Bank", "Booth_Waves_1", "Booth_Waves_2",
                                             "Booth_Waves_3", "Booth_Waves_4", "Booth_Waves_5" })
            {
                Transform t = booth.transform.Find(child);
                if (t != null) pond.Add(t.gameObject);
                else Debug.LogWarning($"[LevelsSetup] No se encuentra Booth/{child}.");
            }
        }

        GameObject lighting = GameObject.Find("Lighting_Night");
        if (lighting != null) pond.Add(lighting);

        SerializedProperty envs = so.FindProperty("environments");
        envs.arraySize = 1;
        SerializedProperty env = envs.GetArrayElementAtIndex(0);
        env.FindPropertyRelative("id").stringValue = "Pond";

        SerializedProperty objs = env.FindPropertyRelative("objects");
        objs.arraySize = pond.Count;
        for (int i = 0; i < pond.Count; i++)
            objs.GetArrayElementAtIndex(i).objectReferenceValue = pond[i];

        so.ApplyModifiedProperties();

        TargetSpawner spawner = gm.GetComponent<TargetSpawner>();
        if (spawner == null)
            spawner = Object.FindFirstObjectByType<TargetSpawner>();

        if (spawner != null)
        {
            SerializedObject sp = new SerializedObject(spawner);
            sp.FindProperty("timeBonusOutline").objectReferenceValue = gold;
            sp.FindProperty("ammoBonusOutline").objectReferenceValue = red;
            sp.ApplyModifiedProperties();
        }
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child;
        }

        return null;
    }
}
