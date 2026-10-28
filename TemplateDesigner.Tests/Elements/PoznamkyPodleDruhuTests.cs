using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Rozložení poznámek linkového JŘ podle druhů (`#7487`, KORID): každá skupina druhů je vlastní
/// blok na novém řádku, se svým počtem sloupců, v pořadí, které si uživatel nastaví - „všechno do
/// tří sloupců, negativní značky do jednoho". Linkový JŘ se tak rozkládá vždy (jako starý tisk),
/// vývěska bez druhů zůstává jednou mřížkou.
/// </summary>
public class PoznamkyPodleDruhuTests
{
    private const float MmToPx = 96f / 25.4f;

    private static DataContext Poznamka(string druh, string typ, string znak, string text) =>
        new(new Dictionary<string, object?> { ["DRUH"] = druh, ["TYP"] = typ, ["ZNAK"] = znak, ["TEXT"] = text });

    private static DataContext Linkova(params DataContext[] poznamky) =>
        new(new Dictionary<string, object?>(),
            new Dictionary<string, IReadOnlyList<DataContext>> { ["POZNAMKY"] = poznamky });

    /// <summary>Výsek poznámek, jak je posílá linková agenda (druh, typ, značka).</summary>
    private static DataContext TypickaLinka() => Linkova(
        Poznamka("1", "znak", "X", "jede v pracovních dnech"),
        Poznamka("1", "znak", "+", "jede v neděli a ve státem uznané svátky"),
        Poznamka("2", "nz", "10", "nejede 24. 12."),
        Poznamka("2", "nz", "11", "nejede 31. 12."),
        Poznamka("3", "znak", "m", "spoj 21 vyčká příjezdu spoje"),
        Poznamka("5", "znak", "H", "spoj vhodný pro cestující na vozíku"),
        Poznamka("6", "znak", "R", "k jízdence je možné zakoupit místenku"),
        Poznamka("9", "text", "", "Na lince platí tarif IDS."));

    private static List<NotesLegendElement.BlokPoznamek> Bloky(NotesLegendElement el, DataContext data)
    {
        var poznamky = data.GetRows("POZNAMKY").Select(r => new PoznamkaData
        {
            Druh = r.GetString("DRUH"), Znak = r.GetString("ZNAK"), Text = r.GetString("TEXT"),
            Typ = r.GetString("TYP") switch { "nz" => TypPoznamky.NegativniZnacka, "znak" => TypPoznamky.SpecialniZnak, _ => TypPoznamky.Text },
        }).ToList();
        return el.RozdelDoBloku(el.FiltrujPoznamkyProTest(poznamky));
    }

    private static List<NastaveniSkupinyPoznamek> Vychozi(Action<NastaveniSkupinyPoznamek>? uprav = null)
    {
        var skupiny = new NotesLegendElement().EfektivniSkupiny();
        if (uprav is not null) skupiny.ForEach(uprav);
        return skupiny;
    }

    /// <summary>Tytéž poznámky bez druhu - jak je pošle vývěska.</summary>
    private static DataContext BezDruhu(params DataContext[] poznamky) => Linkova(
        [.. poznamky.Select(p => Poznamka("", p.GetString("TYP"), p.GetString("ZNAK"), p.GetString("TEXT")))]);

    // ── Bez nastavení ───────────────────────────────────────────────────────

    [Fact]
    public void BezNastaveni_RozkladaPoDruzichVychozimPoradim()
    {
        var el = new NotesLegendElement { Columns = 3 };

        var bloky = Bloky(el, TypickaLinka());

        Assert.Equal(["1", "2", "3", "5", "6", "9"], bloky.Select(b => b.Poznamky[0].Druh));
        Assert.All(bloky, b => Assert.Equal(3, b.Sloupcu));
    }

    [Fact]
    public void BezNastaveni_VypnutyTypDruhNetiskne()
    {
        // Starší šablona s vypnutými negativními značkami je po rozložení na druhy dál nemá.
        var el = new NotesLegendElement { ShowNegativniZnacky = false };

        Assert.DoesNotContain(Bloky(el, TypickaLinka()).SelectMany(b => b.Poznamky), p => p.Druh == "2");
    }

    [Fact]
    public void DleTypuUvnitrDruhu_JePoradiZDat()
    {
        // U linkového nabízí formulář „Podle typu" jako „Podle dat" - pořadí typů drží skupiny.
        var data = Linkova(
            Poznamka("7", "text", "MHD", "platí tarif MHD"),
            Poznamka("7", "znak", "x", "zastávka na znamení"));
        var el = new NotesLegendElement { Razeni = LegendRazeni.DleTypu };

        var blok = Assert.Single(Bloky(el, data));

        Assert.Equal(["MHD", "x"], blok.Poznamky.Select(p => p.Znak));
    }

