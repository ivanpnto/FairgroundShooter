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

    // Lo que el telón quiere estar ahora (el parámetro del Animator): abierto o cerrado
    public bool IsOpen => animator != null && animator.GetBool(OpenParam);

    // Velocidad de las animaciones del telón (1 = normal). La revelación lo abre "de golpe" con 3
    public float Speed
    {
        get => animator != null ? animator.speed : 1f;
        set { if (animator != null) animator.speed = value; }
    }

    [Header("Fase 8: sonido y bultos")]
    [Tooltip("Suena al abrirse y al cerrarse (tela que se arrastra)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip swooshClip;
    [Tooltip("Golpe sordo de cada bulto: algo choca detrás de la tela")]
    [SerializeField] private AudioClip thumpClip;
    [Tooltip("Cuánto se abomba la tela en el pico de un bulto (escala extra en profundidad)")]
    [SerializeField] private float bumpDepth = 0.9f;

    private Transform[] halves;                         // Curtain_L, Curtain_R
    private readonly float[] bumpTime = { -1f, -1f };   // Time.time del último bulto de cada mitad
    private readonly float[] bumpStrength = new float[2];
    private bool lastOpen, started;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        gameManager = FindFirstObjectByType<GameManager>();
        halves = new[] { transform.Find("Curtain_L"), transform.Find("Curtain_R") };
    }

    private void Update()
    {
        if (gameManager == null)
            return;

        // En pausa sigue abierto: el telón cerrado taparía la partida que se va a retomar
        bool open = OpenOverride ?? (gameManager.CurrentState == GameManager.GameState.Playing || gameManager.IsPaused);
        animator.SetBool(OpenParam, open);

        // Sonido en cada cambio, no en el primer frame (al arrancar el telón ya está cerrado: no se mueve)
        if (started && open != lastOpen && audioSource != null && swooshClip != null)
            audioSource.PlayOneShot(swooshClip);
        lastOpen = open;
        started = true;
    }

    // Fase 8: alguien empuja la tela desde dentro (side 0 = izquierda, 1 = derecha). Lo usa la transición entre
    // niveles mientras «se cambia el decorado» con el telón cerrado
    public void Bump(int side, float strength = 1f)
    {
        side = Mathf.Clamp(side, 0, 1);
        bumpTime[side] = Time.time;
        bumpStrength[side] = strength;
        if (audioSource != null && thumpClip != null)
            audioSource.PlayOneShot(thumpClip, 0.6f + 0.4f * strength);
    }

    // Después del Animator (que escribe la escala de cada mitad en cada frame): el bulto se SUMA encima de la
    // animación sin pelearse con ella. Engrosar la tela en profundidad marca los pliegues, y se lee como un empujón
    private void LateUpdate()
    {
        for (int i = 0; i < halves.Length; i++)
        {
            if (halves[i] == null || bumpTime[i] < 0f) continue;
            float t = Time.time - bumpTime[i];
            if (t > 1.2f) { bumpTime[i] = -1f; continue; }

            // Sube en 0,1 s y vuelve con un vaivén amortiguado, como una tela pesada que se balancea
            float rise = Mathf.Clamp01(t / 0.1f);
            float wobble = Mathf.Cos((t - 0.1f) * 16f) * Mathf.Exp(-Mathf.Max(0f, t - 0.1f) * 4.5f);
            float bulge = bumpStrength[i] * (t < 0.1f ? rise : wobble);

            Vector3 s = halves[i].localScale;
            s.z *= 1f + bumpDepth * bulge;
            halves[i].localScale = s;
        }
    }
}
