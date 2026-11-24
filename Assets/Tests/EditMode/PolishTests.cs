using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Reglas del 25/11: resoluciones de OPCIONES y nombre corto de la pista del cambio de arma
public class PolishTests
{
    private static Resolution R(int w, int h) => new Resolution { width = w, height = h };

    [Test]
    public void Resoluciones_SinRepetirPorFrecuencia_YOrdenadas()
    {
        var all = new[] { R(1920, 1080), R(1280, 720), R(1920, 1080), R(640, 480), R(2560, 1440) };
        List<Vector2Int> sizes = OptionsPanelUI.DistinctSizes(all);
        CollectionAssert.AreEqual(new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440) }, sizes);
    }

    [Test]
    public void SiguienteResolucion_DaLaVuelta_YSaltaDesdeUnaVentanaRedimensionada()
    {
        var sizes = new List<Vector2Int> { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) };
        Assert.AreEqual(1, OptionsPanelUI.NextIndex(sizes, new Vector2Int(1280, 720)));
        Assert.AreEqual(0, OptionsPanelUI.NextIndex(sizes, new Vector2Int(1920, 1080)));
        Assert.AreEqual(1, OptionsPanelUI.NextIndex(sizes, new Vector2Int(1500, 800)));   // la primera mayor
        Assert.AreEqual(0, OptionsPanelUI.NextIndex(sizes, new Vector2Int(3000, 2000)));
    }

    [Test]
    public void NombreCorto_PrimeraPalabraEnMayusculas()
    {
        WeaponData barrett = ScriptableObject.CreateInstance<WeaponData>();
        barrett.displayName = "Barrett M82A1";
        Assert.AreEqual("BARRETT", SidearmHintUI.ShortName(barrett));
        Assert.AreEqual("", SidearmHintUI.ShortName(null));
        Object.DestroyImmediate(barrett);
    }
}