    [Fact]
    public void ZastavkovaDataBezDruhu_SeNedeli()
    {
        // Druh posílá jen linková agenda; vývěska má jen jednu mřížku, i kdyby prvek skupiny nesl.
        var el = new NotesLegendElement { Skupiny = Vychozi() };
        var data = Linkova(Poznamka("", "nz", "10", "nejede 24. 12."), Poznamka("", "text", "", "tarif"));

        Assert.Single(Bloky(el, data));
    }

    // ── Skupiny ─────────────────────────────────────────────────────────────

    [Fact]
    public void SkupinyJdouVNastavenemPoradi()
    {
        var skupiny = Vychozi();
        var nz = skupiny.Single(s => s.Skupina == SkupinaPoznamek.NegativniZnacky);
        skupiny.Remove(nz);
        skupiny.Insert(0, nz);
        var el = new NotesLegendElement { Skupiny = skupiny };

        var bloky = Bloky(el, TypickaLinka());

        Assert.Equal(["2", "1", "3", "5", "6", "9"], bloky.Select(b => b.Poznamky[0].Druh));
    }

    [Fact]
    public void KazdaSkupinaMaSvujPocetSloupcu()
    {
        // KORID: „všechno do tří sloupců, negativní značky do jednoho".
        var el = new NotesLegendElement
        {
            Columns = 3,
            Skupiny = Vychozi(s => s.Sloupcu = s.Skupina == SkupinaPoznamek.NegativniZnacky ? 1 : null),
        };

        var bloky = Bloky(el, TypickaLinka());

        Assert.Equal(1, bloky.Single(b => b.Poznamky[0].Druh == "2").Sloupcu);
        Assert.All(bloky.Where(b => b.Poznamky[0].Druh != "2"), b => Assert.Equal(3, b.Sloupcu));
    }

    [Fact]
    public void VypnutaSkupinaSeNetiskne()
    {
        var el = new NotesLegendElement
        {
            Skupiny = Vychozi(s => s.Tisknout = s.Skupina != SkupinaPoznamek.Navaznosti),
        };

        Assert.DoesNotContain(Bloky(el, TypickaLinka()).SelectMany(b => b.Poznamky), p => p.Druh == "3");
    }

    [Fact]
    public void SeSkupinamiNeplatiPrepinaceTypu()
    {
        // Formulář přepínače typů u linkového JŘ neukazuje - o tisku rozhoduje skupina.
        var el = new NotesLegendElement { ShowNegativniZnacky = false, Skupiny = Vychozi() };

        Assert.Contains(Bloky(el, TypickaLinka()).SelectMany(b => b.Poznamky), p => p.Druh == "2");
    }

    [Theory]
    [InlineData("1", SkupinaPoznamek.TydenniPlatnost)]
    [InlineData("2", SkupinaPoznamek.NegativniZnacky)]
    [InlineData("3", SkupinaPoznamek.Navaznosti)]
    [InlineData("5", SkupinaPoznamek.AtributySpoju)]
    [InlineData("6", SkupinaPoznamek.DalsiZnacky)]
    [InlineData("7", SkupinaPoznamek.DalsiZnacky)]
    [InlineData("8", SkupinaPoznamek.PoznamkyLinky)]
    [InlineData("9", SkupinaPoznamek.PoznamkyLinky)]
    [InlineData("10", SkupinaPoznamek.PoznamkyLinky)]
    [InlineData("99", SkupinaPoznamek.DalsiZnacky)]
    public void DruhPatriDoSkupiny(string druh, SkupinaPoznamek skupina) =>
        Assert.Equal(skupina, NotesLegendElement.SkupinaDruhu(druh));

    [Fact]
    public void PrvniOtevreniUkazeCoPrvekTiskneDnes()
    {
        // Okno druhů se plní z přepínačů typů, takže potvrzení beze změny obsah legendy nezmění.
        var el = new NotesLegendElement { ShowNegativniZnacky = false, ShowTextovePoznamky = false };

        var skupiny = el.EfektivniSkupiny();

        Assert.Equal(Enum.GetValues<SkupinaPoznamek>(), skupiny.Select(s => s.Skupina));
        Assert.False(skupiny.Single(s => s.Skupina == SkupinaPoznamek.NegativniZnacky).Tisknout);
        Assert.False(skupiny.Single(s => s.Skupina == SkupinaPoznamek.PoznamkyLinky).Tisknout);
        Assert.True(skupiny.Single(s => s.Skupina == SkupinaPoznamek.Navaznosti).Tisknout);
    }

