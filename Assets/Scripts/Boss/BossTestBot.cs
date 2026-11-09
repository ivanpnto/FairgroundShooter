#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Herramienta de balance (solo editor y builds de desarrollo): un jugador simulado contra el boss.
// Prioridades: 1) la mandíbula durante el aviso del bocinazo, 2) un esbirro saltando, 3) el proyectil más
// cercano al jugador, 4) un esbirro esperando, 5) un punto débil abierto. Tiempo de reacción al cambiar de objetivo; con probabilidad 1 - accuracy
// el disparo sale desviado missOffset metros. Dispara por la misma tubería que el jugador (PlayerShoot.Shoot),
// así que racha, puntos, munición y recarga funcionan igual. Resultados en la consola con el prefijo [Bot].
public class BossTestBot : MonoBehaviour
{
    [Range(0f, 1f)] public float accuracy = 0.7f;
    [Min(0f)] public float reactionTime = 0.25f;
    [Min(0f)] public float missOffset = 0.7f;
    [Min(0.05f)] public float shotsPerSecond = 5f;
    [Tooltip("Partidas seguidas: al acabar una, vuelve a empezar el nivel del boss")]
    [Min(1)] public int runs = 4;
    public int bossLevelIndex = 3;
    [Tooltip("Vacío = juega con el perfil activo. Con nombre: crea/elige ese perfil y lo reinicia (sin mejoras, arma inicial)")]
    public string cleanProfileName = "_bot";
    [Tooltip("Al empezar la toma de aire del bocinazo (cabeza roja), recarga si el cargador no está lleno, como haría un jugador que ha aprendido la señal")]
    public bool tacticalReload = true;

    private GameManager gameManager;
    private LevelManager levelManager;
    private BossController boss;
    private PlayerHealth health;
    private PlayerShoot shoot;
    private PlayerAim aim;
    private WeaponAmmo ammo;
    private Camera cam;
    private MethodInfo shootMethod;

    private Transform currentTarget;
    private float targetAcquiredAt;
    private float nextShotTime;
    private int runsDone, honkOk, honkFail, wins;
    private float runStart;
    private readonly List<string> report = new List<string>();
    private bool inHonk, honkReloadAtStart, jawWasOpen;
    private int honkShots, honkHits, honkReloads;
    private float honkFirstHit, honkOpenAt;

    private void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        levelManager = FindFirstObjectByType<LevelManager>();
        boss = FindFirstObjectByType<BossController>();
        health = FindFirstObjectByType<PlayerHealth>();
        shoot = FindFirstObjectByType<PlayerShoot>();
        aim = FindFirstObjectByType<PlayerAim>();
        cam = Camera.main;
        shootMethod = typeof(PlayerShoot).GetMethod("Shoot", BindingFlags.Instance | BindingFlags.NonPublic);
        var ammoField = typeof(PlayerShoot).GetField("weaponAmmo", BindingFlags.Instance | BindingFlags.NonPublic);
        ammo = ammoField != null ? ammoField.GetValue(shoot) as WeaponAmmo : null;

        if (!string.IsNullOrEmpty(cleanProfileName))
        {
            var profiles = FindFirstObjectByType<ProfileManager>();
            if (profiles != null)
            {
                profiles.CreateProfile(cleanProfileName);   // false si ya existe: da igual
                profiles.SelectProfile(cleanProfileName);
                profiles.ResetCurrentProfile();
                var weapons = FindFirstObjectByType<WeaponController>();
                if (weapons != null && profiles.StarterWeapon != null) weapons.Equip(profiles.StarterWeapon);
            }
        }

