using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Hitbox de la caseta y del decorado (I4.5). Menú: Tools > Fairground > Setup Scenery Colliders.
//
// Regla: "lo que se ve, para la bala". Cada pieza sólida del decorado recibe un MeshCollider con su
// propia malla y pasa a la capa Scenery. Así un disparo al arbusto da en el arbusto (fallo, con punto
// de impacto para el polvo) y no atraviesa hasta el búho escondido detrás.
//
// Por qué MeshCollider y no cajas: los decorados son recortes planos con silueta irregular (arbustos,
// colina, arrecife). Una caja taparía huecos por los que SÍ se ve el objetivo, y eso sería injusto.
// Un MeshCollider estático (no convexo) solo se usa para raycasts: es barato y coincide al píxel.
//
// Se excluyen las piezas finas o que se mueven (banderines, bombillas, luciérnagas, algas, burbujas):
// su collider no seguiría la animación y bloquearía disparos donde ya no hay nada.
//
// Trabaja sobre los PREFABS, no sobre la escena: así Main.unity casi no cambia y no hay conflictos.
public static class SceneryCollidersSetup
{
    private const string SceneryLayerName = "Scenery";

    private static readonly string[] Prefabs =
    {
        "Assets/Prefabs/Enviroments/Booth.prefab",
        "Assets/Prefabs/Enviroments/Env_Forest.prefab",
        "Assets/Prefabs/Enviroments/Env_Sea.prefab",
    };

    // Piezas que NO llevan collider (se compara si el nombre contiene el texto)
    private static readonly string[] Excluded =
    {
        "Bulbs", "Bunting", "Fireflies", "Kelp", "Bubble", "Rays", "Halo",
    };

    [MenuItem("Tools/Fairground/Setup Scenery Colliders")]
    public static void Setup()
    {
        int layer = LayerMask.NameToLayer(SceneryLayerName);
        if (layer < 0)
        {
            Debug.LogError("[SceneryColliders] Falta la capa 'Scenery' (Project Settings > Tags and Layers).");
            return;
        }

        StringBuilder log = new StringBuilder("[SceneryColliders]\n");

        foreach (string path in Prefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int added = 0;

            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                GameObject go = mf.gameObject;
                bool excluded = IsExcluded(go.name);

                MeshCollider col = go.GetComponent<MeshCollider>();

                if (excluded || mf.sharedMesh == null)
                {
                    // Volver a ejecutar la herramienta tras cambiar la lista de excluidos limpia lo sobrante
                    if (col != null) Object.DestroyImmediate(col);
                    continue;
                }

                if (col == null) col = go.AddComponent<MeshCollider>();
                col.sharedMesh = mf.sharedMesh;
                col.convex = false;
                go.layer = layer;
                added++;
            }

            // Las paredes invisibles de la caseta también son decorado
            Transform walls = root.transform.Find("WallColliders");
            if (walls != null)
                foreach (Transform t in walls.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = layer;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            log.AppendLine($"  {System.IO.Path.GetFileNameWithoutExtension(path)}: {added} piezas con collider");
        }

        // El suelo general de la escena (plano de Unity con collider) también es decorado
        GameObject ground = GameObject.Find("Ground");
        if (ground != null && ground.layer != layer)
        {
            ground.layer = layer;
            EditorSceneManager.MarkSceneDirty(ground.scene);
        }

        log.Append(CheckLanes());
        Debug.Log(log.ToString());
    }

    private static bool IsExcluded(string name)
    {
        foreach (string token in Excluded)
            if (name.Contains(token)) return true;
        return false;
    }

    // Comprobación: desde la cámara, ¿el centro de cada punto de aparición queda tapado por decorado?
    // Un punto tapado no es un error en sí (el búho y la medusa empiezan escondidos a propósito),
    // pero sirve para revisar los carriles de un vistazo.
    [MenuItem("Tools/Fairground/Check Lanes Visibility")]
    public static void CheckLanesMenu() => Debug.Log("[SceneryColliders]\n" + CheckLanes());

    private static string CheckLanes()
    {
        Camera cam = Camera.main;
        if (cam == null) return "  (sin Main Camera: no se comprueban los carriles)\n";

        Physics.SyncTransforms();
        int mask = 1 << LayerMask.NameToLayer(SceneryLayerName);
        StringBuilder sb = new StringBuilder("  Carriles (centro del punto visto desde la cámara):\n");

        List<SpawnLane> lanes = new List<SpawnLane>(
            Object.FindObjectsByType<SpawnLane>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        lanes.Sort((a, b) => string.CompareOrdinal(a.LaneId, b.LaneId));

        foreach (SpawnLane lane in lanes)
        {
            foreach (Transform p in lane.transform)
            {
                Vector3 to = p.position - cam.transform.position;
                // Se para un poco antes del punto: el objetivo ocupa ±0,1 m de profundidad
                bool blocked = Physics.Raycast(cam.transform.position, to.normalized, out RaycastHit hit,
                                               to.magnitude - 0.15f, mask, QueryTriggerInteraction.Ignore);
                sb.AppendLine($"    {lane.LaneId,-16} {p.name,-20} {(blocked ? "TAPADO por " + hit.collider.name : "visible")}");
            }
        }
        return sb.ToString();
    }
}
