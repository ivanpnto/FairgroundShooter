using UnityEngine;

// Mecánica exclusiva del Bosque: animales asustadizos. Si el jugador tiene la diana en la mira más de dwellTime
// segundos seguidos, se asusta: el búho se esconde tras su arbusto y el conejo huye (TargetMovement.Startle).
// Premia el disparo rápido desde la cadera frente a apuntar con calma, al revés que el resto del juego.
//
// «En la mira» = el rayo del centro de la cámara atraviesa los límites de alguno de sus colliders activos (sin
// mirar si hay algo delante: un animal escondido tras un arbusto también nota que lo apuntan).
// Al reaccionar da un respingo (estira el modelo) para que se entienda por qué se va. Tras asustarse tiene
// calmTime segundos de calma, para que no sea imposible de cazar.
//
// Va en la raíz de la diana, junto a Target y su TargetMovement (prefabs OwlTarget y RabbitTarget).
[RequireComponent(typeof(Target))]
public class ShyTarget : MonoBehaviour
{
    [Tooltip("Segundos seguidos en la mira antes de asustarse")]
    [Min(0.05f)] [SerializeField] private float dwellTime = 0.5f;
    [Tooltip("Segundos tras un susto en los que no se vuelve a asustar")]
    [Min(0f)] [SerializeField] private float calmTime = 2.5f;
    [Tooltip("Respingo: cuánto se estira el modelo en vertical al darse cuenta (0 = nada)")]
    [Min(0f)] [SerializeField] private float jolt = 0.25f;
    [Min(0.01f)] [SerializeField] private float joltTime = 0.18f;

    private Target target;
    private TargetMovement movement;
    private Collider[] colliders;
    private Transform model;
    private Transform view;
    private GameManager gameManager;

    private float aimed;       // segundos seguidos en la mira
    private float calmLeft;
    private float joltLeft;
    private float modelScaleY;

    private void Awake()
    {
        target = GetComponent<Target>();
        movement = GetComponent<TargetMovement>();
        colliders = GetComponents<Collider>();
        model = transform.childCount > 0 ? transform.GetChild(0) : null;
        if (model != null) modelScaleY = model.localScale.y;
    }

    private void Start()
    {
        gameManager = SceneCache<GameManager>.Get();
        PlayerShoot shoot = SceneCache<PlayerShoot>.Get();
        view = shoot != null ? shoot.transform : (Camera.main != null ? Camera.main.transform : null);
    }

    private void Update()
    {
        UpdateJolt();

        if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing) return;
        if (movement == null || view == null || !target.CanBeHit) { aimed = 0f; return; }

        calmLeft = Mathf.Max(0f, calmLeft - Time.deltaTime);
        aimed = IsAimedAt() ? aimed + Time.deltaTime : 0f;

        if (aimed >= dwellTime && calmLeft <= 0f && movement.Startle())
        {
            aimed = 0f;
            calmLeft = calmTime;
            joltLeft = joltTime;
            GameLog.Info($"Bosque: {name} se asusta");
        }
    }

    private bool IsAimedAt()
    {
        Ray ray = new Ray(view.position, view.forward);
        foreach (Collider col in colliders)
            if (col != null && col.enabled && col.bounds.IntersectRay(ray))
                return true;
        return false;
    }

    // Solo la Y (FaceDirection escribe el signo de la X del mismo modelo) y solo mientras dura el respingo: el último
    // paso la deja como estaba y fuera de él no se toca, por si otro script anima la escala
    private void UpdateJolt()
    {
        if (model == null || jolt <= 0f || joltLeft <= 0f) return;
        joltLeft = Mathf.Max(0f, joltLeft - Time.deltaTime);
        float k = Mathf.Sin(Mathf.PI * (1f - joltLeft / joltTime));   // 0 → 1 → 0: al acabar queda en 0
        Vector3 scale = model.localScale;
        scale.y = modelScaleY * (1f + jolt * k);
        model.localScale = scale;
    }
}
