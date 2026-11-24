using System.Collections;
using UnityEngine;

// Esbirro del boss (I7.5, §27.5): un muñeco de resorte que sale, espera y, si no lo derribas, SALTA hacia
// el jugador y le quita 1 corazón. Todo ataque avisa y se puede contrarrestar (§27.6, reglas 1 y 5):
//   1. Espera a la vista minionWaitTime s (solo cuenta el tiempo en Playing).
//   2. Pide permiso al boss: nunca salta durante el bocinazo ni a la vez que otro esbirro.
//   3. Aviso: tiembla y su contorno se pone rojo (el material de aviso del boss) durante minionWarningTime s.
//   4. Salto en arco hasta PlayerDamageZone en minionLungeTime s. Se le puede seguir disparando.
// No se engancha a Target.Expired (como decía el plan): un objetivo que expira ya no se puede disparar,
// y el salto no se podría contrarrestar. Por eso el prefab lleva un lifetime largo y el salto llega antes.
[RequireComponent(typeof(Target))]
public class MinionLunge : MonoBehaviour, IPlayerHazard
{
    [Tooltip("Cuánto crece durante el salto (se acerca a la cámara)")]
    [SerializeField] private float lungeScale = 1.35f;
    [Tooltip("Altura extra del arco del salto (m)")]
    [SerializeField] private float arcHeight = 1.2f;
    [SerializeField] private float shakeAmount = 0.05f;

    public int HeartDamage { get; private set; } = 1;
    // Ni muerto ni caducando: uno que expira en pleno salto ya no se puede disparar, así que tampoco debe dañar
    public bool IsActiveHazard => lunging && target != null && target.CanBeHit;
    public bool IsLunging => lunging;

    private Target target;
    private BossController boss;
    private BossConfig config;
    private GameManager gameManager;
    private bool lunging, claimed, reached;
    private Coroutine routine;
    // Materiales de antes del aviso, mientras dura: si lo derriban temblando, HandleKilled los devuelve
    private Material[][] outlineBeforeWarning;
    private Vector3 warningRest;

    private void Awake()
    {
        target = GetComponent<Target>();
        gameManager = SceneCache<GameManager>.Get();
        target.Killed += HandleKilled;
    }

    // Lo llama BossController al crearlo
    public void Bind(BossController owner, BossConfig bossConfig)
    {
        boss = owner;
        config = bossConfig;
        HeartDamage = config.minionHeartDamage;
        routine = StartCoroutine(Run());
    }

    private bool Playing => gameManager == null || gameManager.CurrentState == GameManager.GameState.Playing;

    private IEnumerator Run()
    {
        // 1. A la vista, esperando
        float wait = Random.Range(config.minionWaitTime.x, config.minionWaitTime.y) * boss.Fury;
        for (float t = 0f; t < wait; ) { if (Playing) t += Time.deltaTime; yield return null; }

        // 2. Permiso: un peligro a la vez
        while (!boss.TryClaimDanger(this)) yield return null;
        claimed = true;

        // 3. Aviso: temblor y contorno rojo
        boss.NotifyMinionWarning(this);
        outlineBeforeWarning = SwapOutline(config.minionWarningMaterial);
        warningRest = transform.position;
        for (float t = 0f; t < config.minionWarningTime; )
        {
            if (Playing) t += Time.deltaTime;
            transform.position = warningRest + Random.insideUnitSphere * shakeAmount;
            yield return null;
        }
        EndWarning();

        GameLog.Info("Esbirro: ¡salta!");
        // 4. Salto. Capa BossHazard: la única que choca con PlayerZone (el disparo sí la ve)
        lunging = true;
        int hazardLayer = LayerMask.NameToLayer("BossHazard");
        if (hazardLayer >= 0) SetLayer(hazardLayer);
        foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
            if (mb is TargetMovement || mb is TargetAppear) mb.enabled = false;   // el salto manda

        PlayerDamageZone zone = SceneCache<PlayerDamageZone>.Get();
        Vector3 from = transform.position;
        Vector3 scale0 = transform.localScale;
        for (float t = 0f; t < config.minionLungeTime; )
        {
            if (Playing) t += Time.deltaTime;
            float k = Mathf.Clamp01(t / config.minionLungeTime);
            // Hacia la cabeza del jugador en cada fotograma (de pie o agachado): agacharse no esquiva el salto, solo
            // el chorro de agua. En profundidad, hasta la zona de daño
            Camera cam = Camera.main;
            Vector3 eye = cam != null ? cam.transform.position : (zone != null ? zone.transform.position : from);
            Vector3 to = zone != null ? new Vector3(zone.transform.position.x, eye.y, zone.transform.position.z) : eye;
            transform.position = Vector3.Lerp(from, to, k) + Vector3.up * (4f * arcHeight * k * (1f - k));
            transform.localScale = scale0 * Mathf.Lerp(1f, lungeScale, k);
            yield return null;
        }
        // Si por lo que sea no ha tocado el trigger (p. ej. está muy lejos), se da por llegado
        // (salvo que haya caducado en pleno salto: ver IsActiveHazard)
        if (!reached && target.CanBeHit)
        {
            PlayerHealth health = SceneCache<PlayerHealth>.Get();
            if (health != null) health.TakeHit(HeartDamage);
            OnReachedPlayer();
        }
    }

    public void OnReachedPlayer()
    {
        if (reached) return;
        reached = true;
        Release();
        Destroy(gameObject);
    }

    // Derribado (también en pleno salto): deja de ser un peligro y cae como cualquier diana
    private void HandleKilled(Target t)
    {
        if (routine != null) StopCoroutine(routine);
        // Derribado durante el aviso: sin esto caía con el contorno rojo de «va a saltar» y descolocado
        EndWarning();
        lunging = false;
        SetLayer(0);
        Release();
    }

    private void OnDestroy()
    {
        if (target != null) target.Killed -= HandleKilled;
        Release();
    }

    // Fin del aviso (por tiempo o porque lo derriban): contorno original y quieto donde estaba
    private void EndWarning()
    {
        if (outlineBeforeWarning == null) return;
        transform.position = warningRest;
        RestoreOutline(outlineBeforeWarning);
        outlineBeforeWarning = null;
    }

    private void Release()
    {
        if (claimed && boss != null) boss.ReleaseDanger(this);
        claimed = false;
    }

    private void SetLayer(int layer)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    // Cambia el contorno (los materiales que empiezan por M_T_Outline) y devuelve los originales
    private Material[][] SwapOutline(Material warning)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        var original = new Material[renderers.Length][];
        for (int r = 0; r < renderers.Length; r++)
        {
            original[r] = renderers[r].sharedMaterials;
            if (warning == null) continue;
            Material[] mats = renderers[r].sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name.StartsWith("M_T_Outline")) mats[i] = warning;
            renderers[r].sharedMaterials = mats;
        }
        return original;
    }

    private void RestoreOutline(Material[][] original)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int r = 0; r < renderers.Length && r < original.Length; r++) renderers[r].sharedMaterials = original[r];
    }
}
