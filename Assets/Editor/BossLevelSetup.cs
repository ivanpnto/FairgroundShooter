using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Monta el nivel 4 (I7.6, §27) en la escena abierta: el circo (entorno, carriles y dianas), el nivel en
// LevelManager, el artículo de la tienda, el boss, la revelación con sus luces, la zona de daño, la vida
// del jugador y el HUD de depuración. Se puede repetir: lo que ya existe se reutiliza y se vuelve a enlazar.
// Pensada para Main.unity (escena compartida: avisar antes, pull antes, commit + push después).
public static class BossLevelSetup
{
    private const string BossPrefabPath = "Assets/Prefabs/Boss/Boss.prefab";
    private const string CircusFbxPath = "Assets/Art/Models/Environments/Env_Circus.fbx";
    private const string CircusPrefabPath = "Assets/Prefabs/Enviroments/Env_Circus.prefab";
    private const string LevelPath = "Assets/Data/Levels/Level4_Boss.asset";
    private const string UpgradePath = "Assets/Data/Upgrades/BossLevel.asset";
    private const string EnvironmentId = "Circus";

    // Carriles del circo (z = profundidad; la barrera de la pista está en z = 2,45 y las gradas en z = 4,3)
    private static readonly (string id, Vector3[] points)[] Lanes =
    {
        ("Circus_Seal", new[] { new Vector3(-1.5f, 1.1f, 3.0f), new Vector3(1.5f, 1.1f, 3.0f) }),
        ("Circus_Elephant", new[] { new Vector3(-2.2f, 1.0f, 3.6f), new Vector3(2.2f, 1.0f, 3.6f) }),
        ("Circus_Jack", new[] { new Vector3(-4f, -0.45f, 2.75f), new Vector3(-2f, -0.45f, 2.75f), new Vector3(0f, -0.45f, 2.75f),
                                new Vector3(2f, -0.45f, 2.75f), new Vector3(4f, -0.45f, 2.75f) }),
        // Esbirros del boss (I7.5): salen del suelo de la caseta rota, delante del boss
        ("Boss_Minion", new[] { new Vector3(-3.5f, 0.65f, 3.0f), new Vector3(-1.3f, 0.65f, 3.4f), new Vector3(1.3f, 0.65f, 3.4f),
                                new Vector3(3.5f, 0.65f, 3.0f) }),
    };

