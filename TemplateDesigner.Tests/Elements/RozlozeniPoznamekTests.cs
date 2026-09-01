using SkiaSharp;
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

    // ── Vypnutý nadpis ──────────────────────────────────────────────────────

    [Fact]
    public void NadpisJeVychozeZapnuty()
    {
        Assert.True(new NotesLegendElement().ShowHeaderBar);
    }

    [Fact]
    public void VypnutyNadpis_NezabiraVysku()
    {
        var sNadpisem = new NotesLegendElement { Width = 190f, NoteFontSize = 7f };
        var bezNadpisu = new NotesLegendElement { Width = 190f, NoteFontSize = 7f, ShowHeaderBar = false };

        float sN = sNadpisem.CalculateHeightFromData(Stranka(3));
        float bezN = bezNadpisu.CalculateHeightFromData(Stranka(3));

        // Rozdíl = výška pruhu (9 pt × 0,3528 × 1,8 = 5,7 mm), ne nula.
        Assert.True(sN - bezN > 5f, $"pruh měl ubrat ~5,7 mm, ubral {sN - bezN:0.##}");
    }

    [Fact]
    public void PrazdnyNadpis_PruhNechavaBytZapnuty()
    {
        // Pás bez textu je legitimní - proto je vypínač samostatný a nevisí na prázdném textu.
        var el = new NotesLegendElement { Width = 190f, HeaderText = "" };
        var sTextem = new NotesLegendElement { Width = 190f, HeaderText = "Poznámky" };

        Assert.Equal(sTextem.CalculateHeightFromData(Stranka(3)),
                     el.CalculateHeightFromData(Stranka(3)), 3);
    }

    [Fact]
    public void VypnutyNadpis_PrezijeKopiiPrvku()
    {
        var el = new NotesLegendElement { ShowHeaderBar = false };
        Assert.False(((NotesLegendElement)el.Clone()).ShowHeaderBar);
    }

    [Fact]
    public void VypnutyNadpis_SeOpravduNenakresli()
    {
        // Výpočet výšky sám nestačí: mutací 1. 9. se ukázalo, že pruh kreslený navzdory vypnutí
        // projde zeleně - a to je horší stav než dnešní, protože by překryl první poznámku.
        var el = new NotesLegendElement { X = 0f, Y = 0f, Width = 100f, NoteFontSize = 7f };
        var data = Stranka(3);

        Assert.True(MaPodbarvenyPruhNahore(el, data), "zapnutý nadpis se má podbarvit");

        el.ShowHeaderBar = false;
        Assert.False(MaPodbarvenyPruhNahore(el, data), "vypnutý nadpis nesmí nakreslit nic");
    }

    /// <summary>Je u horní hrany prvku vodorovný pruh jiné barvy než bílé pozadí?</summary>
    private static bool MaPodbarvenyPruhNahore(NotesLegendElement el, DataContext data)
    {
        const float mmToPx = 4f;
        using var bitmap = new SKBitmap(440, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            el.Render(new RenderContext(canvas, mmToPx, data));
        }

        // Vzorek vpravo od textu nadpisu (60 mm), v polovině výšky pruhu (9 pt → 5,7 mm).
        var pixel = bitmap.GetPixel((int)(60f * mmToPx), (int)(2.5f * mmToPx));
        return pixel != SKColors.White;
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
        Assert.Contains("Poznámky", hlaska);
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
