using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Mide la imagen de mira de cada arma (J4.2) para poder reajustar scopeFov y eyeRelief con números
// y no a ojo. Menú: Tools > Fairground > Measure Sight Pictures. Solo lee: no modifica ningún asset.
//
// Simula lo que hace WeaponVisual al apuntar del todo: el ojo (la cámara) en el origen y el Sight en
// (0, 0, eyeRelief) con sus ejes alineados con los de la cámara. Así cada vértice del modelo pasa al
// espacio de la cámara con: espacio de la raíz → espacio del Sight → + (0, 0, eyeRelief).
//
// Por cada arma escribe en la consola:
// - qué fracción del ANCHO de pantalla ocupa el alza (objetivo: 25–35 %, igual en todas). Se mide
//   proyectando y no con la distancia, porque las alzas tienen anchos muy distintos;
// - qué parte de la pantalla tapa el arma (y de la mitad de arriba, donde se apunta);
// - el vértice visible más cercano al ojo (debe quedar por encima del plano cercano);
// - el ángulo de la línea de mira respecto al cañón (Sight frente a Muzzle);
// - si la punta del guion se ve: rayo desde el ojo hasta ella contra los triángulos del modelo.
public static class SightPictureTool
{
    private const float ReferenceAspect = 16f / 9f;
    private const float NearClip = 0.01f;   // el de la cámara de Player

    // La punta del guion se busca sola, sin datos por arma: es el vértice más lejano que cae sobre el
    // eje de la cámara, porque el Sight está definido por la recta alza → punta del guion. En vertical
    // la tolerancia es pequeña (la punta está en la recta); en lateral, mayor, porque los vértices de
    // la punta están a los lados del eje (el guion de la FiveSeven mide 2,7 mm de ancho). Los hombros
    // del alza, la capucha del guion o la boca quedan muy fuera de estas tolerancias
    private const float TipMinDistance = 0.05f;   // m por delante del Sight
    private const float TipMaxSlopeX = 0.008f;    // rad
    private const float TipMaxSlopeY = 0.0015f;   // rad

    // Los cortes a menos de esta distancia de la punta son el propio guion (el rayo roza su cara
    // superior o, el que va por debajo de la punta, entra por ella), no un obstáculo. Mayor que el
    // guion más largo: la cara superior del de la FiveSeven mide 2,8 mm
    private const float TipTolerance = 0.005f;

    // Cuánto guion por debajo de la punta tiene que verse para darlo por visible
    private const float TipVisibleDepth = 0.0005f;

    // Con la malla original de PolyOne, el tambor del dióptero del MP5 tapa el guion: es un aviso esperado
    // hasta que el prefab use la malla corregida (HK_MP5_Body_Fixed)
    private const string ExpectedObstructedMesh = "HK_MP5_Body";

    // "Alza" = lo que rodea al guion en la imagen: los vértices de los primeros 3 cm por delante del
    // Sight (la cara trasera está a 1 cm) y a menos de 5 mm de la línea de mira. Con una franja más
    // ancha entrarían el asa del M16, las orejas del tambor del MP5 o la corredera de la FiveSeven
    private const float AlzaDepth = 0.03f;
    private const float AlzaBand = 0.005f;

    // Resolución de la rejilla con la que se rasteriza el arma para medir cuánto tapa (16:9)
    private const int GridW = 160;
    private const int GridH = 90;

