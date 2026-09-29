using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>Řazení „Dle typu" v prvku Poznámky: dny platnosti jdou první (rozhodnutí Jan 29. 9.).</summary>
public class NotesLegendRazeniTests
{
    private static PoznamkaData Pozn(string znak, TypPoznamky typ, string kategorie = "", string druh = "") =>
        new() { Znak = znak, Typ = typ, Kategorie = kategorie, Druh = druh, Text = znak };

    [Fact]
    public void Zastavkova_DnyPlatnostiPredNegativnimiZnackami()
    {
        var el = new NotesLegendElement();
        var poznamky = new List<PoznamkaData>
        {
            Pozn("a", TypPoznamky.Text, "rozdil-trasy"),
            Pozn("21", TypPoznamky.NegativniZnacka, "omezeni"),
            Pozn("x", TypPoznamky.SpecialniZnak, "atribut-zastavky"),
            Pozn("X", TypPoznamky.SpecialniZnak, "tydenni-platnost"),
            Pozn("6", TypPoznamky.SpecialniZnak, "tydenni-platnost"),
        };

        Assert.Equal(["X", "6", "21", "x", "a"], el.OrderNotes(poznamky).Select(p => p.Znak));
    }

    [Fact]
    public void Linkova_DruhJednaPredNegativnimiZnackami()
    {
        var el = new NotesLegendElement();
        var poznamky = new List<PoznamkaData>
        {
            Pozn("21", TypPoznamky.NegativniZnacka, druh: "2"),
            Pozn("R", TypPoznamky.SpecialniZnak, druh: "6"),
            Pozn("X", TypPoznamky.SpecialniZnak, druh: "1"),
        };

        Assert.Equal(["X", "21", "R"], el.OrderNotes(poznamky).Select(p => p.Znak));
    }

    [Fact]
    public void RazeniDleDat_ZustavaBezeZmeny()
    {
        var el = new NotesLegendElement { Razeni = LegendRazeni.Vychozi };
        var poznamky = new List<PoznamkaData>
        {
            Pozn("21", TypPoznamky.NegativniZnacka, druh: "2"),
            Pozn("X", TypPoznamky.SpecialniZnak, druh: "1"),
        };

        Assert.Equal(["21", "X"], el.OrderNotes(poznamky).Select(p => p.Znak));
    }
}
