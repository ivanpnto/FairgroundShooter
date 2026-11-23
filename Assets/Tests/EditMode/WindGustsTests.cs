using NUnit.Framework;

// Límite del empujón del viento (WindGusts.LimitPush): nunca saca una diana de la zona de juego
public class WindGustsTests
{
    private const float Limit = 5.3f;

    [Test]
    public void HaciaDentro_EmpujaEntero()
    {
        Assert.AreEqual(-0.5f, WindGusts.LimitPush(4f, -0.5f, Limit), 1e-5f);
        Assert.AreEqual(0.5f, WindGusts.LimitPush(-6.5f, 0.5f, Limit), 1e-5f);   // entrando desde detrás de la pared
    }

    [Test]
    public void HaciaFuera_SoloHastaElBorde()
    {
        Assert.AreEqual(0.3f, WindGusts.LimitPush(5f, 0.5f, Limit), 1e-5f);
        Assert.AreEqual(-0.3f, WindGusts.LimitPush(-5f, -0.5f, Limit), 1e-5f);
    }

    [Test]
    public void FueraDeLaZona_NoLaEmpujaMasAfuera()
    {
        Assert.AreEqual(0f, WindGusts.LimitPush(6.8f, 0.2f, Limit), 1e-5f);
        Assert.AreEqual(0f, WindGusts.LimitPush(-6.8f, -0.2f, Limit), 1e-5f);
    }

    [Test]
    public void EnElCentro_EmpujaEntero()
    {
        Assert.AreEqual(0.4f, WindGusts.LimitPush(0f, 0.4f, Limit), 1e-5f);
        Assert.AreEqual(0.4f, WindGusts.LimitPush(1f, 0.4f, Limit), 1e-5f);
    }
}
