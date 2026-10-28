using UnityEngine;

public abstract class TargetMovement : MonoBehaviour
{
    protected Vector3 startPosition;

    private GameManager gameManager;

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

    // Los modelos miran hacia +X. Para que no "naden marcha atrás" volteamos la escala X
    // del objeto raíz; el BoxCollider se voltea con él y sigue encajando con el modelo.
    protected void FaceDirection(float direction)
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (direction < 0f ? -1f : 1f);
        transform.localScale = scale;
    }
}
