using UnityEngine;

// Telón de la caseta (I4.4). La animación vive en CLIPS y en un ANIMATOR CONTROLLER (máquina de estados
// Closed → Opening → Open → Closing), no en código: este script solo traduce el estado del juego a un
// parámetro del Animator. Es la otra forma de animar en Unity, frente a las animaciones por código
// (movimientos de los objetivos, noria, TargetFall).
//
// Abierto solo mientras se juega. Al cerrarse en la transición entre niveles, además, tapa el cambio
// de decorado (bosque → fondo marino) en lugar de que se vea "saltar".
//
// Consulta el estado cada frame en vez de suscribirse a un evento: GameManager no expone uno y así no
// hay que tocarlo. Animator.SetBool con el mismo valor no hace nada, así que no cuesta.
[RequireComponent(typeof(Animator))]
public class CurtainController : MonoBehaviour
{
    private static readonly int OpenParam = Animator.StringToHash("Open");

    private Animator animator;
    private GameManager gameManager;

    // Anulación para escenas (revelación del boss, §27.1): true/false fuerza abierto o cerrado; null = lo de
    // siempre (abierto solo en Playing)
    public bool? OpenOverride { get; set; }

    // Velocidad de las animaciones del telón (1 = normal). La revelación lo abre "de golpe" con 3
    public float Speed
    {
        get => animator != null ? animator.speed : 1f;
        set { if (animator != null) animator.speed = value; }
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        gameManager = FindFirstObjectByType<GameManager>();
    }

    private void Update()
    {
        if (gameManager == null)
            return;

        animator.SetBool(OpenParam, OpenOverride ?? gameManager.CurrentState == GameManager.GameState.Playing);
    }
}
