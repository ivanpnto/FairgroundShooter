using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Rotura de la caseta en la revelación del boss (I7.4, §27.1). Va en la raíz de la caseta (Booth).
//
// La pared del fondo y la parte trasera del techo son piezas separadas en Blender (Booth_BackWall,
// Booth_RoofBack). Sus trozos rotos están hechos de antemano (Booth_Broken.fbx: celdas de Voronoi de esas
// mismas mallas, con sus materiales), así que aquí solo se cambian unas por otras y se lanza la física:
//   Crack()  → las piezas enteras se ocultan y aparecen los trozos, quietos pero separados unos centímetros:
//              se ven grietas (y, con una luz roja detrás, se cuela el rojo por ellas).
//   Burst()  → los trozos pasan a Rigidbody y salen despedidos desde el punto del impacto (el boss).
//   Al rato encogen y desaparecen: así no paran balas cuando empieza el combate.
//   ResetBooth() → todo como al principio (partida nueva).
//
// Física real con Rigidbody (§12): es uno de los conceptos de la asignatura que aporta el nivel 4.
public class BoothBreak : MonoBehaviour
{
    [Tooltip("Piezas enteras que se rompen (Booth_BackWall, Booth_RoofBack)")]
    [SerializeField] private GameObject[] intactParts;
    [Tooltip("Modelo con los trozos (Booth_Broken.fbx). Mismo origen que el de la caseta")]
    [SerializeField] private GameObject brokenPrefab;

    [Header("Grietas")]
    [Tooltip("Cuánto se separan los trozos del centro de su pieza al agrietarse (m)")]
    [SerializeField, Min(0f)] private float crackSpread = 0.06f;
    [Tooltip("Cuánto se adelantan hacia el jugador (m): la pared 'abomba' por el golpe desde detrás")]
    [SerializeField, Min(0f)] private float crackBulge = 0.12f;

    [Header("Reventón")]
    [SerializeField, Min(0f)] private float wallSpeed = 9f;
    [SerializeField, Min(0f)] private float roofSpeed = 7f;
    [Tooltip("Velocidad extra hacia arriba (m/s)")]
    [SerializeField, Min(0f)] private float upSpeed = 4f;
    [Tooltip("Giro máximo de cada trozo (rad/s)")]
    [SerializeField, Min(0f)] private float spin = 8f;
    [SerializeField, Min(0.1f)] private float pieceLifetime = 1.5f;
    [SerializeField, Min(0.05f)] private float shrinkTime = 0.35f;

    public bool IsBroken => broken != null;
    public event System.Action Cracked;
    public event System.Action<Vector3> Burst;   // punto del impacto

    private GameObject broken;
    private readonly List<Rigidbody> pieces = new List<Rigidbody>();

    public void Crack(Vector3 impactPoint)
    {
        if (broken != null || brokenPrefab == null)
            return;

        foreach (GameObject part in intactParts)
            if (part != null) part.SetActive(false);

        // Los trozos van donde estaba la pieza entera, bajo la raíz de la caseta y con la pose que trae su FBX.
        // Booth_Broken.fbx se exporta con pivotes por trozo (sin Bake Space Transform) y su raíz entra con
        // (90, 0, 0): forzarla a 0 los tumbaría (§6)
        broken = Instantiate(brokenPrefab, transform);
        broken.transform.localPosition = brokenPrefab.transform.localPosition;
        broken.transform.localRotation = brokenPrefab.transform.localRotation;

        pieces.Clear();
        foreach (MeshRenderer rend in broken.GetComponentsInChildren<MeshRenderer>())
        {
            GameObject go = rend.gameObject;
            go.AddComponent<BoxCollider>();   // la caja se ajusta sola a la malla
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.mass = 3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            // Grieta: se separa un poco del punto del golpe y se adelanta hacia el jugador (−Z)
            Vector3 away = go.transform.position - impactPoint;
            away.z = 0f;
            Vector3 offset = away.normalized * crackSpread + Vector3.back * crackBulge * Random.Range(0.5f, 1f);
            go.transform.position += offset;
            go.transform.Rotate(Random.insideUnitSphere * 3f, Space.World);
            pieces.Add(body);
        }
        Cracked?.Invoke();
    }

    public void BurstOut(Vector3 impactPoint)
    {
        if (broken == null) Crack(impactPoint);
        if (broken == null) return;

        foreach (Rigidbody body in pieces)
        {
            if (body == null) continue;
            body.isKinematic = false;

            bool roof = body.name.Contains("Roof");
            Vector3 dir = body.worldCenterOfMass - impactPoint;
            Vector3 velocity;
            if (roof)
            {
                // El techo sale hacia arriba y hacia los lados
                dir.y = Mathf.Abs(dir.y) + 2f;
                velocity = dir.normalized * roofSpeed * Random.Range(0.7f, 1.2f);
            }
            else
            {
                // La pared, hacia el jugador (−Z) y abriéndose hacia los lados
                dir.z = -Mathf.Abs(dir.z) - 3f;
                velocity = dir.normalized * wallSpeed * Random.Range(0.6f, 1.1f);
            }
            body.linearVelocity = velocity + Vector3.up * upSpeed * Random.Range(0.5f, 1f);
            body.angularVelocity = Random.insideUnitSphere * spin;
        }
        Burst?.Invoke(impactPoint);
        StartCoroutine(ShrinkLater());
    }

    private IEnumerator ShrinkLater()
    {
        yield return new WaitForSeconds(pieceLifetime);
        float t = 0f;
        var scales = new Dictionary<Transform, Vector3>();
        foreach (Rigidbody body in pieces)
            if (body != null) scales[body.transform] = body.transform.localScale;
        while (t < shrinkTime)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.SmoothStep(0f, 1f, t / shrinkTime);
            foreach (KeyValuePair<Transform, Vector3> entry in scales)
                if (entry.Key != null) entry.Key.localScale = entry.Value * k;
            yield return null;
        }
        if (broken != null) broken.SetActive(false);
    }

    // Partida nueva: pared y techo enteros, trozos fuera
    public void ResetBooth()
    {
        StopAllCoroutines();
        if (broken != null) Destroy(broken);
        broken = null;
        pieces.Clear();
        foreach (GameObject part in intactParts)
            if (part != null) part.SetActive(true);
    }
}
