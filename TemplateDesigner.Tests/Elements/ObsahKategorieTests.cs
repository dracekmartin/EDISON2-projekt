using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Vypínání obsahu podle kategorie (rozhodnutí Jan 2026-08-04). Dřív to byly čtyři parametry sestavy
/// a vedle nich filtr v bloku poznámek - dvě místa pro jednu otázku. Teď to drží **jen šablona**
/// a server posílá nadmnožinu otagovanou kategorií.
///
/// Klíčový invariant: značka u odjezdu a její vysvětlivka v legendě visí na **jedné** volbě, takže
/// nemůže vzniknout značka, které nikdo nerozumí, ani vysvětlivka k něčemu, co na vývěsce není.
/// </summary>
public class ObsahKategorieTests
{
    private static Template Sablona(params TemplateElement[] elements)
    {
        var t = new Template();
        var s = new Section { Name = "Obsah" };
        foreach (var e in elements) s.Elements.Add(e);
        t.Sections.Add(s);
        return t;
    }

    /// <summary>Výchozí stav: kola ani místenky se netisknou, souhrn bezbariér ano (jako dosud).</summary>
    [Fact]
    public void VychoziSablona_SkryvaKolaAMistenky()
    {
        var hidden = NoteVisibilityFilter.HiddenContentCategories(Sablona(new NotesLegendElement()));

        Assert.Contains(NoteCategories.OdjezdKola, hidden);
        Assert.Contains(NoteCategories.OdjezdMistenka, hidden);
        Assert.DoesNotContain("bezbarier-agregat", hidden);
    }

    /// <summary>Zapnutím v bloku poznámek se kategorie odemkne - a protože filtr pouští jak anotace,
    /// tak řádek legendy, dostane uživatel značku i vysvětlivku jedním klikem.</summary>
    [Fact]
    public void ZapnutiVBlokuPoznamek_OdemkneKategorii()
    {
        var hidden = NoteVisibilityFilter.HiddenContentCategories(
            Sablona(new NotesLegendElement { ShowZnackaKola = true }));

        Assert.DoesNotContain("odjezd-kola", hidden);
        Assert.Contains(NoteCategories.OdjezdMistenka, hidden);   // druhá kategorie se tím nezapne
    }

    /// <summary>Pokračující spoje (#13093 bod 3) - jedna volba drží značku `u1` u minuty i její
    /// vysvětlivku, takže nemůže vzniknout značka, které nikdo nerozumí.</summary>
    [Fact]
    public void PokracujiciSpoje_VychoziSkryte_ZapnutimOdemcene()
    {
        Assert.Contains(NoteCategories.Pokracovani,
            NoteVisibilityFilter.HiddenContentCategories(Sablona(new NotesLegendElement())));

        Assert.DoesNotContain(NoteCategories.Pokracovani,
            NoteVisibilityFilter.HiddenContentCategories(
                Sablona(new NotesLegendElement { ShowPokracujiciSpoje = true })));
    }

    /// <summary>Souhrn bezbariér se schová jen když ho žádný blok poznámek nechce (dřív parametr).</summary>
    [Fact]
    public void VypnutySouhrnBezbarier_SeSkryje()
    {
        var hidden = NoteVisibilityFilter.HiddenContentCategories(
            Sablona(new NotesLegendElement { ShowSouhrnBezbarier = false }));

        Assert.Contains(NoteCategories.BezbarierAgregat, hidden);
    }

    /// <summary>Dva bloky poznámek: stačí, když kategorii chce jeden z nich - jinak by druhý blok
    /// (např. „jen výluky") umlčel značky celé vývěsky.</summary>
    [Fact]
    public void StaciJedenBlokZDvou()
    {
        var hidden = NoteVisibilityFilter.HiddenContentCategories(Sablona(
            new NotesLegendElement { ShowZnackaKola = false, Vyluky = VylukyFiltr.Jen },
            new NotesLegendElement { ShowZnackaKola = true }));

        Assert.DoesNotContain("odjezd-kola", hidden);
    }

    /// <summary>Šablona bez bloku poznámek značky nezapne - vědomý důsledek toho, že vypínání obsahu
    /// drží jeden prvek. Kdyby se to mělo změnit, je to tenhle test, který na to upozorní.</summary>
    [Fact]
    public void SablonaBezBlokuPoznamek_ZnackyNetiskne()
    {
        var hidden = NoteVisibilityFilter.HiddenContentCategories(Sablona(new DepartureGridElement()));

        Assert.Contains(NoteCategories.OdjezdKola, hidden);
        Assert.Contains(NoteCategories.OdjezdMistenka, hidden);
    }
}
