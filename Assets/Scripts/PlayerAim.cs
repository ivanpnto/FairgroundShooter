using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAim : MonoBehaviour
{
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private float horizontalLimit = 60f;
    [SerializeField] private float verticalLimit = 45f;

    // 1. Variable para controlar si la cámara puede moverse. Empieza en false.
    public bool canAim = false;

    private float horizontalRotation = 0f;
    private float verticalRotation = 0f;

    private void Update()
    {
        // 2. Si no podemos apuntar, salimos del Update inmediatamente.
        if (!canAim) return;

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

    // 3. Método público que llamarás desde el botón "Play"
    public void EnableAiming()
    {
        canAim = true;
        
        // Opcional pero recomendado para juegos en primera persona: 
        // Ocultar y bloquear el cursor en el centro de la pantalla al empezar a jugar
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}