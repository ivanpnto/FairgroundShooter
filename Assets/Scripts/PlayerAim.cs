using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAim : MonoBehaviour
{
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private float horizontalLimit = 60f;
    [SerializeField] private float verticalLimit = 45f;

    public bool canAim = false;

    private float horizontalRotation = 0f;
    private float verticalRotation = 0f;

    private GameManager gameManager;
    private GameManager.GameState lastState;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager != null)
        {
            lastState = gameManager.CurrentState;
            UpdateCursor();
        }
    }

    private void Update()
    {
        if (gameManager == null)
            return;

        // Comprobar si ha cambiado el estado del juego
        if (gameManager.CurrentState != lastState)
        {
            lastState = gameManager.CurrentState;
            UpdateCursor();
        }

        // No permitir apuntar fuera de la partida
        if (gameManager.CurrentState != GameManager.GameState.Playing)
        {
            canAim = false;
            return;
        }

        canAim = true;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float horizontal = mouseDelta.x * sensitivity;
        float vertical = mouseDelta.y * sensitivity;

        horizontalRotation += horizontal;
        verticalRotation -= vertical;

        horizontalRotation = Mathf.Clamp(
            horizontalRotation,
            -horizontalLimit,
            horizontalLimit
        );

        verticalRotation = Mathf.Clamp(
            verticalRotation,
            -verticalLimit,
            verticalLimit
        );

        transform.localRotation = Quaternion.Euler(
            verticalRotation,
            horizontalRotation,
            0f
        );
    }

    private void UpdateCursor()
    {
        if (gameManager.CurrentState == GameManager.GameState.Playing)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}