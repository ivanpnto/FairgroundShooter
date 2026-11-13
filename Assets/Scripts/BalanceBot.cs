#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

// Jugador simulado para el BALANCE (bloque 3). Generaliza a BossTestBot: juega la partida entera (niveles 1–3 y,
// si el perfil lo tiene comprado, el nivel 4 con el boss) con un equipo concreto, y escribe en la consola, con el
// prefijo [Balance], los puntos de cada nivel, los créditos ganados, la precisión real y los datos del combate.
//
// Modelo de jugador (lo importante es que sea el MISMO en todas las mediciones, no que sea perfecto):
//  - Elige la diana con más puntos ponderados por lo cerca que está de la mira (las lejanas cuestan de alcanzar)
//    y se queda con la actual mientras siga viva (una persona no cambia de objetivo a cada fotograma).
//  - Al cambiar de objetivo tarda reactionTime + aimSecondsPerDegree × ángulo en llevar la mira.
//  - Con probabilidad 1 − accuracy, el disparo sale desviado missOffset metros (puede dar a otra diana o a una
//    prohibida, como a una persona).
//  - Cadencia del arma, con un tope de clics por segundo en las semiautomáticas.
//  - Contra el boss, las prioridades de BossTestBot (mandíbula en el bocinazo, esbirro saltando, proyectil más
//    cercano, esbirro esperando, punto débil abierto) y recarga táctica al ver la toma de aire.
//
// Uso: en Play en BossSandbox (usa profiles_sandbox.json), añadir el componente a un objeto. Cambia el perfil a
// «_balance» con el equipo elegido. Solo editor y builds de desarrollo.
public class BalanceBot : MonoBehaviour
{
    public enum Loadout { Starter, Mid, Full, Demo }

    [Header("Jugador")]
    [Range(0f, 1f)] public float accuracy = 0.55f;
    [Min(0f)] public float reactionTime = 0.25f;
    [Min(0f)] public float aimSecondsPerDegree = 0.006f;
    [Min(0f)] public float missOffset = 0.7f;
    [Tooltip("Clics por segundo como máximo con armas semiautomáticas")]
    [Min(0.5f)] public float maxClickRate = 5f;
    public bool tacticalReload = true;

    [Header("Partidas")]
    public Loadout loadout = Loadout.Starter;
    [Tooltip("Arma equipada (id). Vacío = la que corresponde al equipo")]
    public string weaponId = "";
    [Tooltip("Comprar el nivel 4 en el perfil (si no, la partida acaba en el nivel 3)")]
    public bool includeBoss = false;
    [Tooltip("Empezar directamente en el nivel 4 (para medir solo el boss)")]
    public bool startAtBoss = false;
    [Min(1)] public int runs = 3;
    [Tooltip("Velocidad del juego durante la medición (1 = tiempo real)")]
    [Range(1f, 4f)] public float timeScale = 3f;

    private GameManager gameManager;
    private LevelManager levelManager;
    private ScoreManager score;
    private BossController boss;
    private PlayerHealth health;
    private PlayerShoot shoot;
    private PlayerAim aim;
    private WeaponAmmo ammo;
    private WeaponController weapons;
    private Camera cam;
    private MethodInfo shootMethod;

    private Transform current;
    private float readyAt, nextShotTime;
    private int runsDone;
    private readonly List<string> report = new List<string>();
    private readonly List<int> levelScores = new List<int>();
    private int scoreAtLevelStart;
    private float fightStart = -1f;
    private int honkOk, honkFail, minionWarnings, minionHits, projectileHits;
    private bool inHonk;
    private float realStart;

    private void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        levelManager = FindFirstObjectByType<LevelManager>();
        score = FindFirstObjectByType<ScoreManager>();
        boss = FindFirstObjectByType<BossController>();
        health = FindFirstObjectByType<PlayerHealth>();
        shoot = FindFirstObjectByType<PlayerShoot>();
        aim = FindFirstObjectByType<PlayerAim>();
        weapons = FindFirstObjectByType<WeaponController>();
        cam = Camera.main;
        shootMethod = typeof(PlayerShoot).GetMethod("Shoot", BindingFlags.Instance | BindingFlags.NonPublic);
        ammo = typeof(PlayerShoot).GetField("weaponAmmo", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(shoot) as WeaponAmmo;

        PrepareProfile();
        if (aim != null) aim.enabled = false;

        levelManager.LevelStarted += HandleLevelStarted;
        gameManager.GameEnded += HandleGameEnded;
        if (boss != null)
        {
            boss.PhaseStarted += p => { if (p == 0) fightStart = Time.time; };
            boss.HonkResolved += c => { if (c) honkOk++; else honkFail++; };
            boss.MinionWarning += _ => minionWarnings++;
            boss.StateChanged += s =>
            {
                inHonk = s == BossController.BossState.Telegraph && boss.CurrentAttack == BossAttack.ChargedHonk;
                // Lo que hace un jugador que ha aprendido la señal («cabeza roja → recarga ya»): si el cargador no está
                // lleno y la recarga acaba antes de que se abra la boca (la toma de aire dura 1,2 s), recarga. Con la
                // Five-seven (1,5 s) sí; con la AK (2,5 s) o la M16 (2,2 s) no, porque perdería la ventana
                if (inHonk && tacticalReload && ammo != null && !ammo.IsReloading
                    && ammo.CurrentAmmo < weapons.CurrentStats.MagazineSize && weapons.CurrentStats.ReloadTime <= 1.6f)
                    ammo.TryReload();
            };
        }
        PlayerDamageZone zone = FindFirstObjectByType<PlayerDamageZone>();
        if (zone != null) zone.HazardReached += (h, damaged) =>
        {
            if (!damaged) return;
            if (h is MinionLunge) minionHits++; else projectileHits++;
        };

        Application.runInBackground = true;
        Time.timeScale = timeScale;
        realStart = Time.realtimeSinceStartup;
        BeginRun();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        if (aim != null) aim.enabled = true;
        if (levelManager != null) levelManager.LevelStarted -= HandleLevelStarted;
        if (gameManager != null) gameManager.GameEnded -= HandleGameEnded;
    }

