using NUnit.Framework;

// Tests del balance (bloque 3): escalado del boss con el equipo y perfiles prefabricados (perfil Demo)
public class BalanceTests
{
    private const float Tolerance = 1e-3f;

    // Five-seven sin mejoras: daño 1, cargador 12, 5 disparos/s (0,2 s), recarga 1,5 s
    private static WeaponStats Starter(int rays = 1, float explosion = 0f) =>
        new WeaponStats(1f, 12, 0.2f, 1.5f, 100f, 1, rays, explosion);

    [Test]
    public void DanyoSostenido_CuentaLaRecarga()
    {
        // 12 disparos en 2,4 s + 1,5 s de recarga → 12 / 3,9 = 3,077 de daño por segundo
        float dps = BossPowerScaling.SustainedDamage(Starter(), false, 0.5f, 0.5f);
        Assert.That(dps, Is.EqualTo(12f / 3.9f).Within(Tolerance));
    }

    [Test]
    public void DanyoSostenido_SemiautomaticaLimitadaPorLosClics()
    {
        // Un arma semiautomática a 20 disparos/s no se puede disparar tan rápido a mano: tope de 6 clics/s
        var fast = new WeaponStats(1f, 12, 0.05f, 1.5f, 100f);
        float semi = BossPowerScaling.SustainedDamage(fast, false, 0.5f, 0.5f);
        float auto = BossPowerScaling.SustainedDamage(fast, true, 0.5f, 0.5f);
        Assert.That(semi, Is.EqualTo(12f / (12f / BossPowerScaling.HumanClickRate + 1.5f)).Within(Tolerance));
        Assert.That(auto, Is.GreaterThan(semi));
    }

    [Test]
    public void DanyoSostenido_DisparoDobleYExplosionCuentanEnParte()
    {
        float baseDps = BossPowerScaling.SustainedDamage(Starter(), false, 0.5f, 0.5f);
        float both = BossPowerScaling.SustainedDamage(Starter(rays: 2, explosion: 0.6f), false, 0.5f, 0.5f);
        Assert.That(both / baseDps, Is.EqualTo(1.5f * 1.5f).Within(Tolerance));
    }

    [TestCase(1f, 1f)]        // arma inicial: sin escalado
    [TestCase(0.5f, 1f)]      // un arma peor nunca baja la vida del boss
    [TestCase(4f, 2.8284f)]   // 4 ^ 0,75
    [TestCase(16f, 4f)]       // 8, pero con tope en 4
    public void Escala_PotenciaElevadaAlExponenteConLimites(float power, float expected)
    {
        Assert.That(BossPowerScaling.Scale(power, 0.75f, 4f), Is.EqualTo(expected).Within(Tolerance));
    }

    [Test]
    public void Escala_ExponenteCero_SinEscalado()
    {
        Assert.That(BossPowerScaling.Scale(10f, 0f, 4f), Is.EqualTo(1f));
    }

    [Test]
    public void PresetDemo_TraeElNivel4YNoDaElPerfilPorTerminado()
    {
        PlayerProfile demo = ProfilePreset.DefaultDemo().Build();
        Assert.That(demo.GetUpgradeLevel("boss_level"), Is.EqualTo(1), "el nivel 4 viene comprado");
        Assert.That(demo.GetUpgradeLevel("explosive"), Is.EqualTo(0), "no trae la tienda entera");
        Assert.That(demo.seenHowToPlay, Is.False, "quien lo use ve la pantalla de controles");
        Assert.That(demo.credits, Is.GreaterThan(0), "trae créditos para probar la tienda");
    }

    [Test]
    public void Preset_ConstruyeCopias()
    {
        ProfilePreset preset = ProfilePreset.DefaultDemo();
        PlayerProfile a = preset.Build();
        a.upgradeLevels[0].level = 99;   // modificar un perfil construido no toca el preset
        Assert.That(preset.Build().upgradeLevels[0].level, Is.Not.EqualTo(99));
    }
}