        if (aim != null) aim.enabled = false;   // la vista la mueve el bot
        boss.HonkResolved += countered =>
        {
            if (countered) honkOk++; else honkFail++;
            Debug.Log($"[Bot] bocinazo {(countered ? "OK" : "FALLO")} fase {boss.PhaseIndex + 1}: disparos {honkShots}, " +
                      $"impactos {honkHits}, recargando al empezar {honkReloadAtStart}, recarga táctica {honkReloads > 0}, " +
                      $"primer impacto {(honkFirstHit < 0 ? "-" : honkFirstHit.ToString("F2"))} s desde que abre");
        };
        boss.StateChanged += s =>
        {
            bool honk = s == BossController.BossState.Telegraph && boss.CurrentAttack == BossAttack.ChargedHonk;
            if (honk)
            {
                inHonk = true; honkShots = honkHits = honkReloads = 0; honkFirstHit = -1f;
                honkReloadAtStart = ammo != null && ammo.IsReloading;
                if (tacticalReload && ammo != null && !ammo.IsReloading) { ammo.TryReload(); if (ammo.IsReloading) honkReloads++; }
            }
            else inHonk = false;
        };
        foreach (var p in boss.GetComponentsInChildren<BossWeakPoint>(true))
            if (p.Id == "Jaw") p.Damaged += (w, d) =>
            {
                if (!inHonk) return;
                honkHits++;
                if (honkFirstHit < 0f) honkFirstHit = Time.time - honkOpenAt;
            };
        boss.PhaseStarted += phase => { if (phase == 0) runStart = Time.time; };
        gameManager.GameEnded += OnGameEnded;
        Application.runInBackground = true;
        BeginRun();
    }

    private void OnDestroy()
    {
        if (gameManager != null) gameManager.GameEnded -= OnGameEnded;
        if (aim != null) aim.enabled = true;
    }

    private void BeginRun()
    {
        honkOk = honkFail = 0;
        gameManager.RestartGame();
        levelManager.StartLevel(bossLevelIndex);
        runStart = Time.time;
    }

    private void OnGameEnded(GameOverReason reason)
    {
        runsDone++;
        bool won = reason == GameOverReason.Victory;
        if (won) wins++;
        string line = $"[Bot] partida {runsDone}/{runs} acc {accuracy:P0}: {(won ? "VICTORIA" : "DERROTA")} " +
                      $"t={Time.time - runStart:F1}s corazones={health.Hearts} vidaBoss={boss.Health:F1} " +
                      $"fase={boss.PhaseIndex + 1} bocinazos ok={honkOk} fallo={honkFail}";
        report.Add(line);
        Debug.Log(line);
        if (runsDone < runs) Invoke(nameof(BeginRun), 0.5f);
        else Debug.Log($"[Bot] RESUMEN acc {accuracy:P0}: {wins}/{runs} victorias\n" + string.Join("\n", report));
    }

    private void LateUpdate()
    {
        if (gameManager.CurrentState != GameManager.GameState.Playing || boss.State == BossController.BossState.Inactive)
            return;

        if (inHonk)
        {
            bool jawOpen = false;
            foreach (var p in boss.GetComponentsInChildren<BossWeakPoint>()) if (p.Id == "Jaw" && p.CanBeHit) jawOpen = true;
            if (jawOpen && !jawWasOpen) honkOpenAt = Time.time;
            jawWasOpen = jawOpen;
        }
        else jawWasOpen = false;

        Transform target = ChooseTarget();
        if (target != currentTarget) { currentTarget = target; targetAcquiredAt = Time.time; }
        if (target == null || Time.time - targetAcquiredAt < reactionTime) return;

        if (ammo != null && !ammo.CanShoot) return;   // vacío: WeaponAmmo ya recarga solo
        if (Time.time < nextShotTime) return;
        nextShotTime = Time.time + 1f / shotsPerSecond;

        Vector3 point = AimPoint(target);
        if (Random.value > accuracy) point += (Vector3)(Random.insideUnitCircle.normalized * missOffset);
        aim.transform.rotation = Quaternion.LookRotation(point - cam.transform.position);

        if (ammo == null || ammo.UseAmmo())
        {
            if (inHonk && target.GetComponent<BossWeakPoint>() != null) honkShots++;
            shootMethod.Invoke(shoot, null);
        }
    }

    private static Vector3 AimPoint(Transform t)
    {
        var box = t.GetComponent<BoxCollider>();
        return box != null ? t.TransformPoint(box.center) : t.position;
    }

    private Transform ChooseTarget()
    {
        BossWeakPoint[] points = boss.GetComponentsInChildren<BossWeakPoint>();

        // 1) Bocinazo: la mandíbula manda
        if (boss.State == BossController.BossState.Telegraph && boss.CurrentAttack == BossAttack.ChargedHonk)
            foreach (var p in points) if (p.CanBeHit && p.Id == "Jaw") return p.transform;

        // 2) Un esbirro en pleno salto (I7.5): es el peligro más inmediato
        MinionLunge[] minions = FindObjectsByType<MinionLunge>(FindObjectsSortMode.None);
        foreach (var m in minions)
            if (m.IsActiveHazard) return m.transform;

        // 3) El proyectil más cercano al jugador
        Transform best = null; float bestDist = float.MaxValue;
        foreach (var pr in FindObjectsByType<BossProjectile>(FindObjectsSortMode.None))
        {
            if (!pr.IsActiveHazard) continue;
            float d = Vector3.Distance(pr.transform.position, cam.transform.position);
            if (d < bestDist) { bestDist = d; best = pr.transform; }
        }
        if (best != null) return best;

        // 4) Un esbirro esperando (antes de que salte)
        foreach (var m in minions)
        {
            var t = m.GetComponent<Target>();
            if (t != null && t.CanBeHit) return m.transform;
        }

        // 5) Un punto débil abierto (se mantiene el actual si sigue abierto)
        if (currentTarget != null)
        {
            var cur = currentTarget.GetComponent<BossWeakPoint>();
            if (cur != null && cur.CanBeHit) return currentTarget;
        }
        foreach (var p in points) if (p.CanBeHit) return p.transform;
        return null;
    }
}
#endif
