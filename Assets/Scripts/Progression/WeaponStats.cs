using System.Collections.Generic;
using UnityEngine;

// Estadísticas EFECTIVAS del arma equipada: las de WeaponData con las mejoras del perfil aplicadas.
// A partir de la Etapa 2, munición, cadencia, alcance y daño se leen de aquí y no de WeaponData
// (que se queda para lo visual y el sonido). Struct de solo lectura, como ShotResult.
public readonly struct WeaponStats
{
    public readonly float Damage;
    public readonly int MagazineSize;
    public readonly float TimeBetweenShots;
    public readonly float ReloadTime;
    public readonly float Range;

    // Mejoras caras (I6.2). Sin ellas: 1 objetivo, 1 rayo, sin explosión
    public readonly int Penetration;        // objetivos que atraviesa un disparo (1 = el primero)
    public readonly int Rays;               // rayos por disparo (1 = uno)
    public readonly float ExplosionRadius;  // 0 = sin explosión

    public WeaponStats(float damage, int magazineSize, float timeBetweenShots, float reloadTime, float range,
        int penetration = 1, int rays = 1, float explosionRadius = 0f)
    {
        Damage = damage;
        MagazineSize = magazineSize;
        TimeBetweenShots = timeBetweenShots;
        ReloadTime = reloadTime;
        Range = range;
        Penetration = penetration;
        Rays = rays;
        ExplosionRadius = explosionRadius;
    }
}

// Función pura: mismos datos, mismo resultado, sin leer ni escribir nada más. Así se puede probar
// sin escena y WeaponController solo tiene que llamarla al equipar y al cambiar el perfil
public static class WeaponStatsCalculator
{
    // La cadencia nunca baja de este factor del intervalo base, por si alguien pone valores
    // absurdos en un UpgradeData (con 1 − 0.10·n y 3 niveles se queda en 0.7)
    private const float MinFireIntervalFactor = 0.1f;

    // Fórmulas de §25, con el porcentaje de cada nivel tomado de UpgradeData.valuePerLevel
    // (Munición 0.25, Cadencia 0.10, Daño 0.25) y el nivel, del perfil:
    // - munición: magazineSize × (1 + v·n), hacia arriba;
    // - cadencia: timeBetweenShots × (1 − v·n);
    // - daño: baseDamage × (1 + v·n).
    // ScoreMultiplier no toca el arma (ver ScoreMultiplier). profile o upgrades null = sin mejoras
    public static WeaponStats Compute(WeaponData data, PlayerProfile profile, IReadOnlyList<UpgradeData> upgrades)
    {
        float ammoBonus = Bonus(UpgradeEffect.Ammo, profile, upgrades);
        float fireRateBonus = Bonus(UpgradeEffect.FireRate, profile, upgrades);
        float damageBonus = Bonus(UpgradeEffect.Damage, profile, upgrades);

        // − 0.0001 antes de redondear hacia arriba: con valores como 0.1, 10 × 1.1 da 11.000001 en
        // float y CeilToInt lo convertiría en 12
        int magazine = Mathf.Max(1, Mathf.CeilToInt(data.magazineSize * (1f + ammoBonus) - 0.0001f));
        float interval = data.TimeBetweenShots * Mathf.Max(MinFireIntervalFactor, 1f - fireRateBonus);

        // Mejoras caras: suman unidades, no porcentajes. Se redondean porque no hay "medio rayo"
        int penetration = 1 + Mathf.RoundToInt(Bonus(UpgradeEffect.Penetration, profile, upgrades));
        int rays = 1 + Mathf.RoundToInt(Bonus(UpgradeEffect.ExtraRays, profile, upgrades));
        float explosionRadius = Bonus(UpgradeEffect.Explosive, profile, upgrades);

        return new WeaponStats(
            data.baseDamage * (1f + damageBonus),
            magazine,
            interval,
            data.reloadTime,
            data.maxDistance,
            penetration,
            rays,
            explosionRadius);
    }

    // Multiplicador de puntos de la mejora ScoreMultiplier: 1 + v·n (1 sin mejoras)
    public static float ScoreMultiplier(PlayerProfile profile, IReadOnlyList<UpgradeData> upgrades)
    {
        return 1f + Bonus(UpgradeEffect.ScoreMultiplier, profile, upgrades);
    }

    // Suma de v·n de todas las mejoras con ese efecto (hoy hay una por efecto, pero así no se rompe si hubiera dos)
    private static float Bonus(UpgradeEffect effect, PlayerProfile profile, IReadOnlyList<UpgradeData> upgrades)
    {
        if (profile == null || upgrades == null)
            return 0f;

        float total = 0f;
        foreach (UpgradeData upgrade in upgrades)
        {
            if (upgrade == null || upgrade.effect != effect)
                continue;
            int level = Mathf.Clamp(profile.GetUpgradeLevel(upgrade.id), 0, upgrade.maxLevel);
            total += upgrade.valuePerLevel * level;
        }
        return total;
    }
}