    [MenuItem("Tools/Fairground/Measure Sight Pictures")]
    public static void Measure()
    {
        var report = new StringBuilder("Imagen de mira de cada arma (16:9, apuntado completo):\n");

        foreach (string guid in AssetDatabase.FindAssets("t:WeaponData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
            report.AppendLine(MeasureWeapon(data));
        }

        Debug.Log(report.ToString());
    }

    private static string MeasureWeapon(WeaponData data)
    {
        string header = $"• {data.displayName} ({data.scopeType}, scopeFov {data.scopeFov}, eyeRelief {data.eyeRelief:F4})";

        WeaponModel model = data.modelPrefab != null ? data.modelPrefab.GetComponent<WeaponModel>() : null;
        if (model == null || model.Sight == null)
            return header + ": sin Sight, no apunta";

        // Del espacio de la raíz al de la cámara. Se usa la raíz del prefab y no su transform en el
        // mundo, para que no influya dónde esté colocado el asset
        Transform root = model.transform;
        Matrix4x4 rootToSight = Matrix4x4.TRS(
            root.InverseTransformPoint(model.Sight.position),
            Quaternion.Inverse(root.rotation) * model.Sight.rotation,
            Vector3.one).inverse;
        Matrix4x4 sightToEye = Matrix4x4.Translate(new Vector3(0f, 0f, data.eyeRelief));

        var points = new List<Vector3>();
        var triangles = new List<int>();
        CollectMeshes(root, sightToEye * rootToSight, points, triangles);

        float tanV = Mathf.Tan(data.scopeFov * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * ReferenceAspect;

        // 1. Ancho del alza en pantalla
        float xMin = float.MaxValue, xMax = float.MinValue;
        foreach (Vector3 p in points)
        {
            float alongSight = p.z - data.eyeRelief;
            if (alongSight < 0f || alongSight > AlzaDepth || Mathf.Abs(p.y) > AlzaBand)
                continue;
            float x = p.x / p.z;
            xMin = Mathf.Min(xMin, x);
            xMax = Mathf.Max(xMax, x);
        }
        string alza = xMax > xMin ? $"alza {(xMax - xMin) / (2f * tanH):P1} del ancho" : "alza no encontrada";

        // 2. Vértice visible más cercano (dentro del frustum)
        float nearest = float.MaxValue;
        foreach (Vector3 p in points)
            if (p.z > 0f && Mathf.Abs(p.x / p.z) < tanH && Mathf.Abs(p.y / p.z) < tanV)
                nearest = Mathf.Min(nearest, p.z);
        string near = nearest < NearClip
            ? $"¡RECORTADO! vértice visible a {nearest:F4} m (< {NearClip})"
            : $"vértice visible más cercano a {nearest:F3} m";

        // 3. Cuánto tapa el arma
        float covered, coveredTop;
        Rasterize(points, triangles, tanH, tanV, out covered, out coveredTop);

        // 4. Ángulo de la línea de mira respecto al cañón. Positivo = al apuntar, la boca queda por
        // encima de la línea de mira (lo normal: la recta alza → guion baja hacia la boca)
        string angle = "";
        if (model.Muzzle != null)
        {
            Vector3 barrel = Quaternion.Inverse(model.Sight.rotation) * model.Muzzle.forward;
            angle = $", línea de mira {Mathf.Atan2(barrel.y, barrel.z) * Mathf.Rad2Deg:F2}° respecto al cañón";
        }

        // 5. Visibilidad de la punta del guion (solo miras abiertas: el visor no tiene guion)
        string sight = data.scopeType == ScopeType.IronSights
            ? CheckFrontSight(data, points, triangles, tanH, tanV, UsesMesh(root, ExpectedObstructedMesh))
            : "visor: sin guion que comprobar";

        return $"{header}: {alza}, arma {covered:P1} de la pantalla ({coveredTop:P1} de la mitad de arriba), {near}{angle}, {sight}";
    }

    // Busca la punta del guion y lanza un rayo desde el ojo (el origen) hasta ella. Si un triángulo del
    // modelo lo corta antes, el guion queda tapado: se avisa también con un warning para que no pase
    // desapercibido en el informe
    private static string CheckFrontSight(WeaponData data, List<Vector3> points, List<int> triangles,
        float tanH, float tanV, bool obstructionExpected)
    {
        var candidates = new List<Vector3>();
        float farthest = float.MinValue;
        foreach (Vector3 p in points)
        {
            if (p.z < data.eyeRelief + TipMinDistance || Mathf.Abs(p.x / p.z) > TipMaxSlopeX || Mathf.Abs(p.y / p.z) > TipMaxSlopeY)
                continue;
            candidates.Add(p);
            farthest = Mathf.Max(farthest, p.z);
        }

        // Los vértices de la punta van por pares a los lados del eje: se promedian los del último
        // milímetro para tomar el centro de la punta y no una de sus esquinas
        Vector3 tip = Vector3.zero;
        int count = 0;
        foreach (Vector3 p in candidates)
            if (p.z > farthest - 0.001f)
            {
                tip += p;
                count++;
            }
        if (count == 0)
        {
            Debug.LogWarning($"{data.displayName}: no se encuentra la punta del guion sobre el eje de la cámara (¿Sight mal colocado?)");
            return "¡punta del guion NO encontrada en el eje!";
        }
        tip /= count;

        // Dónde queda la punta en pantalla, en % de media pantalla (0 = centro exacto)
        string onScreen = $"punta del guion a {tip.z:F3} m, desvío ({tip.x / tip.z / tanH:P2}, {tip.y / tip.z / tanV:P2})";

        // Dos rayos: a la punta y a TipVisibleDepth por debajo. Con solo el primero, una línea que roza
        // el fondo de la muesca daría "visible" aunque en pantalla apenas asome el borde del guion
        float obstacle = Mathf.Min(
            FirstObstacle(tip, points, triangles),
            FirstObstacle(tip + Vector3.down * TipVisibleDepth, points, triangles));
        float tipDistance = tip.magnitude;

        if (obstacle == float.MaxValue)
            return $"{onScreen}, guion VISIBLE";

        string message = $"{data.displayName}: el propio modelo tapa la punta del guion a {obstacle:F4} m del ojo (guion a {tipDistance:F4} m)";
        if (obstructionExpected)
            return $"{onScreen}, guion TAPADO a {obstacle:F4} m del ojo (esperado: malla original del dióptero)";
        Debug.LogWarning(message);
        return $"{onScreen}, ¡guion TAPADO a {obstacle:F4} m del ojo!";
    }

    // Distancia desde el ojo al primer triángulo que corta el rayo hacia target antes de llegar a él
    // (float.MaxValue si no hay ninguno)
    private static float FirstObstacle(Vector3 target, List<Vector3> points, List<int> triangles)
    {
        float distance = target.magnitude;
        Vector3 direction = target / distance;
        float obstacle = float.MaxValue;
        for (int t = 0; t < triangles.Count; t += 3)
        {
            float d;
            if (RayTriangle(Vector3.zero, direction, points[triangles[t]], points[triangles[t + 1]], points[triangles[t + 2]], out d)
                && d < distance - TipTolerance)
                obstacle = Mathf.Min(obstacle, d);
        }
        return obstacle;
    }

    private static bool UsesMesh(Transform root, string meshName)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null && filter.sharedMesh.name == meshName)
                return true;
        return false;
    }

