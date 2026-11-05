using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum TargetBonus
{
    None,
    Time,   // contorno dorado: suma tiempo al nivel
    Ammo    // contorno rojo: rellena el cargador
}

public class Target : MonoBehaviour
{
    [SerializeField] private int points = 10;
    [SerializeField] private float lifetime = 5f;

    // Antes hitsToKill (disparos para derribarlo). La vida se mide en impactos de daño 1, así que los
    // prefabs se migran solos con el mismo número (la tortuga conserva 3)
    [Tooltip("Vida base, en impactos de daño 1 (p. ej. la tortuga = 3). El nivel la multiplica")]
    [FormerlySerializedAs("hitsToKill")]
    [SerializeField, Min(1)] private int health = 1;

    [Tooltip("Segundos entre el impacto final y la destrucción, para animaciones de caída (0 = inmediato)")]
    [SerializeField, Min(0f)] private float destroyDelay = 0f;

    [Tooltip("Los materiales cuyo nombre empiece así se sustituyen en los objetivos especiales")]
    [SerializeField] private string outlineMaterialName = "M_T_Outline";

    [Tooltip("Si se activa, el contorno oscuro del recorte también toma el color del bonus. Desactivado por defecto: " +
             "el contorno oscuro separa el halo del cuerpo (sin él, el halo dorado se funde con el pato amarillo)")]
    [SerializeField] private bool tintOutline = false;

    [Header("Halo de los objetivos especiales")]
    [Tooltip("Tamaño de la silueta luminosa respecto al modelo")]
    [SerializeField, Min(1f)] private float haloScale = 1.1f;
    [Tooltip("Distancia detrás del modelo a la que se coloca el halo (m)")]
    [SerializeField, Min(0f)] private float haloDepth = 0.07f;

    // Puntos de extensión para efectos (destello, caída, sonido) sin tocar esta lógica
    // Impactos restantes ESTIMADOS con el daño del disparo que acaba de recibir (Ceil(vida / daño)).
    // Se mantiene la firma de antes de la vida numérica para no cambiar a sus suscriptores (TargetFall)
    public event System.Action<Target, int> Damaged;
    public event System.Action<Target> Killed;
    public event System.Action<Target> Expired;          // se acabó su tiempo sin que lo derribaran

    // Segundos que tarda en irse cuando expira (la animación de bajada). Lo fija TargetAppear en su Awake;
    // sin ese componente vale 0 y el objetivo desaparece de golpe, como antes
    public float ExpireDuration { get; set; }
    public bool IsExpiring { get; private set; }

    public TargetBonus Bonus { get; private set; } = TargetBonus.None;
    public bool CanHaveBonus => points > 0;   // un objetivo de penalización nunca es especial
    public bool IsPenalty => points < 0;      // resta puntos y rompe la racha
    public bool IsDead { get; private set; }
    public float MaxHealth { get; private set; }   // vida con el multiplicador del nivel ya aplicado (puede ser decimal)
    public float Health => currentHealth;

    private ScoreManager scoreManager;
    private TargetSpawner spawner;
    private int spawnEntryIndex = -1;   // Qué tipo de la lista del nivel es este objetivo
    private float pointsMultiplier = 1f;
    private float timeBonusSeconds;
    private float currentHealth;

    // Por debajo de esto la vida cuenta como 0: el daño de las mejoras (1.25, 1.75…) suma en float
    private const float HealthEpsilon = 0.001f;

    private void Awake()
    {
        scoreManager = FindFirstObjectByType<ScoreManager>();

        // Sin ApplyLevel (p. ej. un objetivo colocado a mano en la escena) vale la vida del prefab
        MaxHealth = health;
        currentHealth = health;
    }

    // Antes era Destroy(gameObject, lifetime): desaparecía de golpe. Ahora, al acabarse el tiempo,
    // se avisa (Expired) para que la animación lo baje tras el decorado y se destruye al terminar.
    // Invoke en lugar de Destroy diferido: si lo derriban antes, Expire() comprueba IsDead y no hace nada
    private void Start()
    {
        Invoke(nameof(Expire), lifetime);
    }

    private void Expire()
    {
        if (IsDead)
            return;

        IsExpiring = true;
        StopInteraction();   // mientras baja no se le puede acertar: ya "se ha ido"

        Expired?.Invoke(this);
        Destroy(gameObject, ExpireDuration);
    }

    // Colliders fuera (no bloquea ni recibe disparos) y movimiento parado
    private void StopInteraction()
    {
        foreach (Collider col in GetComponents<Collider>())
            col.enabled = false;

        TargetMovement movement = GetComponent<TargetMovement>();
        if (movement != null)
            movement.enabled = false;
    }

    public void SetSpawner(TargetSpawner targetSpawner, int entryIndex)
    {
        spawner = targetSpawner;
        spawnEntryIndex = entryIndex;
    }

