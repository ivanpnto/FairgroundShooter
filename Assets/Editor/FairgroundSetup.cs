using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Coloca el exterior de la feria (siempre visible, no depende del nivel). Menú: Tools > Fairground > Setup Exterior.
public static class FairgroundSetup
{
    private const string Model = "Assets/Art/Models/Environments/Env_Fairground.fbx";
    private const string PrefabPath = "Assets/Prefabs/Enviroments/Env_Fairground.prefab";

    [MenuItem("Tools/Fairground/Setup Exterior")]
    public static void Setup()
    {
        GameObject existing = null;
        foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == "Env_Fairground") existing = go;
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
        instance.name = "Env_Fairground";
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;   // el FBX trae la raíz girada 180º

        foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
        {
            bool moving = t.name.Contains("Rotor") || t.name.Contains("Gondola");
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, moving ? 0 : StaticEditorFlags.BatchingStatic);

            MeshRenderer mr = t.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                // El exterior no necesita sombras: está lejos, apenas recibe luz y ahorra coste
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            if (t.name == "Fair_Wheel_Rotor")
            {
                // La noria está girada para mirar al jugador: su eje es la dirección horizontal hacia la cámara
                Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
                Vector3 axis = cam != null ? cam.transform.position - t.position : Vector3.forward;
                axis.y = 0f;
                AddRotation(t.gameObject, axis.normalized, 8f);
            }
            else if (t.name == "Fair_Carousel_Rotor")
                AddRotation(t.gameObject, Vector3.up, 20f);
            else if (t.name.StartsWith("Fair_Gondola"))
                t.gameObject.AddComponent<KeepWorldRotation>();
        }

        PrefabUtility.SaveAsPrefabAssetAndConnect(instance, PrefabPath, InteractionMode.AutomatedAction);

        // El plano Ground de Unity está a la misma altura que el suelo de la feria: se oculta su malla
        // (se mantiene su collider) para que no parpadeen al solaparse
        GameObject ground = GameObject.Find("Ground");
        if (ground != null && ground.GetComponent<MeshRenderer>() != null)
            ground.GetComponent<MeshRenderer>().enabled = false;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[FairgroundSetup] Exterior de la feria colocado.");
    }

    private static void AddRotation(GameObject go, Vector3 axis, float speed)
    {
        ConstantRotation rot = go.AddComponent<ConstantRotation>();
        SerializedObject so = new SerializedObject(rot);
        so.FindProperty("worldAxis").vector3Value = axis;
        so.FindProperty("degreesPerSecond").floatValue = speed;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
