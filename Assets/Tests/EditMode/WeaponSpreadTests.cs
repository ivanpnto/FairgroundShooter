using NUnit.Framework;
using UnityEngine;

public class WeaponSpreadTests
{
    [Test]
    public void Apuntando_NoHayDispersion()
    {
        Assert.AreEqual(0f, WeaponSpread.Current(1.5f, 2f, 1f), 1e-5f);
    }

    [Test]
    public void DesdeLaCadera_SumaBaseYApertura()
    {
        Assert.AreEqual(3.5f, WeaponSpread.Current(1.5f, 2f, 0f), 1e-5f);
        Assert.AreEqual(1.75f, WeaponSpread.Current(1.5f, 2f, 0.5f), 1e-5f);
    }

    [Test]
    public void Apertura_TieneTope()
    {
        float bloom = 0f;
        for (int i = 0; i < 50; i++) bloom = WeaponSpread.AddShot(bloom, 0.4f, 3f);
        Assert.AreEqual(3f, bloom, 1e-5f);
    }

    [Test]
    public void Recuperacion_NoDependeDeLosFps()
    {
        float a = WeaponSpread.Recover(2f, 5f, 0.1f);
        float b = 2f;
        for (int i = 0; i < 10; i++) b = WeaponSpread.Recover(b, 5f, 0.01f);
        Assert.AreEqual(a, b, 1e-4f);
        Assert.Less(a, 2f);
    }

    [Test]
    public void Desvio_GiraLosGradosPedidos()
    {
        Vector3 d = WeaponSpread.Deviate(Vector3.forward, Vector3.up, Vector3.right, new Vector2(2f, 0f));
        Assert.AreEqual(2f, Vector3.Angle(Vector3.forward, d), 1e-3f);
        Assert.Greater(d.x, 0f, "+x es a la derecha");
        Vector3 u = WeaponSpread.Deviate(Vector3.forward, Vector3.up, Vector3.right, new Vector2(0f, 3f));
        Assert.Greater(u.y, 0f, "+y es hacia arriba");
    }

    [Test]
    public void Pixeles_UnGradoConFov60()
    {
        // tan(1°) / tan(30°) × 540 ≈ 16,3 px en un Canvas de 1080 de alto
        Assert.AreEqual(16.32f, WeaponSpread.ToPixels(1f, 60f, 540f), 0.05f);
    }
}
