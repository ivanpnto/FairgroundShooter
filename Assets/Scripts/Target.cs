using System.Collections.Generic;
using UnityEngine;

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

    [Tooltip("Disparos necesarios para derribarlo (p. ej. la tortuga = 3)")]
    [SerializeField, Min(1)] private int hitsToKill = 1;

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
    public event System.Action<Target, int> Damaged;   // impactos restantes
    public event System.Action<Target> Killed;

    public TargetBonus Bonus { get; private set; } = TargetBonus.None;
    public bool CanHaveBonus => points > 0;   // un objetivo de penalización nunca es especial
    public bool IsDead { get; private set; }

    private ScoreManager scoreManager;
    private TargetSpawner spawner;
    private int spawnEntryIndex = -1;   // Qué tipo de la lista del nivel es este objetivo
    private float pointsMultiplier = 1f;
    private float timeBonusSeconds;
    private int hitsRemaining;

    private void Awake()
    {
        scoreManager = FindFirstObjectByType<ScoreManager>();
        hitsRemaining = hitsToKill;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    public void SetSpawner(TargetSpawner targetSpawner, int entryIndex)
    {
        spawner = targetSpawner;
        spawnEntryIndex = entryIndex;
    }

    // Lo llama el spawner justo después de instanciar (antes de Start)
    public void ApplyLevel(float levelPointsMultiplier, float levelLifetimeMultiplier)
    {
        pointsMultiplier = levelPointsMultiplier;
        lifetime *= levelLifetimeMultiplier;
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

    public void Hit()
    {
        if (IsDead)
            return;

        hitsRemaining--;

        if (hitsRemaining > 0)
        {
            Damaged?.Invoke(this, hitsRemaining);
            return;
        }

        IsDead = true;

        scoreManager.AddScore(Mathf.RoundToInt(points * pointsMultiplier));
        GiveBonusReward();

        Killed?.Invoke(this);

        if (destroyDelay <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // Mientras cae: que no se le pueda volver a dar ni siga con su movimiento
        foreach (Collider col in GetComponents<Collider>())
            col.enabled = false;

        TargetMovement movement = GetComponent<TargetMovement>();
        if (movement != null)
            movement.enabled = false;

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
