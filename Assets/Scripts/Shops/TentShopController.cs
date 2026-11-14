using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

// Base de las tiendas en 3D (Fase 8): la armería y la botica.
//
// Entrar: la cámara de la tienda sale exactamente desde la del jugador, recorre la feria por una curva y se
// cuela por la puerta; a oscuras un instante, corta al interior. Salir: fundido a negro y vuelta al menú.
//
// Los interiores están bajo el suelo de la feria (y = −60), cerrados por la lona: la cámara del juego no los ve.
// Se aíslan de cada nivel en tres frentes:
//  · luces: sus renderers y luces usan solo la Rendering Layer «Armory»; las de los niveles, solo Default;
//  · ambiente y niebla (RenderSettings, globales): se guardan al entrar, se cambian y se restauran al salir;
//  · post-procesado: la cámara de la tienda, ya dentro, solo mira la capa Armory, y en esa capa cada tienda
//    enciende su Volume solo mientras está abierta (si los dos Volumes globales estuvieran activos a la vez,
//    URP los mezclaría).
//
// La hija decide qué se vende: repinta los huecos (RefreshSlots) y escribe la etiqueta flotante (LabelFor).
// Los valores por defecto del viaje y del encuadre son los de la armería; la botica los recibe de su builder.
public abstract class TentShopController : MonoBehaviour
{
    [FormerlySerializedAs("armoryCamera")]
    [SerializeField] protected Camera shopCamera;
    [SerializeField] protected TentShopUI ui;
    [Tooltip("Etiqueta flotante con nombre y precio sobre el objeto señalado")]
    [SerializeField] protected TMPro.TMP_Text hoverLabel;
    [SerializeField] protected AudioSource audioSource;
    [SerializeField] protected AudioClip hoverClip;
    [Tooltip("Post-procesado del interior. Si está vacío se busca entre los hijos")]
    [SerializeField] private Volume interiorVolume;

    [Header("Viaje de cámara (coordenadas de mundo)")]
    [SerializeField] private Vector3[] flightPath =
    {
        new Vector3(-1.5f, 2.2f, -7.8f),   // se aparta de la caseta hacia atrás y a la izquierda
        new Vector3(-8f, 2.7f, -10.2f),     // cruza por delante del puesto morado
        new Vector3(-16.2f, 1.75f, -9.6f),  // frente a la puerta de la carpa
        new Vector3(-19.2f, 1.5f, -5.15f),  // ya entre las cortinas
    };
    [SerializeField] private float flightTime = 3.2f;
    [SerializeField] private Vector3 doorLook = new Vector3(-0.556f, 0f, 0.831f);   // hacia dentro de la puerta

    [Header("Interior")]
    [SerializeField] private Vector3 insidePosition = new Vector3(0f, -57.7f, -0.5f);
    [SerializeField] private Vector3 insideEuler = new Vector3(4f, 0f, 0f);
    [SerializeField] private Vector2 parallax = new Vector2(0.22f, 0.1f);   // metros que se mueve con el ratón
    [SerializeField] private Color insideAmbient = new Color(0.45f, 0.37f, 0.32f);
    [SerializeField] private string volumeLayer = "Armory";

    protected ProfileManager profiles;
    private MenuController menu;
    private Camera mainCamera;
    private ShopSlot hovered;
    private bool inside, busy;
    private int slotMask;

    // RenderSettings guardados al entrar
    private bool savedFog;
    private AmbientMode savedMode;
    private Color savedAmbient, savedSky, savedEquator, savedGround;

    public bool IsOpen => inside || busy;

    protected virtual void Awake()
    {
        profiles = SceneCache<ProfileManager>.Get();
        menu = SceneCache<MenuController>.Get();
        slotMask = 1 << gameObject.layer;   // los colliders de los huecos van en la capa de la raíz (Armory)
        if (shopCamera != null) shopCamera.enabled = false;
        if (hoverLabel != null) hoverLabel.gameObject.SetActive(false);
        if (interiorVolume == null) interiorVolume = GetComponentInChildren<Volume>(true);
        if (interiorVolume != null) interiorVolume.enabled = false;
    }

    protected abstract void RefreshSlots();
    protected abstract string LabelFor(ShopSlot slot);

    // ── Entrar y salir ─────────────────────────────────────────────────────────

    public void Open()
    {
        if (IsOpen || shopCamera == null) return;
        StartCoroutine(EnterRoutine());
    }

    public void Close()
    {
        if (!inside || busy) return;
        StartCoroutine(ExitRoutine());
    }

    private IEnumerator EnterRoutine()
    {
        busy = true;
        mainCamera = Camera.main;
        if (menu != null) menu.HideAll();
        RefreshSlots();

        Transform cam = shopCamera.transform;
        cam.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
        shopCamera.fieldOfView = mainCamera.fieldOfView;
        // Fuera, el post-procesado del nivel: la cámara de la tienda mira lo mismo que la del jugador
        shopCamera.GetUniversalAdditionalCameraData().volumeLayerMask = mainCamera.GetUniversalAdditionalCameraData().volumeLayerMask;
        shopCamera.enabled = true;
        mainCamera.enabled = false;

        Vector3[] pts = new Vector3[flightPath.Length + 1];
        pts[0] = cam.position;
        for (int i = 0; i < flightPath.Length; i++) pts[i + 1] = flightPath[i];
        Quaternion startRot = cam.rotation;
        Quaternion doorRot = Quaternion.LookRotation(doorLook);

        for (float t = 0f; t < flightTime; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / flightTime);
            cam.position = Spline(pts, u);
            // Mira hacia donde va; al principio gira desde la vista del jugador y al final se alinea con la puerta
            Vector3 ahead = Spline(pts, Mathf.Min(1f, u + 0.06f)) - cam.position;
            Quaternion travel = ahead.sqrMagnitude > 1e-5f ? Quaternion.LookRotation(ahead) : cam.rotation;
            Quaternion rot = Quaternion.Slerp(startRot, travel, Mathf.Clamp01(u / 0.18f));
            rot = Quaternion.Slerp(rot, doorRot, Mathf.Clamp01((u - 0.75f) / 0.2f));
            cam.rotation = rot;
            if (ui != null) ui.SetFade(Mathf.Clamp01((t - (flightTime - 0.3f)) / 0.3f));
            yield return null;
        }

