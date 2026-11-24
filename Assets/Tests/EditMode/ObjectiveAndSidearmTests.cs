using NUnit.Framework;
using UnityEngine;

// Reglas del 24/11: objetivo de puntos por nivel y pistola de apoyo de la Barrett
public class ObjectiveAndSidearmTests
{
    // ── Objetivo ──

    [Test]
    public void Objetivo_SeCumpleAlLlegarJusto()
    {
        Assert.IsTrue(LevelConfig.ObjectiveMet(650, 650));
        Assert.IsFalse(LevelConfig.ObjectiveMet(649, 650));
    }

    [Test]
    public void SinObjetivo_SiempreSeCumple()
    {
        Assert.IsTrue(LevelConfig.ObjectiveMet(-200, 0));   // incluso con puntos negativos por los neumáticos
    }

    [Test]
    public void ModoPrueba_ReduceElObjetivoEnProporcion()
    {
        Assert.AreEqual(109, LevelConfig.ScaledTarget(650, 10f, 60f));    // 108,3 hacia arriba
        Assert.AreEqual(650, LevelConfig.ScaledTarget(650, 60f, 60f));
        Assert.AreEqual(0, LevelConfig.ScaledTarget(0, 10f, 60f));
        Assert.AreEqual(1, LevelConfig.ScaledTarget(1, 0.01f, 60f));      // nunca 0 si había objetivo
    }

    // ── Cargador propio por arma ──

    [Test]
    public void Cargador_MenosUnoEsLleno_YNuncaPasaDelTamaño()
    {
        Assert.AreEqual(6, WeaponAmmo.ClampStartAmmo(-1, 6));
        Assert.AreEqual(2, WeaponAmmo.ClampStartAmmo(2, 6));
        Assert.AreEqual(6, WeaponAmmo.ClampStartAmmo(9, 6));   // p. ej. si una mejora cambia el tamaño
        Assert.AreEqual(0, WeaponAmmo.ClampStartAmmo(0, 6));
    }

    // ── Cambio de arma ──

    [Test]
    public void SoloElVisorTelescopicoLlevaPistolaDeApoyo()
    {
        WeaponData barrett = ScriptableObject.CreateInstance<WeaponData>();
        barrett.scopeType = ScopeType.Telescopic;
        WeaponData rifle = ScriptableObject.CreateInstance<WeaponData>();
        rifle.scopeType = ScopeType.IronSights;
        WeaponData pistol = ScriptableObject.CreateInstance<WeaponData>();

        Assert.IsTrue(WeaponController.SidearmAllowed(barrett, pistol));
        Assert.IsFalse(WeaponController.SidearmAllowed(rifle, pistol));
        Assert.IsFalse(WeaponController.SidearmAllowed(pistol, pistol));   // la pistola no cambia a sí misma
        Assert.IsFalse(WeaponController.SidearmAllowed(barrett, null));

        Object.DestroyImmediate(barrett);
        Object.DestroyImmediate(rifle);
        Object.DestroyImmediate(pistol);
    }

    [Test]
    public void CurvaDelCambio_BajaYSube()
    {
        Assert.AreEqual(0f, WeaponController.SwitchCurve(0f), 1e-4f);
        Assert.AreEqual(1f, WeaponController.SwitchCurve(0.5f), 1e-4f);   // punto más bajo: ahí se cambia el modelo
        Assert.AreEqual(0f, WeaponController.SwitchCurve(1f), 1e-4f);
        Assert.AreEqual(WeaponController.SwitchCurve(0.25f), WeaponController.SwitchCurve(0.75f), 1e-4f);
    }
}