    // ---------------------------------------------------------------- perfil y equipo

    private void PrepareProfile()
    {
        ProfileManager profiles = FindFirstObjectByType<ProfileManager>();
        if (profiles == null) return;

        ProfilePreset preset = loadout == Loadout.Demo ? ProfilePreset.DefaultDemo() : new ProfilePreset();
        preset.name = "_balance";
        preset.credits = 0;
        string defaultWeapon = "fiveseven";
        if (loadout == Loadout.Mid)
        {
            preset.upgrades = ProfilePreset.DefaultDemo().upgrades;
            preset.weapons = new List<string> { "mp5", "m16a1" };
            defaultWeapon = "m16a1";
        }
        else if (loadout == Loadout.Full)
        {
            foreach (UpgradeData u in profiles.Upgrades) if (u != null) preset.upgrades.Add(new UpgradeLevel(u.id, u.maxLevel));
            preset.weapons = new List<string> { "mp5", "m16a1", "ak47", "barrett" };
            defaultWeapon = "ak47";
        }
        else if (loadout == Loadout.Demo) defaultWeapon = "m16a1";

        preset.upgrades.RemoveAll(u => u.id == "boss_level");
        if (includeBoss || startAtBoss) preset.upgrades.Add(new UpgradeLevel("boss_level", 1));
        profiles.ApplyPreset(preset);

        string id = string.IsNullOrEmpty(weaponId) ? defaultWeapon : weaponId;
        foreach (WeaponData w in weapons.AvailableWeapons)
            if (w != null && w.id == id) weapons.Equip(w);
    }

    // ---------------------------------------------------------------- partidas

    private void BeginRun()
    {
        levelScores.Clear();
        honkOk = honkFail = minionWarnings = minionHits = projectileHits = 0;
        fightStart = -1f;
        gameManager.RestartGame();
        if (startAtBoss) levelManager.StartLevel(levelManager.LevelCount - 1);
        scoreAtLevelStart = score.GetScore();
    }

    private void HandleLevelStarted(LevelConfig level, int index)
    {
        if (index > 0 && !startAtBoss) levelScores.Add(score.GetScore() - scoreAtLevelStart);
        scoreAtLevelStart = score.GetScore();
    }

    private void HandleGameEnded(GameOverReason reason)
    {
        runsDone++;
        levelScores.Add(score.GetScore() - scoreAtLevelStart);
        var sb = new StringBuilder();
        sb.Append($"[Balance] {loadout}/{weapons.CurrentWeapon.id} acc {accuracy:P0} partida {runsDone}/{runs}: {reason} ");
        sb.Append($"puntos {score.GetScore()} créditos +{gameManager.LastCreditsEarned} precisión real {score.RunAccuracy:P0} ");
        sb.Append($"por nivel [{string.Join(", ", levelScores)}]");
        if (fightStart >= 0f && boss != null)
            sb.Append($" | boss: ×{boss.PowerScale:0.00} vida, {Time.time - fightStart:0.0} s, corazones {health.Hearts}, vida restante {boss.Health:0.0}/{boss.MaxHealth:0}, " +
                      $"bocinazos {honkOk}/{honkOk + honkFail}, esbirros avisan {minionWarnings} aciertan {minionHits}, proyectiles aciertan {projectileHits}, bonus {boss.LastVictoryBonus}");
        report.Add(sb.ToString());
        Debug.Log(sb.ToString());

        if (runsDone < runs) Invoke(nameof(BeginRun), 0.5f);
        else
        {
            Debug.Log($"[Balance] RESUMEN ({Time.realtimeSinceStartup - realStart:0} s reales)\n" + string.Join("\n", report));
            Time.timeScale = 1f;
            enabled = false;
        }
    }

    // ---------------------------------------------------------------- disparo