    // Lo llama el spawner justo después de instanciar (antes de Start).
    // La vida NO se redondea: con ×1.5 un objetivo de vida 1 tiene 1.5 y la tortuga (3) 4.5.
    // Antes se redondeaba hacia arriba (1 → 2) y la mejora de daño no servía con el arma inicial:
    // 1 → 1.75 de daño seguía necesitando 2 disparos contra vida 2. Con 1.5, el nivel 2 de la mejora
    // (daño 1.5) ya los derriba de un disparo, y cada nivel se nota con la tortuga (5 → 4 → 3 disparos).
    // Los de penalización no ganan vida: dañarlo sin derribarlo no restaría nada, y la penalización
    // debe llegar en el primer impacto, como hasta ahora
    public void ApplyLevel(float levelPointsMultiplier, float levelLifetimeMultiplier, float levelHealthMultiplier = 1f)
    {
        pointsMultiplier = levelPointsMultiplier;
        lifetime *= levelLifetimeMultiplier;

        float healthMultiplier = CanHaveBonus ? levelHealthMultiplier : 1f;
        MaxHealth = Mathf.Max(1f, health * healthMultiplier);
        currentHealth = MaxHealth;
    }

    public void ApplyBonus(TargetBonus bonus, Material outline, float seconds)
    {
        Bonus = bonus;
        timeBonusSeconds = seconds;

        if (outline == null)
            return;

        if (tintOutline)
            TintOutline(outline);

        CreateHalo(outline);
    }

    private void TintOutline(Material outline)
    {
        // Cambiamos el material del contorno en todos los renderers del modelo (incluida el ala del pájaro).
        // sharedMaterials en una instancia recién creada no afecta al prefab: solo a este objeto.
        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            Material[] materials = rend.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].name.StartsWith(outlineMaterialName))
                {
                    materials[i] = outline;
                    changed = true;
                }
            }

            if (changed)
                rend.sharedMaterials = materials;
        }
    }

    // El contorno del recorte mide 3 cm: a 8-10 m son pocos píxeles y no basta para llamar la atención.
    // Por eso, además, se coloca detrás del modelo una copia de su silueta, más grande, con el material
    // del bonus (sin iluminación y con brillo HDR, así que el Bloom la convierte en un halo), y se hace latir.
    private void CreateHalo(Material material)
    {
        List<Renderer> haloRenderers = new List<Renderer>();

        // Se recorre una copia (array) tomada antes de crear los halos, para no procesarlos a ellos mismos
        foreach (MeshFilter source in GetComponentsInChildren<MeshFilter>())
        {
            MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();

            // Excluye el MeshRenderer vacío y desactivado que quedó en la raíz de los prefabs
            if (sourceRenderer == null || !sourceRenderer.enabled || source.sharedMesh == null)
                continue;

            GameObject halo = new GameObject("BonusHalo");
            halo.transform.SetParent(source.transform, false);   // hereda el volteo de FaceDirection

            // Agrandar en el plano de la pantalla (X e Y del mundo) pero no en profundidad.
            // Se pasa esa escala al espacio local por si el modelo importado trae una rotación de ejes.
            Vector3 local = Quaternion.Inverse(source.transform.rotation) * new Vector3(haloScale, haloScale, 1f);
            halo.transform.localScale = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));

            // Detrás del modelo: los objetivos siempre miran a la cámara (-Z), así que "detrás" es +Z
            halo.transform.position = source.transform.position + Vector3.forward * haloDepth;

            halo.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;

            MeshRenderer haloRenderer = halo.AddComponent<MeshRenderer>();
            Material[] materials = new Material[source.sharedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;

            haloRenderer.sharedMaterials = materials;
            haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            haloRenderer.receiveShadows = false;

            haloRenderers.Add(haloRenderer);
        }

        if (haloRenderers.Count > 0)
            gameObject.AddComponent<BonusGlow>().Init(haloRenderers.ToArray(), material.GetColor("_BaseColor"));
    }

    // Se conserva para no romper a quien lo llame: un impacto de daño 1, como antes
    public void Hit() => TakeDamage(1f);

    public void TakeDamage(float damage)
    {
        if (IsDead || IsExpiring || damage <= 0f)
            return;

        currentHealth -= damage;

        if (currentHealth > HealthEpsilon)
        {
            Damaged?.Invoke(this, Mathf.CeilToInt(currentHealth / damage));
            return;
        }

        IsDead = true;
        CancelInvoke(nameof(Expire));

        scoreManager.AddScore(Mathf.RoundToInt(points * pointsMultiplier));
        GiveBonusReward();

        Killed?.Invoke(this);

        if (destroyDelay <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // Mientras cae: que no se le pueda volver a dar ni siga con su movimiento
        StopInteraction();

        Destroy(gameObject, destroyDelay);
    }

    private void GiveBonusReward()
    {
        switch (Bonus)
        {
            case TargetBonus.Time:
                GameTimer timer = FindFirstObjectByType<GameTimer>();
                if (timer != null) timer.AddTime(timeBonusSeconds);
                break;

            case TargetBonus.Ammo:
                WeaponAmmo ammo = FindFirstObjectByType<WeaponAmmo>();
                if (ammo != null) ammo.ResetAmmo();   // cargador lleno y cancela la recarga en curso
                break;
        }
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.TargetDestroyed(spawnEntryIndex);
        }
    }
}