        // Corte: a oscuras, al interior
        if (ui != null) ui.SetFade(1f);
        EnterInterior();
        yield return new WaitForSeconds(0.15f);
        for (float t = 0f; t < 0.45f; t += Time.deltaTime)
        {
            if (ui != null) ui.SetFade(1f - t / 0.45f);
            yield return null;
        }
        if (ui != null) ui.SetFade(0f);
        busy = false;
    }

    private void EnterInterior()
    {
        inside = true;
        shopCamera.transform.SetPositionAndRotation(insidePosition, Quaternion.Euler(insideEuler));
        int layer = LayerMask.NameToLayer(volumeLayer);
        if (layer >= 0) shopCamera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << layer;
        if (interiorVolume != null) interiorVolume.enabled = true;

        savedFog = RenderSettings.fog;
        savedMode = RenderSettings.ambientMode;
        savedAmbient = RenderSettings.ambientLight;
        savedSky = RenderSettings.ambientSkyColor;
        savedEquator = RenderSettings.ambientEquatorColor;
        savedGround = RenderSettings.ambientGroundColor;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = insideAmbient;

        if (ui != null) ui.ShowInterior(true);
    }

    private IEnumerator ExitRoutine()
    {
        busy = true;
        SetHovered(null);
        if (ui != null) ui.ShowInterior(false);
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            if (ui != null) ui.SetFade(t / 0.3f);
            yield return null;
        }
        if (ui != null) ui.SetFade(1f);

        RenderSettings.fog = savedFog;
        RenderSettings.ambientMode = savedMode;
        RenderSettings.ambientLight = savedAmbient;
        RenderSettings.ambientSkyColor = savedSky;
        RenderSettings.ambientEquatorColor = savedEquator;
        RenderSettings.ambientGroundColor = savedGround;
        if (interiorVolume != null) interiorVolume.enabled = false;
        shopCamera.enabled = false;
        if (mainCamera != null) mainCamera.enabled = true;
        inside = false;
        if (menu != null) menu.Back();

        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            if (ui != null) ui.SetFade(1f - t / 0.3f);
            yield return null;
        }
        if (ui != null) ui.SetFade(0f);
        busy = false;
    }

    // Catmull-Rom por los puntos del recorrido (pasa por todos), u de 0 a 1 a lo largo de todo el viaje
    private static Vector3 Spline(Vector3[] p, float u)
    {
        int segments = p.Length - 1;
        float f = Mathf.Clamp01(u) * segments;
        int i = Mathf.Min(segments - 1, Mathf.FloorToInt(f));
        float t = f - i;
        Vector3 p0 = p[Mathf.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(p.Length - 1, i + 2)];
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t);
    }

    // ── Dentro ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!inside || busy) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (ui != null && ui.HasSelection) ui.ClearSelection();
            else Close();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 screen = mouse.position.ReadValue();

        // Paralaje: la cámara se desplaza un poco hacia donde está el ratón y sigue mirando al expositor
        Vector2 n = new Vector2(screen.x / Mathf.Max(1, Screen.width) - 0.5f, screen.y / Mathf.Max(1, Screen.height) - 0.5f) * 2f;
        Vector3 target = insidePosition + new Vector3(n.x * parallax.x, n.y * parallax.y, 0f);
        Transform cam = shopCamera.transform;
        cam.position = Vector3.Lerp(cam.position, target, 1f - Mathf.Exp(-Time.deltaTime * 4f));
        cam.rotation = Quaternion.Euler(insideEuler + new Vector3(-n.y * 1.2f, n.x * 2f, 0f));

        // Sobre la interfaz (ficha, botones) no se señala nada
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        ShopSlot slot = null;
        if (!overUI && Physics.Raycast(shopCamera.ScreenPointToRay(screen), out RaycastHit hit, 20f, slotMask, QueryTriggerInteraction.Collide))
            slot = hit.collider.GetComponentInParent<ShopSlot>();
        SetHovered(slot);

        if (slot != null && mouse.leftButton.wasPressedThisFrame && ui != null)
            ui.Select(slot);
    }

    private void SetHovered(ShopSlot slot)
    {
        if (slot == hovered) return;
        if (hovered != null) hovered.SetHovered(false);
        hovered = slot;
        if (slot != null)
        {
            slot.SetHovered(true);
            if (audioSource != null && hoverClip != null) audioSource.PlayOneShot(hoverClip);
        }
        RefreshHoverLabel();
    }

    // También tras comprar: el precio de la etiqueta cambia aunque el ratón no se haya movido
    protected void RefreshHoverLabel()
    {
        if (hoverLabel == null) return;
        hoverLabel.gameObject.SetActive(hovered != null);
        if (hovered == null) return;
        hoverLabel.text = LabelFor(hovered);
        hoverLabel.transform.position = hovered.LabelPoint;
    }
}
