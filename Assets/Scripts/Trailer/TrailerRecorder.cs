#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Herramienta del trailer (solo editor, no entra en la build). Graba planos deterministas: con
// Time.captureFramerate el juego avanza 1/30 s por frame aunque el editor vaya más lento, y cada frame
// se renderiza a 1920×1080 en una RenderTexture y se guarda como JPEG en Recordings/Trailer/<plano>/.
// El sonido del juego (efectos y ambiente, sin la música, que se monta aparte) se graba a la vez con
// AudioRenderer en un .f32 (float estéreo intercalado) sincronizado frame a frame con la imagen.
// La UI en Screen Space - Overlay no sale en una RenderTexture: mientras graba, los canvas raíz pasan a
// Screen Space - Camera sobre la cámara grabada, y al acabar se dejan como estaban.
public class TrailerRecorder : MonoBehaviour
{
    public int width = 1920, height = 1080, fps = 30, jpgQuality = 92;
    public string root;
    [Tooltip("Cámara a grabar. Vacía = la cámara del juego que esté activa (jugador o tienda)")]
    public Camera overrideCamera;
    [Tooltip("Oculta puntos, tiempo, munición y racha del HUD (deja mira, corazones y barra del boss)")]
    public bool hideHudText = true;

    public bool Recording { get; private set; }
    public int Frame { get; private set; }
    public string Shot { get; private set; }

    private RenderTexture rt;
    private Texture2D tex;
    private FileStream audioFile;
    private int channels = 2;
    private readonly Dictionary<Canvas, RenderMode> savedModes = new Dictionary<Canvas, RenderMode>();
    private readonly List<Camera> touched = new List<Camera>();
    private readonly List<GameObject> hidden = new List<GameObject>();
    private Camera current;

    public static TrailerRecorder Get()
    {
        var r = FindFirstObjectByType<TrailerRecorder>();
        if (r == null) r = new GameObject("_TrailerRecorder").AddComponent<TrailerRecorder>();
        return r;
    }

    private void Awake()
    {
        if (string.IsNullOrEmpty(root)) root = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Recordings", "Trailer");
        BossController boss = FindFirstObjectByType<BossController>();
        if (boss != null)
        {
            boss.StateChanged += st => Mark(st + " " + boss.CurrentAttack);
            boss.HonkResolved += ok => Mark("honk " + ok);
            boss.LaserResolved += dodged => Mark("jet dodged=" + dodged);
            boss.PhaseStarted += ph => Mark("phase " + ph);
        }
    }

    public void Begin(string shot)
    {
        if (Recording) End();
        Shot = shot;
        string dir = Path.Combine(root, shot);
        if (Directory.Exists(dir)) foreach (string f in Directory.GetFiles(dir)) File.Delete(f);
        Directory.CreateDirectory(dir);
        if (rt == null)
        {
            rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        }
        Frame = 0;
        Time.captureFramerate = fps;
        channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
        audioFile = new FileStream(Path.Combine(dir, "audio.f32"), FileMode.Create);
        AudioRenderer.Start();
        Recording = true;
        ApplyHud(true);
        StartCoroutine(CaptureLoop(dir));
    }

    // Graba un plano de duración fija (en segundos de juego) y para solo
    public void RecordFor(string shot, float seconds)
    {
        Begin(shot);
        StartCoroutine(StopAfter(Mathf.RoundToInt(seconds * fps)));
    }

    private IEnumerator StopAfter(int frames)
    {
        while (Recording && Frame < frames) yield return null;
        End();
    }

    // Apunta en marks.txt el frame en que pasa algo (ataques del boss…), para encontrarlo al montar
    public void Mark(string label)
    {
        if (!Recording) return;
        File.AppendAllText(Path.Combine(root, Shot, "marks.txt"), Frame + " " + label + "\n");
    }

    public void End()
    {
        if (!Recording) return;
        Recording = false;
        StopAllCoroutines();
        Debug.Log("[Trailer] " + Shot + ": " + Frame + " frames");
        AudioRenderer.Stop();
        if (audioFile != null)
        {
            audioFile.Close();
            File.WriteAllText(Path.Combine(root, Shot, "info.txt"),
                "frames=" + Frame + "\nfps=" + fps + "\nrate=" + AudioSettings.outputSampleRate + "\nchannels=" + channels + "\n");
        }
        audioFile = null;
        Time.captureFramerate = 0;
        foreach (Camera c in touched) if (c != null) c.targetTexture = null;
        touched.Clear();
        foreach (var kv in savedModes) if (kv.Key != null) kv.Key.renderMode = kv.Value;
        savedModes.Clear();
        ApplyHud(false);
        current = null;
    }

    private void ApplyHud(bool hide)
    {
        if (hide && hideHudText)
        {
            GameObject hud = GameObject.Find("UI/HUD");
            if (hud == null) return;
            foreach (string n in new[] { "ScoreText", "TimerText", "AmmoText", "StreakText" })
            {
                Transform t = hud.transform.Find(n);
                if (t != null && t.gameObject.activeSelf) { t.gameObject.SetActive(false); hidden.Add(t.gameObject); }
            }
        }
        else if (!hide)
        {
            foreach (GameObject g in hidden) if (g != null) g.SetActive(true);
            hidden.Clear();
        }
    }

    private Camera PickCamera()
    {
        if (overrideCamera != null && overrideCamera.isActiveAndEnabled) return overrideCamera;
        Camera best = null;
        foreach (Camera c in Camera.allCameras)
        {
            if (c.name.StartsWith("_Trailer")) continue;
            if (best == null || c.depth > best.depth) best = c;
        }
        return best;
    }

    // Antes de renderizar: la cámara elegida dibuja en la RenderTexture, las demás a pantalla
    private void LateUpdate()
    {
        if (!Recording) return;
        Camera cam = PickCamera();
        if (cam != current)
        {
            foreach (Camera c in touched) if (c != null) c.targetTexture = null;
            touched.Clear();
            if (cam != null) { cam.targetTexture = rt; touched.Add(cam); }
            current = cam;
        }
        foreach (Canvas cv in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!cv.isRootCanvas || cv.renderMode == RenderMode.WorldSpace) continue;
            if (!savedModes.ContainsKey(cv)) savedModes[cv] = cv.renderMode;
            if (savedModes[cv] != RenderMode.ScreenSpaceOverlay) continue;
            cv.renderMode = RenderMode.ScreenSpaceCamera;
            cv.worldCamera = cam;
            cv.planeDistance = cam != null ? cam.nearClipPlane + 0.02f : 1f;
        }
    }

    private IEnumerator CaptureLoop(string dir)
    {
        while (Recording)
        {
            yield return new WaitForEndOfFrame();
            if (!Recording) yield break;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, "f" + Frame.ToString("00000") + ".jpg"), tex.EncodeToJPG(jpgQuality));

            int samples = AudioRenderer.GetSampleCountForCaptureFrame();
            if (samples > 0)
            {
                var buf = new NativeArray<float>(samples * channels, Allocator.Temp);
                AudioRenderer.Render(buf);
                byte[] bytes = new byte[buf.Length * 4];
                System.Buffer.BlockCopy(buf.ToArray(), 0, bytes, 0, bytes.Length);
                audioFile.Write(bytes, 0, bytes.Length);
                buf.Dispose();
            }
            Frame++;
        }
    }

    private void OnDisable() => End();
}
#endif
