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
}