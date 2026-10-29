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

    // Retroceso de la vista: un desfase que se SUMA a la puntería del jugador y vuelve
    // solo a cero. Se guarda aparte (en vez de tocar horizontal/verticalRotation) para
    // que la vista regrese a donde apuntaba el jugador cuando el arma deja de disparar
    private float recoilPitch;           // grados hacia arriba
    private float recoilYaw;             // grados a la derecha (+) o izquierda (-)
    private float recoilRecoverySpeed;
    private float recoilRecoverAt;       // Time.time a partir del cual la vista empieza a volver

    private GameManager gameManager;
    private GameManager.GameState lastState;
    private PlayerScope playerScope;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        playerScope = GetComponent<PlayerScope>(); // opcional: sin él, sensibilidad normal

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

        // Con la mira puesta se reduce la sensibilidad en proporción al zoom
        float currentSensitivity = sensitivity * (playerScope != null ? playerScope.SensitivityScale : 1f);

        float horizontal = mouseDelta.x * currentSensitivity;
        float vertical = mouseDelta.y * currentSensitivity;

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

        // Solo se recupera cuando se deja de disparar: si recuperase también durante la ráfaga,
        // la subida se estancaría en un equilibrio de pocos grados por muchas balas que se tiren.
        // Decaimiento exponencial: independiente de los FPS, igual que el retroceso de WeaponVisual
        if (Time.time >= recoilRecoverAt)
        {
            float decay = Mathf.Exp(-recoilRecoverySpeed * Time.deltaTime);
            recoilPitch *= decay;
            recoilYaw *= decay;
        }

        // Los límites se aplican a la suma: el retroceso no puede sacar la vista de rango.
        // En el eje X de Unity, un ángulo negativo mira hacia arriba
        transform.localRotation = Quaternion.Euler(
            Mathf.Clamp(verticalRotation - recoilPitch, -verticalLimit, verticalLimit),
            Mathf.Clamp(horizontalRotation + recoilYaw, -horizontalLimit, horizontalLimit),
            0f
        );
    }

    /// <summary>
    /// Lo llama PlayerShoot tras cada disparo. Solo acumula: la rotación se aplica en Update,
    /// así que el raycast del disparo actual nunca sale desviado (el primero va al centro).
    /// recoveryDelay: segundos sin disparar antes de que la vista empiece a volver.
    /// </summary>
    public void AddRecoil(float up, float side, float recoverySpeed, float recoveryDelay)
    {
        recoilPitch += up;
        recoilYaw += Random.Range(-side, side);
        recoilRecoverySpeed = recoverySpeed;
        recoilRecoverAt = Time.time + recoveryDelay;
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