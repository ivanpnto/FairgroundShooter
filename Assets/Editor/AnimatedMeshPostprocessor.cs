using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Prepara, AL IMPORTAR los FBX, los datos que necesitan los shaders animados del escenario (I4.2):
// escribe en el color de vértice de ciertas mallas un peso, un índice o un valor aleatorio POR PIEZA.
//
// Por qué aquí y no en Blender: la información depende solo de la geometría (qué triángulos forman
// cada banderín, dónde está su parte de arriba...), así que se puede calcular. Hacerlo en la
// importación significa que se rehace sola cada vez que se reexporta el FBX desde Blender, sin pasos
// manuales que olvidar.
//
// Una "pieza" (isla) = triángulos conectados entre sí. Los vértices duplicados por las aristas duras
// (misma posición, distinta normal) se sueldan por posición para que una pieza no se parta en caras.
public class AnimatedMeshPostprocessor : AssetPostprocessor
{
    private enum Rule
    {
        SwayFromTop,      // banderines: R = 0 arriba (cordel) → 1 abajo (punta)
        SwayFromBottom,   // algas:      R = 0 abajo (raíz)   → 1 arriba
        LightChain,       // bombillas:  R = orden de la bombilla en la guirnalda / 255
        Random            // luciérnagas: R aleatorio
    }

    // Nombre del objeto en el FBX → regla
    private static readonly Dictionary<string, Rule> Rules = new Dictionary<string, Rule>
    {
        { "Booth_Bunting", Rule.SwayFromTop },
        { "Sea_Kelp", Rule.SwayFromBottom },
        { "Booth_Bulbs", Rule.LightChain },
        { "Fair_Lights", Rule.LightChain },
        { "Forest_Fireflies", Rule.Random },
    };

    // Las piezas con más vértices que esto en los banderines son los cordeles: no se mueven
    private const int RopeVertexThreshold = 40;

    // Subir este número fuerza a Unity a reimportar los modelos afectados tras cambiar el código
    public override uint GetVersion() => 3;

    private void OnPostprocessModel(GameObject root)
    {
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || !Rules.TryGetValue(mf.gameObject.name, out Rule rule))
                continue;

            Renderer renderer = mf.GetComponent<Renderer>();
            Process(mf.sharedMesh, rule, renderer != null ? renderer.sharedMaterials : null);
        }
    }

    private static void Process(Mesh mesh, Rule rule, Material[] materials)
    {
        Vector3[] vertices = mesh.vertices;
        List<List<int>> islands = FindIslands(mesh, vertices, rule == Rule.LightChain ? materials : null);

        Color32[] colors = new Color32[vertices.Length];   // por defecto (0,0,0,0): quieto y apagado
        System.Random rng = new System.Random(mesh.vertexCount);   // semilla fija: mismo resultado en cada importación

        if (rule == Rule.LightChain)
        {
            List<int> order = ChainOrder(islands, vertices);
            for (int k = 0; k < order.Count; k++)
            {
                // 252 es múltiplo de 2, 3, 4 y 6: al dar la vuelta el patrón de encendido no salta
                byte index = (byte)(k % 252);
                byte seed = (byte)rng.Next(256);
                foreach (int v in islands[order[k]]) colors[v] = new Color32(index, seed, 0, 255);
            }
        }
        else
        {
            foreach (List<int> island in islands)
            {
                byte seed = (byte)rng.Next(256);
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (int v in island) { minY = Mathf.Min(minY, vertices[v].y); maxY = Mathf.Max(maxY, vertices[v].y); }
                float height = Mathf.Max(maxY - minY, 1e-4f);
                bool rope = rule == Rule.SwayFromTop && island.Count > RopeVertexThreshold;

                foreach (int v in island)
                {
                    float w;
                    switch (rule)
                    {
                        case Rule.SwayFromTop: w = rope ? 0f : (maxY - vertices[v].y) / height; break;
                        case Rule.SwayFromBottom: w = (vertices[v].y - minY) / height; w *= w; break;   // la raíz casi no se mueve
                        default: w = (float)rng.NextDouble(); break;
                    }
                    colors[v] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(w) * 255f), seed, 0, 255);
                }
            }
        }

        mesh.colors32 = colors;
    }

    // Unión-búsqueda: cada triángulo une sus tres vértices (soldados por posición) en una misma pieza.
    // Si se pasan materiales, solo cuentan los submeshes cuyo material contiene "Bulb" (en Fair_Lights
    // la misma malla tiene también los postes y el cable, que no son bombillas).
    private static List<List<int>> FindIslands(Mesh mesh, Vector3[] vertices, Material[] onlyBulbs)
    {
        int[] parent = new int[vertices.Length];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;

        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[b] = a; }

        // Soldar por posición (redondeo a 0,1 mm)
        Dictionary<Vector3Int, int> byPosition = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = Vector3Int.RoundToInt(vertices[i] * 10000f);
            if (byPosition.TryGetValue(key, out int first)) Union(first, i);
            else byPosition.Add(key, i);
        }

        bool[] used = new bool[vertices.Length];
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            if (onlyBulbs != null && (s >= onlyBulbs.Length || onlyBulbs[s] == null || !onlyBulbs[s].name.Contains("Bulb")))
                continue;

            int[] tris = mesh.GetTriangles(s);
            for (int t = 0; t < tris.Length; t += 3)
            {
                Union(tris[t], tris[t + 1]);
                Union(tris[t], tris[t + 2]);
                used[tris[t]] = used[tris[t + 1]] = used[tris[t + 2]] = true;
            }
        }

        Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < vertices.Length; i++)
        {
            if (!used[i]) continue;
            int r = Find(i);
            if (!groups.TryGetValue(r, out List<int> list)) groups.Add(r, list = new List<int>());
            list.Add(i);
        }
        return new List<List<int>>(groups.Values);
    }

    // Orden de las bombillas a lo largo de la guirnalda: se empieza por la de más a la izquierda y se
    // salta siempre a la más cercana que quede libre (vecino más próximo). Para cuerdas de bombillas
    // equiespaciadas sigue la cuerda; al acabar una, salta a la siguiente.
    private static List<int> ChainOrder(List<List<int>> islands, Vector3[] vertices)
    {
        List<Vector3> centers = new List<Vector3>();
        foreach (List<int> island in islands)
        {
            Vector3 c = Vector3.zero;
            foreach (int v in island) c += vertices[v];
            centers.Add(c / island.Count);
        }

        List<int> order = new List<int>();
        bool[] taken = new bool[centers.Count];
        int current = -1;
        for (int i = 0; i < centers.Count; i++)
            if (current < 0 || centers[i].x < centers[current].x) current = i;

        while (current >= 0)
        {
            order.Add(current);
            taken[current] = true;
            int next = -1;
            float best = float.MaxValue;
            for (int i = 0; i < centers.Count; i++)
            {
                if (taken[i]) continue;
                float d = (centers[i] - centers[current]).sqrMagnitude;
                if (d < best) { best = d; next = i; }
            }
            current = next;
        }
        return order;
    }
}
