using UnityEngine;

public abstract class TargetMovement : MonoBehaviour
{
    [Tooltip("Modelo que se voltea al cambiar de dirección (opcional: si está vacío se usa el primer hijo)")]
    [SerializeField] private Transform model;

    protected Vector3 startPosition;

    private GameManager gameManager;
    private BoxCollider boxCollider;
    private Vector3 colliderCenter;   // centro original, con el modelo mirando a +X
    private Vector3 modelPosition;    // posición local original del modelo

    // En Awake y no en Start: las clases hijas llaman a FaceDirection desde su Start,
    // y para entonces estos valores ya tienen que estar guardados
    private void Awake()
    {
        if (model == null && transform.childCount > 0)
            model = transform.GetChild(0);

        if (model != null)
            modelPosition = model.localPosition;

        boxCollider = GetComponent<BoxCollider>();

        if (boxCollider != null)
            colliderCenter = boxCollider.center;
    }

    protected virtual void Start()
    {
        startPosition = transform.position;
        gameManager = FindFirstObjectByType<GameManager>();
    }

    private void Update()
    {
        if (gameManager == null)
            return;

        if (gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        Move();
    }

    protected abstract void Move();

    // Los modelos miran hacia +X. Para que no "naden marcha atrás" los reflejamos en X.
    // Se voltea solo el modelo hijo, no la raíz: Unity no admite BoxCollider con escala
    // negativa ("BoxCollider does not support negative scale or size"). A cambio, el centro
    // del collider ya no se refleja solo, así que lo reflejamos a mano para que siga
    // encajando con el modelo (el pez y el pájaro lo tienen desplazado en X).
    protected void FaceDirection(float direction)
    {
        float sign = direction < 0f ? -1f : 1f;

        if (model != null)
        {
            Vector3 scale = model.localScale;
            scale.x = Mathf.Abs(scale.x) * sign;
            model.localScale = scale;

            // Igual que haría el volteo de la raíz si el modelo no estuviera centrado
            model.localPosition = new Vector3(modelPosition.x * sign, modelPosition.y, modelPosition.z);
        }

        if (boxCollider != null)
            boxCollider.center = new Vector3(colliderCenter.x * sign, colliderCenter.y, colliderCenter.z);
    }
}
