using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Modelo de la almeja del Fondo marino (ClamTarget) y sus sitios. Menú: Tools > Fairground > Build Sea Clam.
//
// Antes la almeja eran dos esferas aplastadas con material Unlit: no casaba con el estilo «chapa» del resto del mar
// y se confundía con una piedra. Ahora cada concha es una cúpula baja con estrías radiales, borde ondulado y franjas
// de dos tonos (coral y crema: con la luz azul del fondo marino un malva se volvía azul oscuro y no se veía), con
// sombreado plano, contorno oscuro (técnica del casco invertido: la misma malla un poco mayor y vuelta del revés,
// con el material de contorno de las dianas, M_T_Outline) y el interior rosa nacarado.
//
// Se genera aquí, por código, y no en Blender: es una forma paramétrica (cúpula + estrías), y así se retoca
// cambiando un número. Las mallas se guardan como assets en Art/Models/Targets/SeaClam.
//
// Qué hace (se puede repetir):
//   · Crea o actualiza las mallas y los materiales M_S_ClamShell, M_S_ClamShellRib, M_S_ClamInside, M_S_Pearl.
//   · Prefab SeaClam: pone las mallas en Bottom y Top, el contorno como hijos, cierra bien las conchas, recoloca la
//     perla y asigna el material del destello.
//   · En la escena abierta: los sitios de la almeja a lo largo de la arena delantera.
public static class SeaClamBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Sea/SeaClam.prefab";
    private const string MeshFolder = "Assets/Art/Models/Targets/SeaClam";
    private const string MaterialFolder = "Assets/Art/Materials/Sea/";
    private const string LitShader = "Universal Render Pipeline/Lit";

    // Medidas de cada concha (m)
    private const float Width = 0.52f, Depth = 0.42f, Height = 0.075f;
    private const int Ribs = 9, Segments = 72, Rings = 6;

    // Sitios a lo largo del borde delantero de la arena (misma línea que el sitio original, -2,7 / 0,415 / 2,2),
    // repartidos de lado a lado sin tapar los carriles de la tortuga y las medusas
    private static readonly Vector3[] Spots =
    {
        new Vector3(-4.3f, 0.415f, 2.2f), new Vector3(-2.7f, 0.415f, 2.2f), new Vector3(-0.9f, 0.415f, 2.2f),
        new Vector3(1.0f, 0.415f, 2.2f), new Vector3(2.8f, 0.415f, 2.2f), new Vector3(4.3f, 0.415f, 2.2f),
    };

    [MenuItem("Tools/Fairground/Build Sea Clam")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Sea Clam",
                "Genera el modelo de la almeja, actualiza el prefab SeaClam y pone sus sitios en la escena abierta.\n\n" +
                "En Main, avisa antes a tu compañero. ¿Continuar?", "Montar", "Cancelar"))
            return;
        Run();
    }

    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(MeshFolder))
            AssetDatabase.CreateFolder("Assets/Art/Models/Targets", "SeaClam");

        Material shell = LitMaterial("M_S_ClamShell", new Color(0.93f, 0.4f, 0.34f), 0.2f);
        Material rib = LitMaterial("M_S_ClamShellRib", new Color(1f, 0.8f, 0.6f), 0.2f);
        Material inside = LitMaterial("M_S_ClamInside", new Color(0.99f, 0.62f, 0.74f), 0.55f);
        Material pearl = LitMaterial("M_S_Pearl", new Color(1f, 0.97f, 0.95f), 0.85f);
        // Algo de brillo propio: con la luz azul del fondo marino una perla solo iluminada se veía gris azulada
        pearl.EnableKeyword("_EMISSION");
        pearl.SetColor("_EmissionColor", new Color(0.55f, 0.5f, 0.5f));
        pearl.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        Material outline = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Targets/M_T_Outline.mat");
        Material glint = AssetDatabase.LoadAssetAtPath<Material>(FindMaterial("MuzzleFlash_Additive"));

        Mesh bottomMesh = SaveMesh(BuildShell(false, 1f, false), "SeaClam_Bottom");
        Mesh topMesh = SaveMesh(BuildShell(true, 1f, false), "SeaClam_Top");
        Mesh bottomOutline = SaveMesh(BuildShell(false, 1.07f, true), "SeaClam_Bottom_Outline");
        Mesh topOutline = SaveMesh(BuildShell(true, 1.07f, true), "SeaClam_Top_Outline");

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        Transform visual = root.transform.Find("Visual");
        Transform bottom = visual.Find("Bottom");
        Transform hinge = visual.Find("Hinge");
        Transform top = hinge.Find("Top");
        Transform oldInside = visual.Find("Inside");
        Transform anchor = root.transform.Find("PearlAnchor");

        // Las dos conchas se tocan en el borde (y = 0,05 del prefab); la de arriba cuelga de la bisagra, atrás
        const float rimY = 0.05f;
        SetShell(bottom, bottomMesh, bottomOutline, new Vector3(0f, rimY, 0f), shell, rib, inside, outline);
        SetShell(top, topMesh, topOutline, new Vector3(0f, rimY - hinge.localPosition.y, -hinge.localPosition.z), shell, rib, inside, outline);
        if (oldInside != null) oldInside.gameObject.SetActive(false);   // el interior va ahora en la malla de la concha
        if (anchor != null) anchor.localPosition = new Vector3(0f, rimY + 0.08f, -0.02f);

        // Un 35 % mayor que las esferas de antes: a 7 m de la cámara ocupaba unos pocos píxeles
        root.transform.localScale = Vector3.one * 1.35f;

        ClamTarget clam = root.GetComponent<ClamTarget>();
        if (clam != null)
        {
            var so = new SerializedObject(clam);
            so.FindProperty("glintMaterial").objectReferenceValue = glint;
            // Abierta más de 90°: la concha de arriba queda de pie y enseña su interior rosa a la cámara
            so.FindProperty("openAngle").floatValue = -105f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        // La perla: el material nuevo (Lit, muy liso, brilla con la luz de la almeja)
        var pearlPrefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Sea/PearlTarget.prefab");
        foreach (Renderer r in pearlPrefab.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = pearl;
        PrefabUtility.SaveAsPrefabAsset(pearlPrefab, "Assets/Prefabs/Sea/PearlTarget.prefab");
        PrefabUtility.UnloadPrefabContents(pearlPrefab);

        // Sitios en la escena (la instancia de SeaClam)
        ClamTarget instance = Object.FindFirstObjectByType<ClamTarget>(FindObjectsInactive.Include);
        if (instance != null)
        {
            var so = new SerializedObject(instance);
            SerializedProperty spots = so.FindProperty("spots");
            spots.arraySize = Spots.Length;
            for (int i = 0; i < Spots.Length; i++) spots.GetArrayElementAtIndex(i).vector3Value = Spots[i];
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
            EditorSceneManager.SaveScene(instance.gameObject.scene);
        }
        else Debug.LogWarning("SeaClamBuilder: no hay ninguna almeja (ClamTarget) en la escena abierta; los sitios no se han puesto.");

        AssetDatabase.SaveAssets();
        Debug.Log("SeaClamBuilder: almeja montada.");
    }

    private static void SetShell(Transform shellT, Mesh mesh, Mesh outlineMesh, Vector3 localPos, Material shell, Material rib, Material inside, Material outline)
    {
        shellT.localPosition = localPos;
        shellT.localRotation = Quaternion.identity;
        shellT.localScale = Vector3.one;
        shellT.GetComponent<MeshFilter>().sharedMesh = mesh;
        shellT.GetComponent<MeshRenderer>().sharedMaterials = new[] { shell, rib, inside };

        Transform o = shellT.Find("Outline");
        if (o == null)
        {
            o = new GameObject("Outline", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            o.SetParent(shellT, false);
        }
        o.localPosition = Vector3.zero; o.localRotation = Quaternion.identity; o.localScale = Vector3.one;
        o.GetComponent<MeshFilter>().sharedMesh = outlineMesh;
        var renderer = o.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = outline;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ---------------------------------------------------------------- malla

    // Cúpula baja con estrías. up = concha de arriba (la cúpula mira hacia arriba y el interior hacia abajo);
    // la de abajo es la misma, boca arriba (cuenco). scale > 1 + inverted = casco del contorno.
    // Submallas: 0 y 1 = franjas de la cara de fuera; 2 = interior (no lo lleva el contorno)
    private static Mesh BuildShell(bool up, float scale, bool inverted)
    {
        float sign = up ? 1f : -1f;
        var verts = new List<Vector3>();
        var tris = new[] { new List<int>(), new List<int>(), new List<int>() };

        // Cara de fuera: anillos desde el centro (rho = 0) hasta el borde (rho = 1), ver Outer
        for (int s = 0; s < Segments; s++)
        {
            float t0 = s / (float)Segments * Mathf.PI * 2f;
            float t1 = (s + 1) / (float)Segments * Mathf.PI * 2f;
            int band = Mathf.FloorToInt((t0 + t1) * 0.5f / (Mathf.PI * 2f) * Ribs * 2f) % 2;
            for (int r = 0; r < Rings; r++)
            {
                float r0 = r / (float)Rings, r1 = (r + 1) / (float)Rings;
                Vector3 a = Outer(t0, r0, sign, scale), b = Outer(t1, r0, sign, scale), c = Outer(t1, r1, sign, scale), d = Outer(t0, r1, sign, scale);
                if (r == 0) AddTri(verts, tris[band], a, c, d, up, inverted);
                else AddQuad(verts, tris[band], a, b, c, d, up, inverted);
            }
        }

        // Interior: un disco casi plano, algo hundido, mirando al lado contrario de la cúpula
        if (!inverted)
        {
            for (int s = 0; s < Segments; s++)
            {
                float t0 = s / (float)Segments * Mathf.PI * 2f;
                float t1 = (s + 1) / (float)Segments * Mathf.PI * 2f;
                Vector3 center = new Vector3(0f, sign * Height * 0.35f, 0f);
                Vector3 e0 = Outer(t0, 0.96f, sign, scale); e0.y = sign * 0.004f;
                Vector3 e1 = Outer(t1, 0.96f, sign, scale); e1.y = sign * 0.004f;
                AddTri(verts, tris[2], center, e1, e0, !up, false);
            }
        }

        var mesh = new Mesh { name = (up ? "SeaClam_Top" : "SeaClam_Bottom") + (inverted ? "_Outline" : "") };
        mesh.SetVertices(verts);
        mesh.subMeshCount = inverted ? 1 : 3;
        if (inverted)
        {
            var all = new List<int>(tris[0]); all.AddRange(tris[1]);
            mesh.SetTriangles(all, 0);
        }
        else for (int i = 0; i < 3; i++) mesh.SetTriangles(tris[i], i);
        mesh.RecalculateNormals();   // vértices sin compartir: sombreado plano, como el resto del arte low-poly
        mesh.RecalculateBounds();
        return mesh;
    }

    // Punto de la cara de fuera de la cúpula para el ángulo theta (0 = atrás, en la bisagra) y el radio rho (0..1)
    private static Vector3 Outer(float theta, float rho, float sign, float scale)
    {
        float rim = 1f + 0.05f * Mathf.Cos(Ribs * theta);   // borde ondulado: una onda por estría
        float x = rho * Width * 0.5f * rim * Mathf.Sin(theta);
        float z = rho * Depth * 0.5f * rim * Mathf.Cos(theta);
        float dome = Height * Mathf.Pow(Mathf.Max(0f, 1f - rho * rho), 0.7f);
        float ridge = 0.012f * rho * Mathf.Cos(Ribs * theta);
        float y = sign * (dome + ridge + (scale > 1f ? 0.006f : 0f));
        return new Vector3(x * scale, y * (scale > 1f ? 1.12f : 1f), z * scale);
    }

    // Vértices propios por triángulo (sombreado plano). Las caras miran hacia fuera de la cúpula; el contorno, al revés
    private static void AddTri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, bool up, bool inverted)
    {
        bool flip = up == inverted;
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        if (flip) { tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); }
        else { tris.Add(i); tris.Add(i + 2); tris.Add(i + 1); }
    }

    private static void AddQuad(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool up, bool inverted)
    {
        AddTri(verts, tris, a, b, c, up, inverted);
        AddTri(verts, tris, a, c, d, up, inverted);
    }

    // ---------------------------------------------------------------- assets

    private static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = $"{MeshFolder}/{name}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        existing.Clear();
        EditorUtility.CopySerialized(mesh, existing);   // misma ruta y GUID: el prefab no pierde la referencia
        existing.name = name;
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // Material Lit con el color ya en sRGB, como el resto del proyecto (§6). Si existe se reutiliza (mismo GUID)
    private static Material LitMaterial(string name, Color color, float smoothness)
    {
        string path = MaterialFolder + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader lit = Shader.Find(LitShader);
        if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, path); }
        else if (mat.shader != lit) mat.shader = lit;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static string FindMaterial(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Material"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return path;
        }
        return null;
    }
}
