using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Jak se řídí rozložení poznámek: nastaví se velikost písma a výška řádku, a co je nastavené, to
/// se vytiskne. Nevejde-li se to, písmo se zmenší a aplikace to řekne.
/// </summary>
/// <remarks>
/// Kolega si stěžoval, že se legenda neroztáhne dolů, když je prvek vyšší. Roztahování se vědomě
/// nedělá (rozhodnutí Jan 1. 9.): dvě poznámky mají zůstat nahoře, protože rozprostření tří poznámek
/// na začátek, doprostřed a konec vypadá rozbitě. Skutečná chyba byla jinde - výška řádku byla pevná
/// a nereagovala na písmo, takže zvětšení písma legendu slepilo.
/// </remarks>
public class RozlozeniPoznamekTests
{
    private static DataContext Stranka(int pocetPoznamek)
    {
        var poznamky = Enumerable.Range(1, pocetPoznamek)
            .Select(i => new DataContext(new Dictionary<string, object?>
            {
                ["TYP"] = "text",
                ["ZNAK"] = i.ToString(),
                ["TEXT"] = $"poznámka číslo {i}",
            }))
            .ToList();

        return new DataContext(
            new Dictionary<string, object?>(),
            new Dictionary<string, IReadOnlyList<DataContext>> { ["POZNAMKY"] = poznamky });
    }

    // ── Výška řádku se hýbe s písmem ────────────────────────────────────────

    [Fact]
    public void NovyPrvek_NemaVyskuRadkuNastavenou()
    {
        // Nenastavená = drží proporci k písmu. Pevná hodnota byla ta past: 4 mm je auto-výška
        // pro 7 bodů, takže výchozí stav vypadal správně, ale zvětšení písma legendu slepilo.
        Assert.Null(new NotesLegendElement().NoteLineHeight);
    }

    [Theory]
    [InlineData(7f, 3.95f)]
    [InlineData(10f, 5.64f)]
    [InlineData(4f, 2.26f)]
    public void BezVlastniHodnoty_RosteVyskaRadkuSPismem(float pt, float ocekavanaMm)
    {
        var el = new NotesLegendElement { NoteFontSize = pt };
        Assert.Equal(ocekavanaMm, el.EffectiveNoteLineHeight, 2);
    }

    [Fact]
    public void VlastniVyskaRadku_SePismemNehybe()
    {
        var el = new NotesLegendElement { NoteFontSize = 7f, NoteLineHeight = 8f };
        Assert.Equal(8f, el.EffectiveNoteLineHeight);

        el.NoteFontSize = 12f;
        Assert.Equal(8f, el.EffectiveNoteLineHeight);
    }

    [Fact]
    public void ZadanaAutomatickaHodnota_ZnamenaVratitSeKPismu()
    {
        // Uživatel nemusí znát magické číslo, aby se dostal zpátky k dědění z písma - stačí zadat
        // hodnotu, kterou pole ukazuje (týž vzor jako výška řádku u zastávek a odjezdů).
        var el = new NotesLegendElement { NoteFontSize = 7f, NoteLineHeight = 8f };
        var vyskaRadku = el.GetPropertyDescriptors()
            .OfType<FloatPropertyDescriptor>()
            .Single(d => d.Label == "Výška řádku" && d.Group == "Rozložení");

        vyskaRadku.Set(3.95f);
        Assert.Null(el.NoteLineHeight);
    }

    [Fact]
    public void VelikostIVyskaRadkuJsouVeSkupineRozlozeni()
    {
        // Pavel je nenašel, protože byly mezi barvami buněk, zatímco počet sloupců je v Rozložení.
        var skupiny = new NotesLegendElement().GetPropertyDescriptors()
            .Where(d => d.Label is "Velikost poznámek" or "Výška řádku" or "Počet sloupců")
            .Select(d => d.Group)
            .Distinct()
            .ToList();

        Assert.Equal(["Rozložení"], skupiny);
    }

    // ── Zmenšení a upozornění ───────────────────────────────────────────────

    [Fact]
    public void VejdeLiSeObsah_NicSeNehlasi()
    {
        var el = new NotesLegendElement { Width = 190f, Height = 60f, NoteFontSize = 7f };
        Assert.Null(Hlaska(el, Stranka(2)));
    }

    [Fact]
    public void DvePoznamkyVeVysokemPrvku_ZustanouNahore()
    {
        // Vyplní klidně jen pětinu bloku - roztahování se nedělá (rozhodnutí Jan 1. 9.).
        var el = new NotesLegendElement { Width = 190f, Height = 100f, NoteFontSize = 7f };
        float obsah = el.CalculateHeightFromData(Stranka(2));

        Assert.True(obsah < 30f, $"dvě poznámky mají zabrat málo místa, zabraly {obsah:0.#} mm");
        Assert.Null(Hlaska(el, Stranka(2)));
    }

    [Fact]
    public void NevejdeLiSeObsah_HlaskaJmenujePrvekIMiru()
    {
        var el = new NotesLegendElement { Width = 190f, Height = 20f, NoteFontSize = 7f };
        var hlaska = Hlaska(el, Stranka(15));

        Assert.NotNull(hlaska);
        Assert.Contains("Legenda poznámek", hlaska);
        Assert.Contains("%", hlaska);
    }

    [Fact]
    public void HlaskaRikaKolikStranekSeZmensilo()
    {
        // 20 mm: dvě poznámky se vejdou (nadpis 5,7 + 2 × 3,95 mm), patnáct ne.
        var el = new NotesLegendElement { Width = 190f, Height = 20f, NoteFontSize = 7f };
        var hlaska = ContentShrinkWarnings.Summarize(Sablona(el), [Stranka(2), Stranka(15), Stranka(2)]);

        Assert.NotNull(hlaska);
        Assert.Contains("1 z 3", hlaska);
    }

    [Fact]
    public void PrvekBezPevneVysky_SeNikdyNezmensi()
    {
        // Dynamická výška roste podle obsahu, takže není z čeho zmenšovat.
        var el = new NotesLegendElement { Width = 190f, Height = null, NoteFontSize = 7f };
        Assert.Null(Hlaska(el, Stranka(30)));
    }

    private static Template Sablona(TemplateElement el)
    {
        var t = new Template();
        t.Sections.Clear();
        var section = new Section { Name = "Test" };
        section.Elements.Add(el);
        t.Sections.Add(section);
        return t;
    }

    private static string? Hlaska(TemplateElement el, DataContext stranka) =>
        ContentShrinkWarnings.Summarize(Sablona(el), [stranka]);
}
