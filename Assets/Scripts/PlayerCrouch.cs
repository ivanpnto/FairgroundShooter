using UnityEngine;
using UnityEngine.InputSystem;

// Agacharse tras el mostrador de la caseta (contra el rayo del boss, BossAttack.LaserSweep): mientras se mantiene
// C o Ctrl izquierdo, la vista baja crouchDepth metros. Va en Player (la cámara).
//
// Mueve la POSICIÓN local de la cámara: PlayerAim y CameraShake solo escriben la rotación, así que no se pisan.
// El arma es hija de la cámara y baja con ella. Agachado se puede seguir disparando, pero el mostrador tapa casi
// toda la vista: estar a salvo cuesta no poder castigar al boss.
//
// Fuera de Playing se levanta al instante (como PlayerScope con el zoom): no necesita Reset*().
public class PlayerCrouch : MonoBehaviour
{
    [Tooltip("Metros que baja la vista. Con la cámara a 1,60 y el mostrador a 1,08, 0,5 deja los ojos justo por encima")]
    [Min(0f)] [SerializeField] private float crouchDepth = 0.5f;
    [Tooltip("Segundos en agacharse o levantarse del todo")]
    [Min(0.01f)] [SerializeField] private float crouchTime = 0.15f;
    [Tooltip("Depuración y BalanceBot: agachado sin pulsar nada")]
    public bool forceCrouch;

    // 0 = de pie, 1 = agachado del todo
    public float CrouchAmount { get; private set; }
    public bool IsCrouching => CrouchAmount > 0.5f;
    // Altura (mundo) de la vista: es lo que mira el rayo
    public float EyeHeight => transform.position.y;

    private GameManager gameManager;
    private Vector3 standPosition;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        standPosition = transform.localPosition;
    }

    private void Update()
    {
        bool playing = gameManager == null || gameManager.CurrentState == GameManager.GameState.Playing;
        if (!playing)
        {
            // En pausa se queda como estaba (al reanudar no da un salto); en los demás estados, de pie
            if (gameManager == null || !gameManager.IsPaused) SetAmount(0f);
            return;
        }

        Keyboard keyboard = Keyboard.current;
        bool wants = forceCrouch || (keyboard != null && (keyboard.cKey.isPressed || keyboard.leftCtrlKey.isPressed));
        SetAmount(Mathf.MoveTowards(CrouchAmount, wants ? 1f : 0f, Time.deltaTime / crouchTime));
    }

    private void SetAmount(float amount)
    {
        CrouchAmount = amount;
        // SmoothStep: arranca y frena suave, sin el tirón de un movimiento lineal
        transform.localPosition = standPosition + Vector3.down * (crouchDepth * Mathf.SmoothStep(0f, 1f, amount));
    }

    private void OnDisable() => SetAmount(0f);

    // ¿Se salva del rayo? La vista tiene que quedar al menos margin por debajo de él (función pura, con tests)
    public static bool IsBelowBeam(float eyeHeight, float beamHeight, float margin)
    {
        return eyeHeight <= beamHeight - margin;
    }
}
