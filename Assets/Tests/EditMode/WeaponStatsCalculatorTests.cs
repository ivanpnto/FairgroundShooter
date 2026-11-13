using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Tests de WeaponStatsCalculator (§25): las fórmulas de las mejoras de la tienda.
//
// Por qué se puede probar así: Compute es una FUNCIÓN PURA. Recibe el arma, el perfil y la lista de mejoras, y
// devuelve un WeaponStats sin leer ni escribir nada más (ni escena, ni archivos, ni estado estático). Por eso no
// hace falta abrir Main ni darle a Play: se crean los datos en memoria y se comprueba el resultado.
//
// Son tests de modo Edit (Window > General > Test Runner > EditMode > Run All). Cada test sigue el patrón
// Preparar → Actuar → Comprobar, y prueba UNA regla, para que si falla se sepa exactamente cuál se ha roto.
public class WeaponStatsCalculatorTests
{
    // Valores de la Five-seven: cargador 20, 5 disparos/s, recarga 1,5 s, daño 1
    private const int Magazine = 20;
    private const float FireRate = 5f;
    private const float ReloadTime = 1.5f;
    private const float Range = 100f;
    private const float Tolerance = 1e-4f;   // los float no se comparan con ==

    private WeaponData weapon;
    private PlayerProfile profile;
    private readonly List<Object> created = new List<Object>();

    [SetUp]
    public void SetUp()
    {
        weapon = Create<WeaponData>();
        weapon.magazineSize = Magazine;
        weapon.fireRate = FireRate;
        weapon.reloadTime = ReloadTime;
        weapon.maxDistance = Range;
        weapon.baseDamage = 1f;

        profile = new PlayerProfile("test");
    }