    // Vértices (ya en el espacio de la cámara) y triángulos de todas las mallas visibles del modelo
    private static void CollectMeshes(Transform root, Matrix4x4 rootToEye, List<Vector3> points, List<int> triangles)
    {
        // includeInactive = true y activeSelf a mano: en un prefab sin instanciar activeInHierarchy
        // es siempre false, así que GetComponentsInChildren(false) no devolvería nada
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (filter.sharedMesh == null || renderer == null || !renderer.enabled || !IsActiveUnder(filter.transform, root))
                continue;

            Matrix4x4 toEye = rootToEye * root.worldToLocalMatrix * filter.transform.localToWorldMatrix;

            // Las mallas de PolyOne no tienen Read/Write: AcquireReadOnlyMeshData sí puede leerlas
            using (Mesh.MeshDataArray meshData = Mesh.AcquireReadOnlyMeshData(filter.sharedMesh))
            {
                Mesh.MeshData mesh = meshData[0];
                int first = points.Count;

                var vertices = new Unity.Collections.NativeArray<Vector3>(mesh.vertexCount, Unity.Collections.Allocator.Temp);
                mesh.GetVertices(vertices);
                foreach (Vector3 v in vertices)
                    points.Add(toEye.MultiplyPoint3x4(v));
                vertices.Dispose();

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var indices = new Unity.Collections.NativeArray<int>(mesh.GetSubMesh(s).indexCount, Unity.Collections.Allocator.Temp);
                    mesh.GetIndices(indices, s);
                    foreach (int i in indices)
                        triangles.Add(first + i);
                    indices.Dispose();
                }
            }
        }
    }

    // Las balas sueltas de las variantes están desactivadas (override): no deben contar
    private static bool IsActiveUnder(Transform t, Transform root)
    {
        for (; t != null && t != root; t = t.parent)
            if (!t.gameObject.activeSelf)
                return false;
        return root.gameObject.activeSelf;
    }

    // Rasteriza los triángulos en una rejilla 16:9 y devuelve la fracción tapada (total y mitad de
    // arriba). Los triángulos que cruzan el plano cercano se descartan: la medida es aproximada
    private static void Rasterize(List<Vector3> points, List<int> triangles, float tanH, float tanV,
        out float covered, out float coveredTop)
    {
        var grid = new bool[GridW * GridH];

        for (int t = 0; t < triangles.Count; t += 3)
        {
            Vector3 a = points[triangles[t]], b = points[triangles[t + 1]], c = points[triangles[t + 2]];
            if (a.z < NearClip || b.z < NearClip || c.z < NearClip)
                continue;

            Vector2 A = ToGrid(a, tanH, tanV), B = ToGrid(b, tanH, tanV), C = ToGrid(c, tanH, tanV);
            float area = (B.x - A.x) * (C.y - A.y) - (C.x - A.x) * (B.y - A.y);
            if (Mathf.Abs(area) < 1e-9f)
                continue;

            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.x, Mathf.Min(B.x, C.x))));
            int x1 = Mathf.Min(GridW - 1, Mathf.CeilToInt(Mathf.Max(A.x, Mathf.Max(B.x, C.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.y, Mathf.Min(B.y, C.y))));
            int y1 = Mathf.Min(GridH - 1, Mathf.CeilToInt(Mathf.Max(A.y, Mathf.Max(B.y, C.y))));

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    // Coordenadas baricéntricas del centro de la celda
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((B.x - px) * (C.y - py) - (C.x - px) * (B.y - py)) / area;
                    float w1 = ((C.x - px) * (A.y - py) - (A.x - px) * (C.y - py)) / area;
                    if (w0 >= 0f && w1 >= 0f && w0 + w1 <= 1f)
                        grid[y * GridW + x] = true;
                }
        }

        int all = 0, top = 0;
        for (int i = 0; i < grid.Length; i++)
            if (grid[i])
            {
                all++;
                if (i / GridW >= GridH / 2) top++;
            }

        covered = all / (float)grid.Length;
        coveredTop = top / (grid.Length * 0.5f);
    }

    private static Vector2 ToGrid(Vector3 p, float tanH, float tanV) =>
        new Vector2((p.x / p.z / tanH + 1f) * 0.5f * GridW, (p.y / p.z / tanV + 1f) * 0.5f * GridH);

    // Möller–Trumbore, sin descartar caras traseras: el rayo debe ver también las paredes interiores
    private static bool RayTriangle(Vector3 origin, Vector3 dir, Vector3 a, Vector3 b, Vector3 c, out float distance)
    {
        distance = 0f;
        Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(dir, e2);
        float det = Vector3.Dot(e1, p);
        if (Mathf.Abs(det) < 1e-12f)
            return false;

        float inv = 1f / det;
        Vector3 s = origin - a;
        float u = Vector3.Dot(s, p) * inv;
        if (u < 0f || u > 1f)
            return false;

        Vector3 q = Vector3.Cross(s, e1);
        float v = Vector3.Dot(dir, q) * inv;
        if (v < 0f || u + v > 1f)
            return false;

        distance = Vector3.Dot(e2, q) * inv;
        return distance > 0f;
    }
}
