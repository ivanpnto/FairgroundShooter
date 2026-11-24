#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Herramienta del trailer (solo editor): cámara de cine aparte de la del jugador, que hace travellings
// suaves entre dos encuadres (posición, punto al que mira y FOV) con un poco de cámara en mano.
public class TrailerCamRig : MonoBehaviour
{
    public Camera Cam { get; private set; }
    public float shake = 0.004f;
    private float seed;

    public static TrailerCamRig Get()
    {
        var r = FindFirstObjectByType<TrailerCamRig>();
        if (r != null) return r;
        var go = new GameObject("_TrailerCam");
        r = go.AddComponent<TrailerCamRig>();
        Camera src = FindFirstObjectByType<PlayerAim>().GetComponent<Camera>();
        r.Cam = go.AddComponent<Camera>();
        r.Cam.CopyFrom(src);
        r.Cam.depth = -50;
        var d = r.Cam.GetUniversalAdditionalCameraData();
        var sd = src.GetUniversalAdditionalCameraData();
        d.renderPostProcessing = true;
        d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        d.volumeLayerMask = sd.volumeLayerMask;
        d.volumeTrigger = go.transform;
        r.seed = Random.value * 100f;
        return r;
    }

    private void Awake() { if (Cam == null) Cam = GetComponent<Camera>(); }

    public void Place(Vector3 pos, Vector3 look, float fov)
    {
        StopAllCoroutines();
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
        Cam.fieldOfView = fov;
    }

    public void Move(Vector3 posA, Vector3 lookA, float fovA, Vector3 posB, Vector3 lookB, float fovB, float seconds, bool ease)
    {
        StopAllCoroutines();
        StartCoroutine(MoveRoutine(posA, lookA, fovA, posB, lookB, fovB, seconds, ease));
    }

    private IEnumerator MoveRoutine(Vector3 pa, Vector3 la, float fa, Vector3 pb, Vector3 lb, float fb, float seconds, bool ease)
    {
        for (float t = 0f; ; t += Time.deltaTime)
        {
            float u = Mathf.Clamp01(t / seconds);
            if (ease) u = u * u * (3f - 2f * u);
            Vector3 p = Vector3.Lerp(pa, pb, u);
            Vector3 l = Vector3.Lerp(la, lb, u);
            float n = Time.time * 0.6f + seed;
            Vector3 jitter = new Vector3(Mathf.PerlinNoise(n, 0.1f) - 0.5f, Mathf.PerlinNoise(0.3f, n) - 0.5f, 0f) * shake * 10f;
            transform.SetPositionAndRotation(p, Quaternion.LookRotation(l + jitter - p));
            Cam.fieldOfView = Mathf.Lerp(fa, fb, u);
            yield return null;
        }
    }
}
#endif