    [MenuItem("Tools/Fairground/Setup Boss Level")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!EditorUtility.DisplayDialog("Setup Boss Level",
                $"Monta el nivel 4 (circo + boss) en la escena «{scene.name}».\n\n" +
                "Si es Main.unity: avisa a tu compañero y haz pull antes; commit + push después.", "Montar", "Cancelar"))
            return;
        Apply();
    }

    // Sin diálogo (para lanzarlo desde código o desde otra herramienta)
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var log = new System.Text.StringBuilder("[BossLevelSetup]\n");
        GameObject circus = EnsureCircus(log);
        EnsureLanes(log);
        ConfigureLevel(log);
        ConfigureLevelManager(circus, log);
        ConfigureShop(log);
        GameObject boss = EnsureBoss(log);
        EnsureReveal(boss, circus, log);
        EnsurePlayerPieces(log);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(log.ToString());
    }

    // ── Circo ──────────────────────────────────────────────────────────────────

    private static GameObject EnsureCircus(System.Text.StringBuilder log)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CircusPrefabPath) == null)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CircusFbxPath);
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            temp.name = "Env_Circus";
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // §6: rotación 0
            PrefabUtility.SaveAsPrefabAsset(temp, CircusPrefabPath);
            Object.DestroyImmediate(temp);
            log.AppendLine("  Prefab Env_Circus creado");
        }
        SceneryCollidersSetup.Setup();   // colliders y capa Scenery del circo (y del resto, sin cambios)

        GameObject circus = FindInScene("Env_Circus");
        if (circus == null)
        {
            circus = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CircusPrefabPath));
            circus.name = "Env_Circus";
            log.AppendLine("  Env_Circus añadido a la escena");
        }
        circus.SetActive(false);   // lo enciende LevelManager con el entorno «Circus»
        return circus;
    }

    private static void EnsureLanes(System.Text.StringBuilder log)
    {
        GameObject spawnRoot = FindInScene("TargetSpawnPoints") ?? new GameObject("TargetSpawnPoints");
        Transform group = spawnRoot.transform.Find("Circus");
        if (group == null)
        {
            group = new GameObject("Circus").transform;
            group.SetParent(spawnRoot.transform, false);
        }

        foreach (var (id, points) in Lanes)
        {
            string laneName = "Lane_" + id;
            Transform lane = group.Find(laneName);
            if (lane == null)
            {
                lane = new GameObject(laneName).transform;
                lane.SetParent(group, false);
            }
            for (int i = lane.childCount - 1; i >= 0; i--) Object.DestroyImmediate(lane.GetChild(i).gameObject);

            var pointTransforms = new List<Transform>();
            for (int i = 0; i < points.Length; i++)
            {
                var p = new GameObject($"P{i + 1}").transform;
                p.SetParent(lane, false);
                p.position = points[i];
                pointTransforms.Add(p);
            }

            SpawnLane spawnLane = lane.GetComponent<SpawnLane>();
            if (spawnLane == null) spawnLane = lane.gameObject.AddComponent<SpawnLane>();
            var so = new SerializedObject(spawnLane);
            so.FindProperty("laneId").stringValue = id;
            var arr = so.FindProperty("points");
            arr.arraySize = pointTransforms.Count;
            for (int i = 0; i < pointTransforms.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = pointTransforms[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        log.AppendLine($"  Carriles del nivel 4: {Lanes.Length}");
    }

    private static void ConfigureLevel(System.Text.StringBuilder log)
    {
        var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelPath);
        level.levelName = "El Circo";
        level.environmentId = EnvironmentId;
        level.isBossLevel = true;
        level.bossRevealDelay = 12f;
        level.duration = 60f;            // se muestra la cuenta atrás de un nivel normal; la revelación llega antes
        level.spawnInterval = 0.9f;
        level.endSpawnIntervalFactor = 0.8f;
        level.maxActiveTargets = 4;
        level.pointsMultiplier = 2.5f;
        level.speedMultiplier = 1.1f;
        level.lifetimeMultiplier = 0.85f;
        level.healthMultiplier = 1.75f;
        level.entries = new[]
        {
            Entry("Foca", "Assets/Prefabs/Circus/SealTarget.prefab", "Circus_Seal", 1.4f, 1),
            Entry("Elefante", "Assets/Prefabs/Circus/ElephantTarget.prefab", "Circus_Elephant", 1f, 1),
            Entry("Muñeco de resorte", "Assets/Prefabs/Circus/JackInBoxTarget.prefab", "Circus_Jack", 1.6f, 2),
        };
        EditorUtility.SetDirty(level);
        AssetDatabase.SaveAssets();
        log.AppendLine("  Level4_Boss: «El Circo», revelación a los 12 s, 3 tipos de diana");
    }

    private static LevelConfig.SpawnEntry Entry(string label, string prefabPath, string laneId, float weight, int maxActive) =>
        new LevelConfig.SpawnEntry
        {
            label = label,
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath),
            laneId = laneId,
            weight = weight,
            maxActive = maxActive,
            groupSize = 1,
        };

    private static void ConfigureLevelManager(GameObject circus, System.Text.StringBuilder log)
    {
        var lm = Object.FindFirstObjectByType<LevelManager>();
        var so = new SerializedObject(lm);

        var levels = so.FindProperty("levels");
        var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelPath);
        bool hasLevel = false;
        for (int i = 0; i < levels.arraySize; i++) hasLevel |= levels.GetArrayElementAtIndex(i).objectReferenceValue == level;
        if (!hasLevel)
        {
            levels.arraySize++;
            levels.GetArrayElementAtIndex(levels.arraySize - 1).objectReferenceValue = level;
            log.AppendLine("  Level4_Boss añadido al final de LevelManager.levels");
        }

        // Entorno «Circus» = el circo + la noche. Sustituye al «BossNight» provisional si estaba
        var envs = so.FindProperty("environments");
        SerializedProperty env = null;
        for (int i = 0; i < envs.arraySize; i++)
        {
            string id = envs.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue;
            if (id == EnvironmentId || id == "BossNight") { env = envs.GetArrayElementAtIndex(i); break; }
        }
        if (env == null)
        {
            envs.arraySize++;
            env = envs.GetArrayElementAtIndex(envs.arraySize - 1);
        }
        env.FindPropertyRelative("id").stringValue = EnvironmentId;
        var objs = env.FindPropertyRelative("objects");
        objs.arraySize = 2;
        objs.GetArrayElementAtIndex(0).objectReferenceValue = circus;
        objs.GetArrayElementAtIndex(1).objectReferenceValue = FindInScene("Lighting_Night");
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("  Entorno «Circus»: Env_Circus + Lighting_Night");
    }

    private static void ConfigureShop(System.Text.StringBuilder log)
    {
        var pm = Object.FindFirstObjectByType<ProfileManager>();
        var upgrade = AssetDatabase.LoadAssetAtPath<UpgradeData>(UpgradePath);
        if (pm == null || upgrade == null) { log.AppendLine("  (sin ProfileManager o sin BossLevel.asset: tienda sin tocar)"); return; }
        var so = new SerializedObject(pm);
        var list = so.FindProperty("upgrades");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == upgrade) return;
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = upgrade;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("  Tienda: «Nivel 4: El Circo» añadido");
    }

    // ── Boss y revelación ──────────────────────────────────────────────────────

    private static GameObject EnsureBoss(System.Text.StringBuilder log)
    {
        var bc = Object.FindFirstObjectByType<BossController>(FindObjectsInactive.Include);
        GameObject boss = bc != null ? bc.gameObject : null;
        if (boss == null)
        {
            boss = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath));
            boss.name = "Boss";
            boss.transform.SetPositionAndRotation(new Vector3(0f, 0f, 8f), Quaternion.identity);
            log.AppendLine("  Boss añadido en (0, 0, 8)");
        }
        var so = new SerializedObject(boss.GetComponent<BossController>());
        so.FindProperty("autoStart").boolValue = false;   // lo arranca BossReveal
        so.ApplyModifiedPropertiesWithoutUndo();
        return boss;
    }

    private static void EnsureReveal(GameObject boss, GameObject circus, System.Text.StringBuilder log)
    {
        GameObject revealGo = FindInScene("BossReveal") ?? new GameObject("BossReveal");
        // Sin ?? con componentes: en el editor GetComponent puede devolver un «falso null» que ?? no detecta
        BossReveal reveal = revealGo.GetComponent<BossReveal>();
        if (reveal == null) reveal = revealGo.AddComponent<BossReveal>();

        Light crack = EnsureLight(revealGo.transform, "CrackLight", LightType.Point, new Vector3(0f, 2.2f, 7f), Quaternion.identity,
            8f, 7f, 30f, new Color(1f, 0.1f, 0.05f));
        Light spotL = EnsureLight(revealGo.transform, "BossSpot_L", LightType.Spot, new Vector3(-3.5f, 0.3f, 5f), Quaternion.Euler(319f, 51.3f, 0f),
            20f, 14f, 45f, new Color(1f, 0.78f, 0.62f));
        Light spotR = EnsureLight(revealGo.transform, "BossSpot_R", LightType.Spot, new Vector3(3.5f, 0.3f, 5f), Quaternion.Euler(319f, 308.7f, 0f),
            20f, 14f, 45f, new Color(1f, 0.78f, 0.62f));

        var so = new SerializedObject(reveal);
        so.FindProperty("boss").objectReferenceValue = boss.GetComponent<BossController>();
        so.FindProperty("bossRoot").objectReferenceValue = boss.transform;
        so.FindProperty("booth").objectReferenceValue = Object.FindFirstObjectByType<BoothBreak>(FindObjectsInactive.Include);
        so.FindProperty("curtain").objectReferenceValue = Object.FindFirstObjectByType<CurtainController>(FindObjectsInactive.Include);
        so.FindProperty("crackLight").objectReferenceValue = crack;
        var spots = so.FindProperty("bossSpots");
        spots.arraySize = 2;
        spots.GetArrayElementAtIndex(0).objectReferenceValue = spotL;
        spots.GetArrayElementAtIndex(1).objectReferenceValue = spotR;

        // Las capas del circo salen volando con la pared
        var decor = new List<Transform>();
        foreach (Transform child in circus.transform)
            if (child.GetComponent<MeshRenderer>() != null) decor.Add(child);
        var decorProp = so.FindProperty("decorToLaunch");
        decorProp.arraySize = decor.Count;
        for (int i = 0; i < decor.Count; i++) decorProp.GetArrayElementAtIndex(i).objectReferenceValue = decor[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine($"  BossReveal enlazado (booth, telón, luces, {decor.Count} capas del circo)");
    }

    private static Light EnsureLight(Transform parent, string name, LightType type, Vector3 position, Quaternion rotation,
                                     float intensity, float range, float angle, Color color)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
        }
        t.SetPositionAndRotation(position, rotation);
        Light light = t.GetComponent<Light>();
        if (light == null) light = t.gameObject.AddComponent<Light>();
        light.type = type;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = angle;
        light.color = color;
        light.shadows = LightShadows.None;
        return light;
    }

    private static void EnsurePlayerPieces(System.Text.StringBuilder log)
    {
        GameObject zone = FindInScene("PlayerDamageZone");
        if (zone == null)
        {
            zone = new GameObject("PlayerDamageZone");
            zone.transform.position = new Vector3(0f, 1.6f, -4.3f);   // fija en el mundo, no hija de la cámara (§27.3)
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.2f, 1.6f, 0.6f);
            var rb = zone.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            zone.AddComponent<PlayerDamageZone>();
            log.AppendLine("  PlayerDamageZone añadida");
        }
        zone.layer = LayerMask.NameToLayer("PlayerZone");

        var shoot = Object.FindFirstObjectByType<PlayerShoot>();
        if (shoot != null && shoot.GetComponent<PlayerHealth>() == null)
        {
            shoot.gameObject.AddComponent<PlayerHealth>();
            log.AppendLine("  PlayerHealth añadido a " + shoot.name);
        }

        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null && gm.GetComponent<BossDebugHUD>() == null)
        {
            gm.gameObject.AddComponent<BossDebugHUD>();
            log.AppendLine("  BossDebugHUD añadido (provisional hasta el HUD de Jaime, J7.1)");
        }
    }

    // GameObject.Find no ve los objetos inactivos (los entornos suelen estarlo)
    private static GameObject FindInScene(string name)
    {
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name) return root;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        }
        return null;
    }
}
