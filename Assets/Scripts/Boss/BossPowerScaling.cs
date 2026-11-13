using UnityEngine;

// Escalado del boss con el equipo del jugador (balance, bloque 3).
//
// Problema: con todas las mejoras y un arma buena, el boss caía en ~12 s sin llegar a enseñar el bocinazo. La
// tienda tiene que notarse, pero el combate final no puede convertirse en un trámite.
//
// Solución: al empezar el combate se compara el daño SOSTENIDO del arma equipada (con las mejoras) con el del arma
// inicial sin mejoras, y la vida del boss se multiplica por esa relación elevada a un exponente < 1:
//   escala = clamp(potencia ^ exponente, 1, máximo)
// Con exponente 1 el combate duraría lo mismo con cualquier equipo (las mejoras no servirían de nada); con 0 no
// habría escalado. Entre medias, el jugador mejor equipado sigue tardando menos, pero no 5 veces menos.
//
// Función pura (como WeaponStatsCalculator): mismos datos, mismo resultado; se prueba sin escena.
public static class BossPowerScaling
{
    // Un jugador no hace más de ~6 clics por segundo con un arma semiautomática, aunque el arma lo permita
    public const float HumanClickRate = 6f;

    // Daño medio por segundo contando las recargas: daño por disparo × balas del cargador / (tiempo de vaciarlo + recarga).
    // Disparo doble y explosión se cuentan solo en parte (efficiency): contra los puntos débiles del boss, el segundo
    // rayo (3,5° de separación) y la onda no siempre aciertan
    public static float SustainedDamage(WeaponStats stats, bool automatic, float extraRayEfficiency, float explosionBonus)
    {
        float interval = automatic ? stats.TimeBetweenShots : Mathf.Max(stats.TimeBetweenShots, 1f / HumanClickRate);
        float perShot = stats.Damage
                        * (1f + extraRayEfficiency * Mathf.Max(0, stats.Rays - 1))
                        * (stats.ExplosionRadius > 0f ? 1f + explosionBonus : 1f);
        int magazine = Mathf.Max(1, stats.MagazineSize);
        return perShot * magazine / (magazine * interval + stats.ReloadTime);
    }

    // Potencia relativa (1 = el arma inicial sin mejoras) → multiplicador de la vida del boss
    public static float Scale(float power, float exponent, float max)
    {
        return Mathf.Clamp(Mathf.Pow(Mathf.Max(0f, power), exponent), 1f, Mathf.Max(1f, max));
    }
}
