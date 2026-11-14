using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Monta el paseo de la feria (Fase 8): Tools > Fairground > Build Promenade.
//
// La feria se construyó como un decorado de teatro, para verse desde la caseta: detrás del jugador no había nada.
// El viaje de cámara hasta la armería gira justo hacia ese lado, así que el paseo cierra el hueco: camino hasta la
// carpa, farolas con guirnaldas y banderines, tres casetas de enfrente, carritos de palomitas y de algodón con
// globos, siluetas de público y el horizonte de siluetas por detrás (empalma con Fair_Horizon).
//
// Prefab Env_Promenade (Prefabs/Enviroments): el modelo de Env_Promenade.fbx (colección Env_Promenade de
// Booth.blend), las luces, los materiales animados (banderines con Fairground/Sway, bombillas en cadena con
// Fairground/AnimatedLights; los pesos los escribe AnimatedMeshPostprocessor) y el vaivén de los globos.
// Se rehace entero en cada pasada en la misma ruta. Nada de esto se ve durante la partida (queda a la espalda).
public static class PromenadeBuilder
{
    private const string ModelPath = "Assets/Art/Models/Environments/Env_Promenade.fbx";
    private const string PrefabPath = "Assets/Prefabs/Enviroments/Env_Promenade.prefab";

    [MenuItem("Tools/Fairground/Build Promenade")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Promenade",
                "Monta el paseo de la feria (detrás de la caseta, camino de la armería) en la escena abierta.\n\n" +
                "En Main, avisa antes a tu compañero. ¿Continuar?", "Montar", "Cancelar"))
            return;
        Run();
    }

    public static void Run()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.importAnimation = false;
        importer.bakeAxisConversion = false;
        importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
        importer.SaveAndReimport();

        var root = new GameObject("Env_Promenade");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;   // como Env_Fairground: el FBX trae la raíz girada (§6)

        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            r.shadowCastingMode = r.name.StartsWith("Prom_Horizon") || r.name == "Prom_Path" ? ShadowCastingMode.Off : ShadowCastingMode.On;

        // Banderines que se mecen y bombillas que se encienden en cadena, con los mismos materiales que la caseta
        SetMaterials(model, "Prom_Bunting", "M_Sway_Red", "M_Sway_Yellow", "M_Sway_StripeBlue", "M_Sway_Cream");
        SetMaterials(model, "Prom_StringBulbs", "M_Lights_Bulb");

        // Público: silueta casi negra con un borde cálido que brilla un poco. Con M_E_Silh (el del horizonte, que
        // recibe luz) las farolas los aclaraban y parecían fantasmas azulados
        Transform crowd = Find(model.transform, "Prom_Crowd");
        if (crowd != null)
            crowd.GetComponent<Renderer>().sharedMaterials = new[]
            {
                CrowdMaterial("M_A_CrowdDark", new Color(0.035f, 0.03f, 0.045f), Color.black),
                CrowdMaterial("M_A_CrowdRim", new Color(0.25f, 0.18f, 0.12f), new Color(0.55f, 0.36f, 0.2f)),
            };

        // Luces: farolas, carritos y casetas (capa de luz por defecto, sin sombras)
        Transform lamps = Find(model.transform, "Prom_LampBulbs");
        if (lamps != null)
        {
            // Una luz por farol: los centros de las bombillas se sacan de la malla (cada isla es una bombilla)
            Mesh m = lamps.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] v = m.vertices;
            int perBulb = v.Length / 5;
            for (int i = 0; i < 5; i++)
            {
                Vector3 c = Vector3.zero;
                for (int k = 0; k < perBulb; k++) c += v[i * perBulb + k];
                c = lamps.TransformPoint(c / perBulb);
                NewLight(root.transform, $"Light_Lamp_{i}", c + Vector3.down * 0.15f, new Color(1f, 0.75f, 0.45f), 3.5f, 8f);
            }
        }
        LightOver(root.transform, model.transform, "Prom_CartPopcorn", new Color(1f, 0.8f, 0.5f), 2.5f, 4f);
        LightOver(root.transform, model.transform, "Prom_CartCotton", new Color(1f, 0.6f, 0.8f), 2.5f, 4f);
        for (int i = 0; i < 3; i++)
            LightOver(root.transform, model.transform, $"Prom_Stall_{i}", new Color(1f, 0.82f, 0.6f), 3.5f, 5.5f);

        // Globos: giran desde el nudo (abajo del cordel), cada uno a su ritmo
        for (int i = 0; i < 4; i++)
        {
            Transform balloon = Find(model.transform, $"Prom_Balloon_{i}");
            if (balloon == null) continue;
            Bounds b = balloon.GetComponent<Renderer>().bounds;
            var pivot = new GameObject($"BalloonPivot_{i}").transform;
            pivot.SetParent(root.transform, false);
            pivot.position = new Vector3(b.center.x, b.min.y, b.center.z);
            balloon.SetParent(pivot, true);
            var osc = pivot.gameObject.AddComponent<IdleOscillator>();
            var so = new SerializedObject(osc);
            so.FindProperty("channel").enumValueIndex = (int)IdleOscillator.Channel.Rotation;
            so.FindProperty("axis").vector3Value = i % 2 == 0 ? new Vector3(0f, 0f, 1f) : new Vector3(1f, 0f, 0.4f);
            so.FindProperty("amplitude").floatValue = 7f;
            so.FindProperty("frequency").floatValue = 0.35f + 0.08f * i;
            so.FindProperty("randomPhase").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        if (GameObject.Find("Env_Promenade") == null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            GameObject fair = GameObject.Find("Env_Fairground");
            if (fair != null && fair.transform.parent != null) inst.transform.SetParent(fair.transform.parent, true);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("PromenadeBuilder: paseo montado.");
    }

    private static void SetMaterials(GameObject model, string objectName, params string[] materialNames)
    {
        Transform t = Find(model.transform, objectName);
        if (t == null) return;
        var r = t.GetComponent<Renderer>();
        Material[] mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length && i < materialNames.Length; i++)
        {
            Material m = FindMaterial(materialNames[i]);
            if (m != null) mats[i] = m;
            else Debug.LogWarning($"PromenadeBuilder: no encuentro el material {materialNames[i]}.");
        }
        r.sharedMaterials = mats;
    }

    private static Material CrowdMaterial(string name, Color baseColor, Color emission)
    {
        string path = $"Assets/Art/Materials/Fairground/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", 0f);
        if (emission.maxColorComponent > 0f) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", emission); }
        else { mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.black); }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material FindMaterial(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Material"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m != null && m.name == name) return m;
        }
        return null;
    }

    private static void LightOver(Transform parent, Transform model, string objectName, Color color, float intensity, float range)
    {
        Transform t = Find(model, objectName);
        if (t == null) return;
        Bounds b = t.GetComponent<Renderer>().bounds;
        NewLight(parent, "Light_" + objectName.Replace("Prom_", ""), new Vector3(b.center.x, b.min.y + b.size.y * 0.75f, b.center.z), color, intensity, range);
    }

    private static void NewLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        l.renderingLayerMask = 1;
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