    // Los ScriptableObject creados con CreateInstance no son assets: si no se destruyen, se quedan en memoria
    // del editor hasta que se recarga el dominio
    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created)
            Object.DestroyImmediate(o);
        created.Clear();
    }

    // ---------------------------------------------------------------- sin mejoras

    [Test]
    public void SinPerfil_DevuelveLasEstadisticasDelArma()
    {
        WeaponStats stats = WeaponStatsCalculator.Compute(weapon, null, null);

        Assert.That(stats.Damage, Is.EqualTo(1f).Within(Tolerance));
        Assert.That(stats.MagazineSize, Is.EqualTo(Magazine));
        Assert.That(stats.TimeBetweenShots, Is.EqualTo(1f / FireRate).Within(Tolerance));
        Assert.That(stats.ReloadTime, Is.EqualTo(ReloadTime).Within(Tolerance));
        Assert.That(stats.Range, Is.EqualTo(Range).Within(Tolerance));
        Assert.That(stats.Penetration, Is.EqualTo(1), "sin mejora, solo el primer objetivo");
        Assert.That(stats.Rays, Is.EqualTo(1), "sin mejora, un solo rayo");
        Assert.That(stats.ExplosionRadius, Is.EqualTo(0f), "sin mejora, sin explosión");
    }

    [Test]
    public void PerfilSinComprasYListaDeMejoras_NoCambiaNada()
    {
        var upgrades = new List<UpgradeData> { Upgrade(UpgradeEffect.Damage, 0.25f), Upgrade(UpgradeEffect.Ammo, 0.25f) };

        WeaponStats withUpgrades = WeaponStatsCalculator.Compute(weapon, profile, upgrades);
        WeaponStats baseline = WeaponStatsCalculator.Compute(weapon, null, null);

        Assert.That(withUpgrades.Damage, Is.EqualTo(baseline.Damage).Within(Tolerance));
        Assert.That(withUpgrades.MagazineSize, Is.EqualTo(baseline.MagazineSize));
    }

    // ---------------------------------------------------------------- mejoras baratas (porcentajes)

    // [TestCase] ejecuta el mismo test con varios datos: cada fila aparece como un test aparte en el Test Runner.
    // Daño: baseDamage × (1 + 0,25·n)
    [TestCase(1, 1.25f)]
    [TestCase(2, 1.5f)]
    [TestCase(3, 1.75f)]
    public void Danyo_SumaUnVeinticincoPorCientoPorNivel(int level, float expected)
    {
        var upgrades = Buy(UpgradeEffect.Damage, 0.25f, level);

        WeaponStats stats = WeaponStatsCalculator.Compute(weapon, profile, upgrades);

        Assert.That(stats.Damage, Is.EqualTo(expected).Within(Tolerance));
    }

    // Munición: magazineSize × (1 + 0,25·n), redondeado HACIA ARRIBA (nunca se pierde una bala)
    [TestCase(20, 1, 25)]
    [TestCase(20, 3, 35)]
    [TestCase(7, 1, 9)]    // 8,75 → 9
    [TestCase(5, 3, 9)]    // 8,75 → 9
    public void Municion_RedondeaHaciaArriba(int magazine, int level, int expected)
    {
        weapon.magazineSize = magazine;
        var upgrades = Buy(UpgradeEffect.Ammo, 0.25f, level);

        WeaponStats stats = WeaponStatsCalculator.Compute(weapon, profile, upgrades);

        Assert.That(stats.MagazineSize, Is.EqualTo(expected));
    }

    // El caso que justifica el "− 0.0001" del código: 10 × 1,1 da 11,000001 en float y CeilToInt daría 12
    [Test]
    public void Municion_ErrorDeFloat_NoAnyadeUnaBalaDeMas()
    {
        weapon.magazineSize = 10;
        var upgrades = Buy(UpgradeEffect.Ammo, 0.1f, 1);

        WeaponStats stats = WeaponStatsCalculator.Compute(weapon, profile, upgrades);

        Assert.That(stats.MagazineSize, Is.EqualTo(11));
    }

    // Cadencia: tiempo entre disparos × (1 − 0,10·n)
    [TestCase(1, 0.18f)]
    [TestCase(3, 0.14f)]
    public void Cadencia_ReduceElTiempoEntreDisparos(int level, float expected)
    {
        var upgrades = Buy(UpgradeEffect.FireRate, 0.1f, level);

        WeaponStats stats = WeaponStatsCalculator.Compute(weapon, profile, upgrades);

        Assert.That(stats.TimeBetweenShots, Is.EqualTo(expected).Within(Tolerance));
    }

    // Con un valor absurdo en el asset (−150 %) el intervalo no puede ser negativo ni cero: se queda en el 10 %
    [Test]
    public void Cadencia_ConValoresAbsurdos_NoBajaDelDiezPorCiento()
    {
        var upgrades = Buy(UpgradeEffect.FireRate, 0.5f, 3);

        WeaponStats stats = WeaponStatsCalculator.Compute(weapon, profile, upgrades);

        Assert.That(stats.TimeBetweenShots, Is.EqualTo(0.1f / FireRate).Within(Tolerance));
    }

    [Test]
    public void RecargaYAlcance_NoLosTocaNingunaMejora()
    {
        var upgrades = new List<UpgradeData>();
        upgrades.AddRange(Buy(UpgradeEffect.Damage, 0.25f, 3));
        upgrades.AddRange(Buy(UpgradeEffect.FireRate, 0.1f, 3));
        upgrades.AddRange(Buy(UpgradeEffect.Ammo, 0.25f, 3));

        WeaponStats stats = WeaponStatsCalculator.Compute(weapon, profile, upgrades);

        Assert.That(stats.ReloadTime, Is.EqualTo(ReloadTime).Within(Tolerance));
        Assert.That(stats.Range, Is.EqualTo(Range).Within(Tolerance));
    }

    // ---------------------------------------------------------------- mejoras caras (unidades)

    [Test]
    public void MejorasCaras_SumanUnidadesYNoPorcentajes()
    {
        var upgrades = new List<UpgradeData>();
        upgrades.AddRange(Buy(UpgradeEffect.Penetration, 1f, 1));
        upgrades.AddRange(Buy(UpgradeEffect.ExtraRays, 1f, 1));
        upgrades.AddRange(Buy(UpgradeEffect.Explosive, 0.6f, 1));

        WeaponStats stats = WeaponStatsCalculator.Compute(weapon, profile, upgrades);

        Assert.That(stats.Penetration, Is.EqualTo(2), "atraviesa hasta 2 objetivos");
        Assert.That(stats.Rays, Is.EqualTo(2), "disparo doble");
        Assert.That(stats.ExplosionRadius, Is.EqualTo(0.6f).Within(Tolerance), "onda de 0,6 m");
    }

    // ---------------------------------------------------------------- robustez

    [Test]
    public void NivelPorEncimaDelMaximo_SeLimitaAlMaximo()
    {
        var upgrades = Buy(UpgradeEffect.Damage, 0.25f, 9, maxLevel: 3);   // un profiles.json editado a mano

        WeaponStats stats = WeaponStatsCalculator.Compute(weapon, profile, upgrades);

        Assert.That(stats.Damage, Is.EqualTo(1.75f).Within(Tolerance));
    }

    [Test]
    public void NivelNegativo_CuentaComoCero()
    {
        var upgrades = Buy(UpgradeEffect.Damage, 0.25f, -2);

        WeaponStats stats = WeaponStatsCalculator.Compute(weapon, profile, upgrades);

        Assert.That(stats.Damage, Is.EqualTo(1f).Within(Tolerance));
    }

    [Test]
    public void ElementoNuloEnLaLista_SeIgnora()
    {
        var upgrades = Buy(UpgradeEffect.Damage, 0.25f, 1);
        upgrades.Add(null);   // un hueco vacío en la lista del ProfileManager

        WeaponStats stats = WeaponStatsCalculator.Compute(weapon, profile, upgrades);

        Assert.That(stats.Damage, Is.EqualTo(1.25f).Within(Tolerance));
    }

    // Hoy hay una mejora por efecto, pero el código suma todas: si algún día hubiera dos, no se rompe
    [Test]
    public void DosMejorasConElMismoEfecto_SeSuman()
    {
        var upgrades = new List<UpgradeData>();
        upgrades.AddRange(Buy(UpgradeEffect.Damage, 0.25f, 1, id: "damage_a"));
        upgrades.AddRange(Buy(UpgradeEffect.Damage, 0.5f, 1, id: "damage_b"));

        WeaponStats stats = WeaponStatsCalculator.Compute(weapon, profile, upgrades);

        Assert.That(stats.Damage, Is.EqualTo(1.75f).Within(Tolerance));
    }

    // Lo que hace "pura" a la función: no modifica lo que recibe y, con los mismos datos, da lo mismo
    [Test]
    public void EsPura_NoModificaElArmaYEsDeterminista()
    {
        var upgrades = Buy(UpgradeEffect.Ammo, 0.25f, 2);

        WeaponStats first = WeaponStatsCalculator.Compute(weapon, profile, upgrades);
        WeaponStats second = WeaponStatsCalculator.Compute(weapon, profile, upgrades);

        Assert.That(weapon.magazineSize, Is.EqualTo(Magazine), "el WeaponData no se toca");
        Assert.That(second.MagazineSize, Is.EqualTo(first.MagazineSize));
        Assert.That(second.Damage, Is.EqualTo(first.Damage));
    }

    // ---------------------------------------------------------------- multiplicador de puntos

    [Test]
    public void MultiplicadorDePuntos_SinPerfil_EsUno()
    {
        Assert.That(WeaponStatsCalculator.ScoreMultiplier(null, null), Is.EqualTo(1f));
    }

    [TestCase(1, 1.1f)]
    [TestCase(3, 1.3f)]
    public void MultiplicadorDePuntos_SumaUnaDecimaPorNivel(int level, float expected)
    {
        var upgrades = Buy(UpgradeEffect.ScoreMultiplier, 0.1f, level);

        Assert.That(WeaponStatsCalculator.ScoreMultiplier(profile, upgrades), Is.EqualTo(expected).Within(Tolerance));
    }

    // El multiplicador de puntos lo lee ScoreManager: no debe cambiar el arma
    [Test]
    public void MultiplicadorDePuntos_NoAfectaAlArma()
    {
        var upgrades = Buy(UpgradeEffect.ScoreMultiplier, 0.1f, 3);

        WeaponStats stats = WeaponStatsCalculator.Compute(weapon, profile, upgrades);

        Assert.That(stats.Damage, Is.EqualTo(1f).Within(Tolerance));
        Assert.That(stats.MagazineSize, Is.EqualTo(Magazine));
    }

    // ---------------------------------------------------------------- utilidades

    private T Create<T>() where T : ScriptableObject
    {
        T instance = ScriptableObject.CreateInstance<T>();
        created.Add(instance);
        return instance;
    }

    private UpgradeData Upgrade(UpgradeEffect effect, float valuePerLevel, int maxLevel = 3, string id = null)
    {
        UpgradeData upgrade = Create<UpgradeData>();
        upgrade.id = id ?? effect.ToString();
        upgrade.effect = effect;
        upgrade.valuePerLevel = valuePerLevel;
        upgrade.maxLevel = maxLevel;
        return upgrade;
    }

    // Crea la mejora y la "compra" en el perfil al nivel indicado (como haría ProfileManager)
    private List<UpgradeData> Buy(UpgradeEffect effect, float valuePerLevel, int level, int maxLevel = 3, string id = null)
    {
        UpgradeData upgrade = Upgrade(effect, valuePerLevel, maxLevel, id);
        profile.upgradeLevels.Add(new UpgradeLevel(upgrade.id, level));
        return new List<UpgradeData> { upgrade };
    }
}
