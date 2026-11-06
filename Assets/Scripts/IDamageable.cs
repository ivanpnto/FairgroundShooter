// Algo que el disparo puede dañar (I7.1). La implementan Target (dianas, proyectiles del boss,
// esbirros) y BossWeakPoint (puntos débiles del boss).
//
// Por qué una interfaz y no un Target con muchísima vida en cada punto débil: Target mezcla daño con
// puntos, tiempo de vida, expiración y bonus. Un punto débil solo comparte con él "recibir daño", así
// que es lo único que se abstrae. PlayerShoot solo necesita saber si se le puede dar y dañarlo.
public interface IDamageable
{
    // false = el disparo lo ignora y sigue (derribado, expirando, punto débil cerrado...)
    bool CanBeHit { get; }

    void TakeDamage(float damage);
}
