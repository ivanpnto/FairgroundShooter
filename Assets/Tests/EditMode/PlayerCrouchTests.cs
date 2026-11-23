using NUnit.Framework;

public class PlayerCrouchTests
{
    // Con los valores de BossConfig: rayo a 1,45 m, margen 0,1; cámara de pie a 1,60 y agachada a 1,10

    [Test]
    public void DePie_ElRayoAlcanza()
    {
        Assert.IsFalse(PlayerCrouch.IsBelowBeam(1.60f, 1.45f, 0.1f));
    }

    [Test]
    public void Agachado_SeSalva()
    {
        Assert.IsTrue(PlayerCrouch.IsBelowBeam(1.10f, 1.45f, 0.1f));
    }

    [Test]
    public void AMedioAgacharse_SoloSiQuedaPorDebajoDelMargen()
    {
        Assert.IsFalse(PlayerCrouch.IsBelowBeam(1.40f, 1.45f, 0.1f), "por debajo del rayo pero dentro del margen");
        Assert.IsTrue(PlayerCrouch.IsBelowBeam(1.35f, 1.45f, 0.1f), "justo en el límite");
    }
}
