using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAim : MonoBehaviour
{
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private float horizontalLimit = 60f;
    [SerializeField] private float verticalLimit = 45f;

    private float horizontalRotation = 0f;
    private float verticalRotation = 0f;

    private void Update()
    {
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
}