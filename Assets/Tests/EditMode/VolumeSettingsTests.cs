using NUnit.Framework;

public class VolumeSettingsTests
{
    [Test]
    public void AlCien_DejaLaMezclaOriginal()
    {
        Assert.AreEqual(0f, VolumeSettings.ToDecibels(1f), 1e-4f);
        Assert.AreEqual(-6f, VolumeSettings.ToDecibels(1f, -6f), 1e-4f);
    }

    [Test]
    public void LaMitad_SonMenosSeisDecibelios()
    {
        Assert.AreEqual(-6.0206f, VolumeSettings.ToDecibels(0.5f), 1e-3f);
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(0.00001f)]
    public void CeroONegativo_EsSilencio(float level)
    {
        Assert.AreEqual(VolumeSettings.SilenceDb, VolumeSettings.ToDecibels(level));
    }

    [Test]
    public void NuncaBajaDelSilencioNiSubeDeLaMezcla()
    {
        Assert.AreEqual(VolumeSettings.SilenceDb, VolumeSettings.ToDecibels(0.001f, -70f));
        Assert.AreEqual(0f, VolumeSettings.ToDecibels(2f), 1e-4f);   // por encima de 1 se recorta
    }
}
