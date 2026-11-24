using NUnit.Framework;

// Etiqueta de papel de la botica (BoticaSlot.PlateName): una sola línea, los nombres largos se acortan
public class BoticaPlateTests
{
    [Test]
    public void NombreCorto_SeQuedaIgual()
    {
        Assert.AreEqual("MUNICIÓN", BoticaSlot.PlateName("Munición"));
        Assert.AreEqual("BALA EXPLOSIVA", BoticaSlot.PlateName("Bala explosiva"));
    }

    [Test]
    public void NombreLargo_SeCortaAntesDeDe()
    {
        Assert.AreEqual("MULTIPLICADOR", BoticaSlot.PlateName("Multiplicador de puntos"));
    }

    [Test]
    public void NombreLargoSinDe_NoSeCorta()
    {
        Assert.AreEqual("SUPERCALIFRAGILISTICO", BoticaSlot.PlateName("Supercalifragilistico"));
    }
}
