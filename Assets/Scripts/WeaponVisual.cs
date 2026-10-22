using UnityEngine;

/// <summary>
/// Presentación visual del arma en primera persona: retroceso, corredera y recarga con cargador.
/// NO contiene lógica de juego: solo LEE el estado de WeaponAmmo (patrón observador).
/// Va en el objeto vacío "WeaponHolder", hijo del objeto que rota PlayerAim.
/// </summary>
public class WeaponVisual : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private WeaponAmmo weaponAmmo;
    [Tooltip("Pieza del cargador (opcional)")]
    [SerializeField] private Transform magazine;
    [Tooltip("Corredera (opcional)")]
    [SerializeField] private Transform slide;

    [Header("Recarga: arma completa")]
    [Tooltip("Desplazamiento del arma durante la recarga (metros, ejes del WeaponHolder)")]
    [SerializeField] private Vector3 reloadOffset = new Vector3(0f, -0.12f, 0f);
    [Tooltip("Inclinación del arma durante la recarga (grados)")]
    [SerializeField] private Vector3 reloadRotation = new Vector3(-15f, 0f, 30f);
    // Eje X = progreso de la recarga (0..1). Eje Y = cuánto se aplica la pose de recarga (0..1)
    [SerializeField] private AnimationCurve weaponReloadCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.2f, 1f),
        new Keyframe(0.85f, 1f), new Keyframe(1f, 0f));

    [Header("Recarga: cargador")]
    [Tooltip("Hacia dónde sale el cargador (metros, ejes del WeaponHolder: Y negativo = abajo)")]
    [SerializeField] private Vector3 magazineOutOffset = new Vector3(0f, -0.25f, 0f);
    // 0 = cargador dentro, 1 = fuera del todo
    [SerializeField] private AnimationCurve magazineCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.2f, 0f), new Keyframe(0.35f, 1f),
        new Keyframe(0.6f, 1f), new Keyframe(0.75f, 0f), new Keyframe(1f, 0f));
    [Tooltip("Ocultar el cargador cuando está fuera (simula cambiarlo por uno nuevo)")]
    [SerializeField] private bool hideMagazineWhenOut = true;

    [Header("Corredera")]
    [Tooltip("Recorrido hacia atrás de la corredera (metros)")]
    [SerializeField] private float slideTravel = 0.04f;
    [Tooltip("Momento de la recarga (0..1) en el que se suelta la corredera")]
    [Range(0f, 1f)] [SerializeField] private float slideReleaseAt = 0.85f;

    [Header("Retroceso")]
    [SerializeField] private float recoilKick = 0.06f;   // metros hacia atrás
    [SerializeField] private float recoilAngle = 10f;    // grados hacia arriba
    [SerializeField] private float recoverySpeed = 12f;  // mayor = vuelve antes

    // Poses de reposo (las que dejes colocadas en el editor)
    private Vector3 restPosition;
    private Quaternion restRotation;
    private Vector3 magazineRest;
    private Vector3 slideRest;
    private Renderer[] magazineRenderers;

    private float recoil; // 1 justo al disparar y decae hacia 0

    private void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;

        if (magazine != null)
        {
            magazineRest = magazine.localPosition;
            magazineRenderers = magazine.GetComponentsInChildren<Renderer>(true);
        }

        if (slide != null)
            slideRest = slide.localPosition;
    }

    // Suscribirse en OnEnable y desuscribirse en OnDisable evita llamadas
    // a un objeto desactivado o destruido
    private void OnEnable()
    {
        if (weaponAmmo != null) weaponAmmo.OnShot += HandleShot;
    }

    private void OnDisable()
    {
        if (weaponAmmo != null) weaponAmmo.OnShot -= HandleShot;
    }

    private void HandleShot() => recoil = 1f;

    private void Update()
    {
        if (weaponAmmo == null)
            return;

        // Decaimiento exponencial: independiente de los FPS (a diferencia de Lerp con deltaTime)
        recoil *= Mathf.Exp(-recoverySpeed * Time.deltaTime);

        float progress = weaponAmmo.ReloadProgress; // 0..1, sincronizado con reloadTime
        bool reloading = weaponAmmo.IsReloading;

        // ---------- 1. Arma completa ----------
        float w = reloading ? weaponReloadCurve.Evaluate(progress) : 0f;

        transform.localPosition = restPosition
                                + reloadOffset * w
                                + Vector3.back * (recoilKick * recoil);

        transform.localRotation = restRotation
                                * Quaternion.Euler(reloadRotation * w)
                                * Quaternion.Euler(-recoilAngle * recoil, 0f, 0f);

        // ---------- 2. Cargador ----------
        if (magazine != null)
        {
            float m = reloading ? magazineCurve.Evaluate(progress) : 0f;

            magazine.localPosition = magazineRest + HolderToPartSpace(magazine, magazineOutOffset * m);

            if (hideMagazineWhenOut)
                SetMagazineVisible(m < 0.95f);
        }

        // ---------- 3. Corredera ----------
        if (slide != null)
        {
            // Atrás al disparar (retroceso) y bloqueada atrás durante la recarga
            // hasta slideReleaseAt, como en una pistola real al vaciar el cargador
            float s = recoil;
            if (reloading && progress < slideReleaseAt)
                s = 1f;

            slide.localPosition = slideRest + HolderToPartSpace(slide, Vector3.back * (slideTravel * s));
        }
    }

    /// <summary>
    /// Convierte un desplazamiento expresado en ejes del WeaponHolder (Y = arriba, Z = delante,
    /// metros reales) al espacio local del padre de la pieza.
    /// Por qué: los modelos de internet suelen tener escalas y rotaciones raras en sus
    /// sub-objetos; así tú siempre piensas en "abajo" y "atrás" desde el punto de vista del jugador.
    /// </summary>
    private Vector3 HolderToPartSpace(Transform part, Vector3 holderOffset)
    {
        Vector3 world = transform.TransformVector(holderOffset);
        return part.parent.InverseTransformVector(world);
    }

    private void SetMagazineVisible(bool visible)
    {
        foreach (Renderer r in magazineRenderers)
            r.enabled = visible;
    }
}
