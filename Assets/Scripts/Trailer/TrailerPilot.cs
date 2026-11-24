#if UNITY_EDITOR
using System.Reflection;
using UnityEngine;

// Herramienta del trailer (solo editor): juega sola como BalanceBot, pero apuntando como una persona
// (giro suave con algo de anticipación al movimiento) en vez de saltar de golpe a cada diana, para que
// los planos en primera persona se vean naturales.
public class TrailerPilot : MonoBehaviour
{
    public bool fire = true;
    [Tooltip("Rapidez del giro hacia la diana (1/s)")] public float turnSpeed = 6f;
    [Tooltip("Dispara cuando la mira está a menos de estos grados")] public float fireAngle = 1.5f;
    public float clickRate = 4f;
    public float lead = 0.15f;
    [Tooltip("Se agacha ante el chorro de agua")] public bool dodgeJet = true;
    [Tooltip("Mira de reposo cuando no hay nada a lo que disparar")] public Vector3 restLook = new Vector3(0f, 1.9f, 5f);
    [Tooltip("Si se fija, apunta solo a este objeto")] public Transform forcedTarget;
    [Tooltip("Ignora dianas más allá de estos grados a los lados (para que el plano no se vaya al lateral de la caseta)")]
    public float maxYaw = 26f;

    private PlayerAim aim; private PlayerShoot shoot; private WeaponAmmo ammo; private WeaponController weapons;
    private PlayerCrouch crouch; private GameManager gm; private BossController boss; private Camera cam;
    private MethodInfo shootMethod;
    private Transform current; private Vector3 lastPos; private Vector3 vel; private float nextShot;

    private void Start()
    {
        aim = FindFirstObjectByType<PlayerAim>(); shoot = FindFirstObjectByType<PlayerShoot>();
        weapons = FindFirstObjectByType<WeaponController>(); crouch = FindFirstObjectByType<PlayerCrouch>();
        gm = FindFirstObjectByType<GameManager>(); boss = FindFirstObjectByType<BossController>();
        cam = aim.GetComponent<Camera>();
        shootMethod = typeof(PlayerShoot).GetMethod("Shoot", BindingFlags.Instance | BindingFlags.NonPublic);
        ammo = typeof(PlayerShoot).GetField("weaponAmmo", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(shoot) as WeaponAmmo;
        aim.enabled = false;
    }

    private void OnDisable()
    {
        if (aim != null) aim.enabled = true;
        if (crouch != null) crouch.forceCrouch = false;
    }

    private void LateUpdate()
    {
        if (crouch != null) crouch.forceCrouch = dodgeJet && boss != null && boss.LaserStage == 2;
        Transform aimT = aim.transform;
        Transform target = gm.CurrentState == GameManager.GameState.Playing ? (forcedTarget != null ? forcedTarget : Choose()) : null;
        Vector3 point;
        if (target != null)
        {
            Vector3 p = AimPoint(target);
            if (target != current) { current = target; lastPos = p; vel = Vector3.zero; }
            vel = Vector3.Lerp(vel, (p - lastPos) / Mathf.Max(1e-4f, Time.deltaTime), 0.3f);
            lastPos = p;
            point = p + vel * lead;
        }
        else { current = null; point = restLook; }

        Quaternion want = Quaternion.LookRotation(point - aimT.position);
        float k = target != null ? turnSpeed : turnSpeed * 0.3f;
        aimT.rotation = Quaternion.Slerp(aimT.rotation, want, 1f - Mathf.Exp(-k * Time.deltaTime));

        if (!fire || target == null || ammo == null) return;
        if (!ammo.CanShoot) { if (ammo.CurrentAmmo == 0 && !ammo.IsReloading) ammo.TryReload(); return; }
        if (Vector3.Angle(aimT.forward, point - aimT.position) > fireAngle || Time.time < nextShot) return;
        WeaponStats s = weapons.CurrentStats;
        float rate = weapons.CurrentWeapon.isAutomatic ? 1f / s.TimeBetweenShots : Mathf.Min(1f / s.TimeBetweenShots, clickRate);
        nextShot = Time.time + 1f / rate;
        if (ammo.UseAmmo()) shootMethod.Invoke(shoot, null);
    }

    private static Vector3 AimPoint(Transform t)
    {
        var box = t.GetComponent<BoxCollider>();
        return box != null ? t.TransformPoint(box.center) : t.position;
    }

    private Transform Choose()
    {
        if (boss != null && boss.State != BossController.BossState.Inactive && boss.State != BossController.BossState.Dead)
        {
            if (boss.State == BossController.BossState.Telegraph && boss.CurrentAttack == BossAttack.ChargedHonk)
            {
                // Como un jugador que ha aprendido la señal: espera a que abra la boca y le dispara dentro
                foreach (var wp in boss.GetComponentsInChildren<BossWeakPoint>()) if (wp.CanBeHit && wp.Id == "Jaw") return wp.transform;
                return null;
            }
            foreach (var m in FindObjectsByType<MinionLunge>(FindObjectsSortMode.None)) if (m.IsActiveHazard) return m.transform;
            foreach (var pr in FindObjectsByType<BossProjectile>(FindObjectsSortMode.None)) if (pr.IsActiveHazard) return pr.transform;
            if (current != null) { var wp = current.GetComponent<BossWeakPoint>(); if (wp != null && wp.CanBeHit) return current; }
            foreach (var p in boss.GetComponentsInChildren<BossWeakPoint>()) if (p.CanBeHit) return p.transform;
            return null;
        }
        if (current != null) { var ct = current.GetComponent<Target>(); if (ct != null && ct.CanBeHit) return current; }
        Transform best = null; float bestAngle = 999f;
        foreach (Target t in FindObjectsByType<Target>(FindObjectsSortMode.None))
        {
            if (!t.CanBeHit || t.IsPenalty) continue;
            Vector3 dir = AimPoint(t.transform) - aim.transform.position;
            if (dir.z <= 0f) continue;
            if (Mathf.Abs(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg) > maxYaw) continue;
            float a = Vector3.Angle(aim.transform.forward, dir);
            if (t.Bonus != TargetBonus.None) a *= 0.5f;
            if (a < bestAngle) { bestAngle = a; best = t.transform; }
        }
        return best;
    }
}
#endif
