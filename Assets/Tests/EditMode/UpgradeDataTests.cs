using NUnit.Framework;
using UnityEngine;

// Tests de UpgradeData.NextLevelCost: lo que la tienda enseña como precio y lo que cobra ProfileManager
public class UpgradeDataTests
{
    private UpgradeData upgrade;

    [SetUp]
    public void SetUp()
    {
        upgrade = ScriptableObject.CreateInstance<UpgradeData>();
        upgrade.maxLevel = 3;
        upgrade.costPerLevel = new[] { 120, 320, 560 };   // Munición (§25)
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(upgrade);

    [TestCase(0, 120)]
    [TestCase(1, 320)]
    [TestCase(2, 560)]
    public void NextLevelCost_DevuelveElPrecioDelSiguienteNivel(int currentLevel, int expected)
    {
        Assert.That(upgrade.NextLevelCost(currentLevel), Is.EqualTo(expected));
    }

    [TestCase(3)]
    [TestCase(4)]
    public void NextLevelCost_AlMaximo_DevuelveMenosUno(int currentLevel)
    {
        Assert.That(upgrade.IsMaxed(currentLevel), Is.True);
        Assert.That(upgrade.NextLevelCost(currentLevel), Is.EqualTo(-1));
    }

    [Test]
    public void NextLevelCost_FaltanPrecios_DevuelveMenosUnoEnVezDeFallar()
    {
        upgrade.costPerLevel = new[] { 120 };   // asset a medio rellenar

        Assert.That(upgrade.NextLevelCost(1), Is.EqualTo(-1));
    }

    [Test]
    public void NextLevelCost_NivelNegativo_DevuelveMenosUno()
    {
        Assert.That(upgrade.NextLevelCost(-1), Is.EqualTo(-1));
    }
}