    [Fact]
    public void ChybejiciSkupinaSeDoplniNakonec()
    {
        // Šablona uložená dřív, než skupina přibyla, ji nesmí ztratit.
        var el = new NotesLegendElement
        {
            Skupiny = [new() { Skupina = SkupinaPoznamek.PoznamkyLinky, Sloupcu = 1 }],
        };

        var skupiny = el.EfektivniSkupiny();

        Assert.Equal(SkupinaPoznamek.PoznamkyLinky, skupiny[0].Skupina);
        Assert.Equal(6, skupiny.Count);
        Assert.All(skupiny, s => Assert.True(s.Tisknout));
    }

    // ── Výška a kreslení ────────────────────────────────────────────────────

    [Fact]
    public void KazdaSkupinaZacinaNaNovemRadku()
    {
        // Dvě poznámky ve dvou sloupcích: vývěska (bez druhů) = jeden řádek, dvě skupiny = dva řádky.
        DataContext[] poznamky = [Poznamka("2", "nz", "10", "nejede"), Poznamka("9", "text", "", "tarif")];
        var el = new NotesLegendElement { Width = 190f, Columns = 2, ShowHeaderBar = false };

        float rozdil = el.CalculateHeightFromData(Linkova(poznamky)) - el.CalculateHeightFromData(BezDruhu(poznamky));
        var spolecna = el;

        Assert.Equal(spolecna.EffectiveNoteLineHeight, rozdil, 2);
    }

    [Fact]
    public void JedenSloupecSkupinyPridaRadky()
    {
        var data = Linkova(
            Poznamka("2", "nz", "10", "a"), Poznamka("2", "nz", "11", "b"),
            Poznamka("2", "nz", "12", "c"), Poznamka("2", "nz", "13", "d"));
        var ctyri = new NotesLegendElement { Width = 190f, Columns = 4, ShowHeaderBar = false, Skupiny = Vychozi() };
        var jeden = new NotesLegendElement
        {
            Width = 190f, Columns = 4, ShowHeaderBar = false,
            Skupiny = Vychozi(s => s.Sloupcu = s.Skupina == SkupinaPoznamek.NegativniZnacky ? 1 : null),
        };

        Assert.Equal(4 * ctyri.CalculateHeightFromData(data), jeden.CalculateHeightFromData(data), 2);
    }

    [Fact]
    public void KresliDruhouSkupinuPodPrvni()
    {
        // Ve společné mřížce vývěsky stojí druhá poznámka vpravo vedle první; po druzích pod ní
        // a vpravo nezůstane nic.
        DataContext[] poznamky = [Poznamka("2", "nz", "10", "nejede"), Poznamka("9", "text", "", "tarif platí")];
        NotesLegendElement Prvek() => new() { X = 0, Y = 0, Width = 120f, Height = 30f, Columns = 2, ShowHeaderBar = false };

        Assert.True(InkoustVpravo(Prvek(), BezDruhu(poznamky)));
        Assert.False(InkoustVpravo(Prvek(), Linkova(poznamky)));
    }

    private static bool InkoustVpravo(NotesLegendElement el, DataContext data)
    {
        using var bitmap = new SKBitmap((int)(120 * MmToPx), (int)(30 * MmToPx), SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            el.Render(new RenderContext(canvas, MmToPx, data));
        }
        for (int x = bitmap.Width / 2 + 2; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
                if (bitmap.GetPixel(x, y).Red < 128) return true;
        return false;
    }

    // ── Uložení ─────────────────────────────────────────────────────────────

    [Fact]
    public void NastaveniPrezijeUlozeniIKopii()
    {
        var el = new NotesLegendElement
        {
            Skupiny = [new() { Skupina = SkupinaPoznamek.NegativniZnacky, Tisknout = false, Sloupcu = 1 }],
        };
        var sablona = new Template { Sections = [new Section { Elements = [el] }] };

        var nactena = (NotesLegendElement)TemplateSerializer.Deserialize(TemplateSerializer.Serialize(sablona))
            .Sections[0].Elements[0];
        var kopie = (NotesLegendElement)el.Clone();
        el.Skupiny[0].Sloupcu = 3;

        foreach (var x in new[] { nactena, kopie })
        {
            var s = Assert.Single(x.Skupiny!);
            Assert.Equal((SkupinaPoznamek.NegativniZnacky, false, (int?)1), (s.Skupina, s.Tisknout, s.Sloupcu));
        }
    }

    [Fact]
    public void BezNastaveniSeDoSablonyNicNepise()
    {
        var sablona = new Template { Sections = [new Section { Elements = [new NotesLegendElement()] }] };

        Assert.DoesNotContain("\"skupiny\":", TemplateSerializer.Serialize(sablona));
    }
}
