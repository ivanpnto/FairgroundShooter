using UnityEngine;

/// <summary>
/// Presentación visual del arma en primera persona: colocación (hold* y apuntado), retroceso,
/// corredera y recarga con cargador. Es el ÚNICO script que escribe la pose del arma.
/// NO contiene lógica de juego: solo LEE el estado de WeaponAmmo y PlayerScope (patrón observador).
/// Va en el objeto vacío "WeaponHolder", hijo del objeto que rota PlayerAim.
/// Dos niveles: el modelo (hijo) lleva la pose base, Lerp(hold, apuntado); WeaponHolder lleva
/// encima la recarga y el retroceso, que así se suman igual con o sin apuntar.
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
    private Vector3 restScale;
    private Vector3 magazineRest;
    private Vector3 slideRest;
    private Renderer[] magazineRenderers;

    private float recoil; // 1 justo al disparar y decae hacia 0

    // Arma equipada (las pone SetModel). weaponModel puede ser null si el prefab no lo tiene
    private Transform modelRoot;
    private WeaponModel weaponModel;
    private WeaponData weaponData;

    // Player (la cámara) es el padre de WeaponHolder: de ahí sale cuánto se está apuntando
    private PlayerScope playerScope;

    private void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        restScale = transform.localScale;

        playerScope = GetComponentInParent<PlayerScope>();

        CacheParts();
    }

    /// <summary>
    /// Lo llama WeaponController al equipar un arma: cambia las piezas animadas, la pose de
    /// recarga y el retroceso. Los campos serializados se quedan como valores por defecto si
    /// no se llama nunca. Las curvas no se copian: son el ritmo de la animación, común a todas.
    /// </summary>
    public void SetModel(Transform root, WeaponModel model, WeaponData data)
    {
        modelRoot = root;
        weaponModel = model;
        weaponData = data;
        // Ya, y no en el siguiente Update: CacheParts lee las poses de reposo de las piezas
        // del modelo, y el modelo recién instanciado aún no está en su sitio
        ApplyModelPose(0f);

        magazine = model != null ? model.Magazine : null;
        slide = model != null ? model.Slide : null;
        CacheParts();

        reloadOffset = data.reloadOffset;
        reloadRotation = data.reloadRotation;
        magazineOutOffset = data.magazineOutOffset;

        recoilKick = data.recoilKick;
        recoilAngle = data.recoilAngle;
        recoverySpeed = data.recoverySpeed;

        recoil = 0f; // que el arma nueva no herede el retroceso a medias de la anterior
    }

    // Las poses de reposo se leen del modelo tal como viene del prefab, antes de animarlo
    private void CacheParts()
    {
        magazineRenderers = null;

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

    /// <summary>
    /// Cuánto está fuera el cargador (0..1) en un momento de la recarga. Lo usa WeaponEffects
    /// para que el sonido salga de la misma curva que la animación, sin duplicarla. Es un
    /// método y no una propiedad con la curva: una AnimationCurve se podría modificar desde fuera.
    /// </summary>
    public float MagazineOutAmount(float reloadProgress) => magazineCurve.Evaluate(reloadProgress);

    private void Update()
    {
        float zoom = playerScope != null ? playerScope.ZoomAmount : 0f;

        // ---------- 0. Pose base del modelo ----------
        // Cada frame, también sin WeaponAmmo: así los hold* (y el Sight) se pueden calibrar
        // en el Inspector durante el Play y se ve el resultado al momento
        ApplyModelPose(zoom);

        if (weaponAmmo == null)
            return;

        // Decaimiento exponencial: independiente de los FPS (a diferencia de Lerp con deltaTime)
        recoil *= Mathf.Exp(-recoverySpeed * Time.deltaTime);

        // Al apuntar queda solo una parte del retroceso (aimRecoilFactor); se escala el
        // resultado y no el valor de recoil, para que al dejar de apuntar no "salte"
        float aimRecoil = weaponData != null ? Mathf.Lerp(1f, weaponData.aimRecoilFactor, zoom) : 1f;
        float r = recoil * aimRecoil;

        float progress = weaponAmmo.ReloadProgress; // 0..1, sincronizado con reloadTime
        bool reloading = weaponAmmo.IsReloading;

        // ---------- 1. Arma completa ----------
        float w = reloading ? weaponReloadCurve.Evaluate(progress) : 0f;

        transform.localPosition = restPosition
                                + reloadOffset * w
                                + Vector3.back * (recoilKick * r);

        transform.localRotation = restRotation
                                * Quaternion.Euler(reloadRotation * w)
                                * Quaternion.Euler(-recoilAngle * r, 0f, 0f);

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
    /// Coloca el modelo respecto a WeaponHolder: la pose de hold* mezclada con la de apuntado
    /// según zoom (posición con Lerp, rotación con Slerp). Sin Sight no hay pose de apuntado
    /// y el arma se queda en hold*, sin error.
    /// </summary>
    private void ApplyModelPose(float zoom)
    {
        if (modelRoot == null || weaponData == null)
            return;

        Vector3 position = weaponData.holdPosition;
        Quaternion rotation = Quaternion.Euler(weaponData.holdRotation);

        Vector3 aimPosition;
        Quaternion aimRotation;
        if (zoom > 0f && TryGetAimPose(out aimPosition, out aimRotation))
        {
            position = Vector3.Lerp(position, aimPosition, zoom);
            rotation = Quaternion.Slerp(rotation, aimRotation, zoom);
        }

        modelRoot.localPosition = position;
        modelRoot.localRotation = rotation;
        modelRoot.localScale = weaponData.holdScale;
    }

    /// <summary>
    /// Pose del modelo (en el espacio de WeaponHolder) que deja el Sight en (0, 0, eyeRelief)
    /// del espacio de la cámara, con su +Z hacia delante y su +Y hacia arriba.
    /// Se calcula con la pose de REPOSO de WeaponHolder, no con la animada: así el retroceso
    /// y la recarga se suman encima en lugar de compensarse.
    /// Supone escalas uniformes (holdScale y la del WeaponHolder): con una escala no uniforme
    /// la rotación del Sight dejaría de ser una rotación pura.
    /// </summary>
    private bool TryGetAimPose(out Vector3 aimPosition, out Quaternion aimRotation)
    {
        aimPosition = Vector3.zero;
        aimRotation = Quaternion.identity;

        Transform sight = weaponModel != null ? weaponModel.Sight : null;
        if (sight == null)
            return false;

        // 1. Sight en el espacio del modelo (sin la escala de la raíz: InverseTransformPoint ya
        //    la quita). Se lee cada frame para poder mover el Sight en Play y verlo al momento
        Vector3 sightPos = modelRoot.InverseTransformPoint(sight.position);
        Quaternion sightRot = Quaternion.Inverse(modelRoot.rotation) * sight.rotation;

        // 2. Objetivo del Sight pasado del espacio de la cámara al de WeaponHolder (su padre
        //    es la cámara): deshacer su posición, su rotación y su escala de reposo
        Quaternion holderRotInv = Quaternion.Inverse(restRotation);
        Vector3 eyeInHolder = holderRotInv * (new Vector3(0f, 0f, weaponData.eyeRelief) - restPosition);
        eyeInHolder = new Vector3(eyeInHolder.x / restScale.x, eyeInHolder.y / restScale.y, eyeInHolder.z / restScale.z);

        // 3. Rotación: holder · modelo · sight = identidad (en la cámara) → modelo = holder⁻¹ · sight⁻¹
        aimRotation = holderRotInv * Quaternion.Inverse(sightRot);

        // 4. Posición: modelo + rotación · (escala ⊙ sight) = objetivo → se despeja la del modelo
        aimPosition = eyeInHolder - aimRotation * Vector3.Scale(weaponData.holdScale, sightPos);
        return true;
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
