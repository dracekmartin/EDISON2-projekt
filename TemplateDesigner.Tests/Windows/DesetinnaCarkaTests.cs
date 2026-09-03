using TemplateDesigner.Models.Elements;
using TemplateDesigner;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Desetinná čárka na cestě z formuláře do prvku. Čárku normalizuje už EDInputForm, tohle je
/// druhá vrstva pro hodnoty, které přijdou jinudy - bez ní se „1,5" nepřečetlo a prvek dostal
/// záložní nulu, tedy šířku 0 bez jediné hlášky (nález Jan 1. 9., #13158).
/// </summary>
public class DesetinnaCarkaTests
{
    [Theory]
    [InlineData("1,5", 1.5f)]
    [InlineData("1.5", 1.5f)]
    [InlineData("-2,5", -2.5f)]
    [InlineData("1,500", 1.5f)]   // čárka = desetinná, ne oddělovač tisíců
    [InlineData("40", 40f)]
    public void SirkaPrijmeCarkuITecku(string zadano, float ocekavano)
    {
        var el = new TextElement();
        ElementPropertyAccessor.Set(el, "Width", zadano);
        Assert.Equal(ocekavano, el.Width, 4);
    }

    [Fact]
    public void CarkaNesrazíSirkuNaNulu()
    {
        var el = new TextElement { Width = 40f };
        ElementPropertyAccessor.Set(el, "Width", "12,5");
        Assert.Equal(12.5f, el.Width, 4);
    }

    [Fact]
    public void NepovinnaVyskaSCarkou_NezustanePrazdna()
    {
        // Height je nullable (prázdno = dynamická výška podle dat), takže neúspěšné čtení
        // hodnoty se dřív projevilo jako „výšku počítá engine", ne jako chyba.
        var el = new TextElement { Height = 20f };
        ElementPropertyAccessor.Set(el, "Height", "7,5");
        Assert.Equal(7.5f, el.Height);
    }

    [Fact]
    public void PrazdnaHodnotaUNepovinnehoPole_ZnamenaDedit()
    {
        var el = new TextElement { Height = 20f };
        ElementPropertyAccessor.Set(el, "Height", "");
        Assert.Null(el.Height);
    }

    [Fact]
    public void TextovaVlastnostSiCarkuPodrzi()
    {
        var el = new TextElement();
        ElementPropertyAccessor.Set(el, "Text", "Praha, Brno");
        Assert.Equal("Praha, Brno", el.Text);
    }
}
