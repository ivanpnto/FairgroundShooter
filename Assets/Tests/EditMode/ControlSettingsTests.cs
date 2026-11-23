using NUnit.Framework;
using UnityEngine;

public class ControlSettingsTests
{
    [Test]
    public void Multiplicador_EscalaLosDosEjes()
    {
        Vector2 d = ControlSettings.MouseToDegrees(new Vector2(10f, 4f), 0.1f, 2f, false);
        Assert.AreEqual(2f, d.x, 1e-5f);
        Assert.AreEqual(0.8f, d.y, 1e-5f);
    }

    [Test]
    public void Invertir_SoloCambiaElEjeVertical()
    {
        Vector2 normal = ControlSettings.MouseToDegrees(new Vector2(10f, 4f), 0.1f, 1f, false);
        Vector2 inverted = ControlSettings.MouseToDegrees(new Vector2(10f, 4f), 0.1f, 1f, true);
        Assert.AreEqual(normal.x, inverted.x, 1e-5f);
        Assert.AreEqual(-normal.y, inverted.y, 1e-5f);
    }

    [TestCase(0f, ControlSettings.MinSensitivity)]
    [TestCase(-5f, ControlSettings.MinSensitivity)]
    [TestCase(99f, ControlSettings.MaxSensitivity)]
    [TestCase(1.5f, 1.5f)]
    public void Sensibilidad_SeRecortaAlRango(float value, float expected)
    {
        Assert.AreEqual(expected, ControlSettings.ClampSensitivity(value), 1e-5f);
    }

    [Test]
    public void Sensibilidad_NaN_VuelveAlValorPorDefecto()
    {
        Assert.AreEqual(ControlSettings.DefaultSensitivity, ControlSettings.ClampSensitivity(float.NaN));
    }
}
