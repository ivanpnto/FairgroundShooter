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

    // Fase 6 o posterior. Existen para fijar el contrato, pero con valores neutros
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
    // CONTRATO (Etapa 0): devuelve las estadísticas base, sin mejoras. Las fórmulas de §25 llegan
    // en la Etapa 2 (munición ×(1 + 0.25·n) hacia arriba, cadencia ×(1 − 0.10·n), daño ×(1 + 0.25·n)),
    // tomando el porcentaje de UpgradeData.valuePerLevel y el nivel de profile.GetUpgradeLevel(id).
    // profile null = sin mejoras (p. ej. antes de elegir perfil)
    public static WeaponStats Compute(WeaponData data, PlayerProfile profile, IReadOnlyList<UpgradeData> upgrades)
    {
        return new WeaponStats(
            data.baseDamage,
            data.magazineSize,
            data.TimeBetweenShots,
            data.reloadTime,
            data.maxDistance);
    }
}
