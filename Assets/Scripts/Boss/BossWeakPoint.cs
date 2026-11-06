using UnityEngine;

// Punto débil del boss (I7.2): ojos, manos, mandíbula, núcleo. Lo único del boss que recibe daño; el
// cuerpo es decorado (capa Scenery) y para la bala.
//
// Abierto: brilla (material "abierto") y tiene el collider activo. Cerrado: collider apagado, así que el
// rayo pasa de largo y da al cuerpo, que cuenta como fallo. Que esté abierto lo decide BossController
// según la fase y el estado (opción B: abierto en Idle y Vulnerable, cerrado en Telegraph y Attack).
//
// No guarda vida propia: pasa el daño al boss, que reparte la vida por fases. Va en el mismo objeto que
// su collider (PlayerShoot busca IDamageable en el collider alcanzado). Capa Default, NO Scenery.
[RequireComponent(typeof(Collider))]
public class BossWeakPoint : MonoBehaviour, IDamageable
{
    [Tooltip("Id con el que BossConfig lo abre en cada fase (p. ej. Eye, Hand, Jaw, Core)")]
    [SerializeField] private string id = "Eye";
    [Tooltip("Renderers que cambian de material al abrirse/cerrarse (vacío = los de este objeto)")]
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Material openMaterial;
    [SerializeField] private Material closedMaterial;

    public string Id => id;
    public bool IsOpen { get; private set; }
    public bool CanBeHit => IsOpen && boss != null && boss.CanTakeDamage;

    // Daño ya multiplicado. Para el tambaleo y el destello (capa additive del Animator, I7.4)
    public event System.Action<BossWeakPoint, float> Damaged;

    private BossController boss;
    private Collider[] colliders;

    private void Awake()
    {
        colliders = GetComponents<Collider>();
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponents<Renderer>();
        SetOpen(false);
    }

    public void Bind(BossController owner) => boss = owner;

    public void SetOpen(bool open)
    {
        IsOpen = open;
        if (colliders != null)
            foreach (Collider col in colliders) col.enabled = open;

        Material material = open ? openMaterial : closedMaterial;
        if (material == null || renderers == null)
            return;
        foreach (Renderer rend in renderers)
            if (rend != null) rend.sharedMaterial = material;
    }

    public void TakeDamage(float damage)
    {
        if (!CanBeHit || damage <= 0f)
            return;

        float applied = boss.ApplyDamage(this, damage);
        if (applied > 0f)
            Damaged?.Invoke(this, applied);
    }
}
