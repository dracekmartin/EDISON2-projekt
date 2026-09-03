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

    // ── Písmena variant tras ────────────────────────────────────────────────────
    // Vysvětlením písmene je záhlaví variantního sloupce, ne řádek legendy. Bez toho sloupce se
    // písmeno nemá čím vysvětlit a legenda ho navíc vysvětlí cizím textem, protože řady písmen
    // variant a rozdílů tras se dnes kryjí (nález na zákaznické vývěsce linky 111).

    /// <summary>Sloupec Varianty tras je u nového prvku viditelný z výroby, takže nové šablony
    /// písmena tisknou dál. Kdyby se ten výchozí stav změnil, spadne tenhle test.</summary>
    [Fact]
    public void ZapnutySloupecVariant_PismenaSeTisknou()
    {
        var hidden = NoteVisibilityFilter.HiddenContentCategories(Sablona(new StopListElement()));

        Assert.DoesNotContain(NoteCategories.VariantaTrasy, hidden);
    }

    /// <summary>Skrytý sloupec Varianty tras písmena u minut zhasne. Přesně tohle je stav šablon
    /// převedených migrací 1.7.0 → 1.8.0, které sloupec zapnutý nedostaly.</summary>
    [Fact]
    public void SkrytySloupecVariant_PismenaSeNetisknou()
    {
        var seznam = new StopListElement();
        seznam.Columns.Single(c => c.Key == StopFields.TravelTimeVariants).Visible = false;

        var hidden = NoteVisibilityFilter.HiddenContentCategories(Sablona(seznam));

        Assert.Contains(NoteCategories.VariantaTrasy, hidden);
    }

    /// <summary>Dva seznamy zastávek: stačí, když sloupec má jeden z nich - jinak by druhý
    /// (např. zúžený na názvy) umlčel písmena celé vývěsky. Stejné pravidlo jako u bloků poznámek.</summary>
    [Fact]
    public void StaciJedenSeznamZastavekZDvou()
    {
        var bezVariant = new StopListElement();
        bezVariant.Columns.Single(c => c.Key == StopFields.TravelTimeVariants).Visible = false;

        var hidden = NoteVisibilityFilter.HiddenContentCategories(
            Sablona(bezVariant, new StopListElement()));

        Assert.DoesNotContain(NoteCategories.VariantaTrasy, hidden);
    }

    /// <summary>Šablona bez seznamu zastávek (jen minuty odjezdů) písmena netiskne - vysvětlení
    /// tam nemá kde vzniknout (rozhodnutí Jan 2026-09-02). U přestupů je to naopak, tam značka
    /// dává smysl i bez seznamu zastávek.</summary>
    [Fact]
    public void SablonaBezSeznamuZastavek_PismenaSeNetisknou()
    {
        var hidden = NoteVisibilityFilter.HiddenContentCategories(Sablona(new DepartureGridElement()));

        Assert.Contains(NoteCategories.VariantaTrasy, hidden);
    }
}