    private void LateUpdate()
    {
        if (gameManager.CurrentState != GameManager.GameState.Playing) { current = null; return; }

        Transform target = ChooseTarget();
        if (target == null)
        {
            // Sin nada a lo que disparar: recarga si el cargador está por debajo de la mitad (lo haría una persona)
            // (durante la toma de aire no: esa decisión ya está tomada arriba)
            if (!inHonk && ammo != null && !ammo.IsReloading && ammo.CurrentAmmo < weapons.CurrentStats.MagazineSize / 2) ammo.TryReload();
            current = null;
            return;
        }
        if (target != current)
        {
            float angle = current != null ? Vector3.Angle(cam.transform.forward, target.position - cam.transform.position) : 20f;
            readyAt = Time.time + reactionTime + angle * aimSecondsPerDegree;
            current = target;
        }
        if (Time.time < readyAt) return;
        if (ammo != null && !ammo.CanShoot) return;
        if (Time.time < nextShotTime) return;

        WeaponStats stats = weapons.CurrentStats;
        float rate = weapons.CurrentWeapon.isAutomatic ? 1f / stats.TimeBetweenShots : Mathf.Min(1f / stats.TimeBetweenShots, maxClickRate);
        nextShotTime = Time.time + 1f / rate;

        Vector3 point = AimPoint(target);
        if (Random.value > accuracy) point += (Vector3)(Random.insideUnitCircle.normalized * missOffset);
        aim.transform.rotation = Quaternion.LookRotation(point - cam.transform.position);
        if (ammo == null || ammo.UseAmmo()) shootMethod.Invoke(shoot, null);
    }

    private static Vector3 AimPoint(Transform t)
    {
        var box = t.GetComponent<BoxCollider>();
        return box != null ? t.TransformPoint(box.center) : t.position;
    }

    private Transform ChooseTarget()
    {
        if (boss != null && boss.State != BossController.BossState.Inactive && boss.State != BossController.BossState.Dead)
            return ChooseBossTarget();

        Transform best = null; float bestValue = 0f;
        foreach (Target t in FindObjectsByType<Target>(FindObjectsSortMode.None))
        {
            if (!t.CanBeHit || t.IsPenalty || t.GetComponent<BossProjectile>() != null) continue;
            Vector3 dir = t.transform.position - cam.transform.position;
            if (Vector3.Dot(dir, Vector3.forward) <= 0f) continue;
            float angle = Vector3.Angle(cam.transform.forward, dir);
            // Puntos (más un extra si es especial) repartidos entre los disparos que le quedan y divididos por
            // lo lejos que está de la mira: una diana jugosa al otro lado de la pantalla vale menos que una cercana
            float shotsLeft = Mathf.Max(1f, Mathf.Ceil(t.Health / Mathf.Max(0.01f, weapons.CurrentStats.Damage)));
            float value = (t.LevelPoints + (t.Bonus != TargetBonus.None ? 30f : 0f)) / shotsLeft / (1f + angle / 20f);
            if (t.transform == current) value *= 1.5f;   // se queda con la que ya sigue
            if (value > bestValue) { bestValue = value; best = t.transform; }
        }
        return best;
    }

    private Transform ChooseBossTarget()
    {
        BossWeakPoint[] points = boss.GetComponentsInChildren<BossWeakPoint>();
        if (inHonk)
        {
            foreach (var p in points) if (p.CanBeHit && p.Id == "Jaw") return p.transform;
            // Toma de aire con la boca aún cerrada: espera con el cargador lleno (salvo un esbirro saltando)
            foreach (var m in FindObjectsByType<MinionLunge>(FindObjectsSortMode.None)) if (m.IsActiveHazard) return m.transform;
            return null;
        }

        MinionLunge[] minions = FindObjectsByType<MinionLunge>(FindObjectsSortMode.None);
        foreach (var m in minions) if (m.IsActiveHazard) return m.transform;

        Transform best = null; float bestDist = float.MaxValue;
        foreach (var pr in FindObjectsByType<BossProjectile>(FindObjectsSortMode.None))
        {
            if (!pr.IsActiveHazard) continue;
            float d = Vector3.Distance(pr.transform.position, cam.transform.position);
            if (d < bestDist) { bestDist = d; best = pr.transform; }
        }
        if (best != null) return best;

        foreach (var m in minions) { var t = m.GetComponent<Target>(); if (t != null && t.CanBeHit) return m.transform; }

        if (current != null) { var cur = current.GetComponent<BossWeakPoint>(); if (cur != null && cur.CanBeHit) return current; }
        foreach (var p in points) if (p.CanBeHit) return p.transform;

        // Antes de que se revele el boss (nivel falso del circo) o en el reposo sin nada abierto: dianas normales
        foreach (Target t in FindObjectsByType<Target>(FindObjectsSortMode.None))
            if (t.CanBeHit && !t.IsPenalty && t.GetComponent<BossProjectile>() == null && t.GetComponent<MinionLunge>() == null) return t.transform;
        return null;
    }
}
#endif
