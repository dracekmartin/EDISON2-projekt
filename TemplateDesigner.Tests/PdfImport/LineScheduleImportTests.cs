using System.IO;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.PdfImport;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.PdfImport;

/// <summary>
/// Import linkového JŘ z PDF. Do 2. 10. se v linkové agendě AI vůbec nevolala a z PDF vznikly
/// jen volné texty a čáry; šablona linkového JŘ přitom potřebuje tabulku spojů, hlavičku na každé
/// stránce a tabulku, která pokračuje přes stránky.
/// </summary>
public class LineScheduleImportTests
{
    [Theory]
    [InlineData("ZASTAVKOVY_JR_AD", ScheduleImportType.ZastavkovyJrMhd)]
    [InlineData("ZASTAVKOVY_JR_MHD", ScheduleImportType.ZastavkovyJrMhd)]
    [InlineData("LINKOVY_JR_AD", ScheduleImportType.LinkovyJr)]
    [InlineData("LINKOVY_JR_MHD", ScheduleImportType.LinkovyJr)]
    [InlineData("FAKTURY", ScheduleImportType.None)]
    [InlineData("GENERAL", ScheduleImportType.None)]
    [InlineData("", ScheduleImportType.None)]
    [InlineData(null, ScheduleImportType.None)]
    public void RezimImportuSeOdvodiZAgendy(string? agenda, ScheduleImportType expected)
    {
        Assert.Equal(expected, ScheduleImportTypes.ForAgenda(agenda));
    }

    // ── Na co se model u tabulky spojů ptá ──────────────────────────────

    [Theory]
    [InlineData("Čísla spojů")]
    [InlineData("Formát času")]
    [InlineData("Popisné sloupce bez vnitřních čar")]
    [InlineData("Podbarvit lichá Tč")]
    public void NaViditelneVolbyTabulkySeModelPta(string popisek)
    {
        Assert.Contains(popisek, ElementSettingsFromImage.BuildCatalog(new LineTableElement()));
    }

    /// <summary>Tyhle řádky na předloze chybí i tehdy, když linka jen nemá platnost, značky nebo
    /// přípoje - vypnout je podle obrázku by je schovalo lince, která je má. Tučné zastávky a čáry
    /// u zastávek jsou údaj z trasy linky, ne vzhled. Prázdný nadpis skupiny dnů vypadá jako
    /// vypnutý a nadpis popisných sloupců model pletl se záhlavím sloupce (zkouška 2. 10.).</summary>
    [Theory]
    [InlineData("Řádek platnosti")]
    [InlineData("Řádek negativních značek")]
    [InlineData("Řádky · Přípojné řádky")]
    [InlineData("Tučné zastávky dle trasy")]
    [InlineData("Čáry u zastávek dle trasy")]
    [InlineData("Nadpisy skupin dnů")]
    [InlineData("Nadpis popisných sloupců")]
    public void NaVolbyNepotvrditelneZPapiruSeNeptame(string klic)
    {
        Assert.DoesNotContain(klic, ElementSettingsFromImage.BuildCatalog(new LineTableElement()));
    }

    [Fact]
    public void OdpovedModeluSeDoTabulkyZapise()
    {
        var el = new LineTableElement();

        ElementSettingsFromImage.Apply(el, new Dictionary<string, string>
        {
            ["Popisné sloupce · Popisné sloupce bez vnitřních čar"] = "ano",
            ["Datové buňky · Formát času"] = "Celý čas stejně (hh:mm)",
        });

        Assert.True(el.PopisneSloupceBezVnitrnichCar);
        Assert.Equal(FormatCasuSpoje.SDvojteckou, el.FormatCasu);
    }

    [Fact]
    public void CerstvaTabulkaNemaVProtokoluOdchylky()
    {
        Assert.Empty(ElementSettingsFromImage.DiffFromDefault(new LineTableElement()));
    }

    // ── Popisné sloupce ─────────────────────────────────────────────────

    [Fact]
    public void SloupceDostanouPoradiZPredlohy()
    {
        var cols = AiGuidedScheduleBuilder.BuildLineColumnsFromAi(["Km", "Tc", "StopName", "Zone"]);

        Assert.Equal(
            [ColumnKeys.Km, ColumnKeys.Tc, ColumnKeys.StopName, ColumnKeys.Zone,
             ColumnKeys.Arrow, ColumnKeys.Mhd, ColumnKeys.Trip],
            cols.Select(c => c.Key));
    }

    /// <summary>Prázdný sloupec se netiskne sám, takže na předloze chybí i u linky, která hodnoty
    /// jen nemá. A dialog sloupců sloupec přidat neumí - nesmí z šablony zmizet.</summary>
    [Fact]
    public void SloupecKteryModelNevidelZustaneZapnuty()
    {
        var cols = AiGuidedScheduleBuilder.BuildLineColumnsFromAi(["StopName"]);

        Assert.Equal(LineTableElement.GetDefaultColumns().Count, cols.Count);
        Assert.All(cols, c => Assert.True(c.Visible));
    }

    [Fact]
    public void NeznamySloupecOdModeluSePreskoci()
    {
        var cols = AiGuidedScheduleBuilder.BuildLineColumnsFromAi(["Platform", "StopName", "StopName"]);

        Assert.Equal(ColumnKeys.StopName, cols[0].Key);
        Assert.Equal(LineTableElement.GetDefaultColumns().Count, cols.Count);
    }

    // ── Rozvržení do sekcí ──────────────────────────────────────────────

    private static TextElement Text(float y, string text = "x") =>
        new() { X = 0, Y = y, Width = 50, Height = 5, Text = text };

    [Fact]
    public void HlavickaTabulkaAPoznamkyJsouKazdaVeSveSekci()
    {
        var nadpis = Text(0, "{CISLO_LINKY}");
        var tabulka = new LineTableElement { X = 0, Y = 20, Width = 200, Height = 100 };
        var poznamky = new NotesLegendElement { X = 0, Y = 125, Width = 200, Height = 20 };
        var paticka = Text(150, "Dopravce");

        var sections = AiVisionImportPipeline.SplitLineScheduleSections([nadpis, tabulka, poznamky, paticka]);

        Assert.Equal(["Hlavička", "Jízdní řád", "Poznámky"], sections.Select(s => s.Name));
        Assert.Equal([nadpis], sections[0].Elements);
        Assert.Equal([tabulka], sections[1].Elements);
        Assert.Equal(0f, tabulka.Y);
        Assert.Equal(0f, poznamky.Y);
        Assert.Equal(25f, paticka.Y);
    }

    [Fact]
    public void PoznamkyVpravoZustanouVedleTabulky()
    {
        var tabulka = new LineTableElement { X = 0, Y = 20, Width = 220, Height = 100 };
        var poznamky = new NotesLegendElement { X = 200, Y = 20, Width = 80, Height = 60 };

        var sections = AiVisionImportPipeline.SplitLineScheduleSections([Text(0), tabulka, poznamky]);

        Assert.Equal(["Hlavička", "Jízdní řád a poznámky"], sections.Select(s => s.Name));
        Assert.Equal([tabulka, poznamky], sections[1].Elements);
        Assert.True(tabulka.X + tabulka.Width <= poznamky.X, "tabulka nesmí zasahovat pod poznámky");
    }

    /// <summary>Poznámky bývají až na druhé stránce předlohy. Bez prvku by se nevytiskly
    /// vysvětlivky ke značkám - kostra je má vždy, kdy přejdou na další stránku, řeší tisk.</summary>
    [Fact]
    public void PoznamkyKtereNaStranceNejsouSeDoplniPodTabulku()
    {
        var tabulka = new LineTableElement { X = 5, Y = 20, Width = 200, Height = 100 };

        var sections = AiVisionImportPipeline.SplitLineScheduleSections([Text(0), tabulka]);

        Assert.Equal(["Hlavička", "Jízdní řád", "Poznámky"], sections.Select(s => s.Name));
        var poznamky = Assert.IsType<NotesLegendElement>(Assert.Single(sections[2].Elements));
        Assert.Equal(tabulka.X, poznamky.X);
        Assert.Equal(tabulka.Width, poznamky.Width);
    }

    /// <summary>Soubor systémové šablony × kde na předloze leží poznámky (pod tabulkou / vedle ní).</summary>
    public static TheoryData<string, float, float> SystemoveKostry => new()
    {
        { "linkovy-jr-ad-postskript-a5.json", 0f, 125f },
        { "linkovy-jr-ad-poznamky-vedle-a4.json", 205f, 20f },
    };

    /// <summary>Kostra importu je táž jako u systémových šablon linkového JŘ - ty mají zákazníci
    /// od vydání agendy a tisk je na nich vyladěný. Rozejde-li se jedno s druhým, padne tohle.</summary>
    [Theory]
    [MemberData(nameof(SystemoveKostry))]
    public void KostraOdpovidaSystemoveSablone(string soubor, float poznamkyX, float poznamkyY)
    {
        var vzor = TemplateDesigner.Serialization.TemplateSerializer.Deserialize(
            File.ReadAllText(Path.Combine(RepoRoot(), "TemplateDesigner", "dotnet", "SystemTemplates", soubor)))!;

        var tabulka = new LineTableElement { X = 0, Y = 20, Width = 200, Height = 100 };
        var poznamky = new NotesLegendElement { X = poznamkyX, Y = poznamkyY, Width = 60, Height = 30 };
        var sections = AiVisionImportPipeline.SplitLineScheduleSections([Text(0), tabulka, poznamky]);

        static object Tvar(Section s) => new
        {
            s.Name, s.Role, s.PrintBehavior, s.OverflowBehavior,
            Prvky = string.Join(",", s.Elements.Where(e => e is LineTableElement or NotesLegendElement)
                                               .Select(e => e.GetType().Name)),
        };
        Assert.Equal(vzor.Sections.Select(Tvar), sections.Select(Tvar));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Kořen repa (EDISON2.sln) nenalezen.");
    }

    /// <summary>Tabulka i poznámky mají výšku podle dat linky - pevná výška z předlohy by u delší
    /// linky písmo zmenšila nebo tabulku ořízla.</summary>
    [Fact]
    public void TabulkaAPoznamkyMajiVyskuZDat()
    {
        var tabulka = new LineTableElement { X = 0, Y = 20, Width = 200, Height = 100 };
        var poznamky = new NotesLegendElement { X = 0, Y = 125, Width = 200, Height = 20 };

        var sections = AiVisionImportPipeline.SplitLineScheduleSections([Text(0), tabulka, poznamky]);

        Assert.Null(tabulka.Height);
        Assert.Null(poznamky.Height);
        Assert.Equal(100f, sections[1].DesignerHeight);
    }

    [Fact]
    public void BezTextuNadTabulkouNevznikneHlavicka()
    {
        var tabulka = new LineTableElement { X = 0, Y = 0, Width = 200, Height = 100 };

        var sections = AiVisionImportPipeline.SplitLineScheduleSections([tabulka]);

        Assert.Equal(["Jízdní řád", "Poznámky"], sections.Select(s => s.Name));
    }

    [Fact]
    public void BezTabulkyZustaneJednaSekce()
    {
        var sections = AiVisionImportPipeline.SplitLineScheduleSections([Text(0), Text(30)]);

        Assert.Single(sections);
        Assert.Equal(2, sections[0].Elements.Count);
    }

    // ── Umístění textů z předlohy ───────────────────────────────────────

    private static ExtractedTextBlock Blok(string text, float x, float y, float w = 30, float h = 3) =>
        new(text, x, y, w, h, "Arial", 8, false, false, "#FF000000");

    /// <summary>PDF drží „Platí od …" a „do …" jako jeden blok, model je vrací po řádcích -
    /// bez rozlišení řádku padly oba texty na totéž místo (zkouška 2. 10.).</summary>
    [Fact]
    public void TextZVicerakovehoBlokuDostaneSvujRadek()
    {
        var blok = Blok("Platí od 11.12.2022\ndo 09.12.2023", 140, 5, h: 6);

        Assert.Equal(0, AiGuidedScheduleBuilder.LineOfBlock(blok, "Platí od 11.12.2022"));
        Assert.Equal(1, AiGuidedScheduleBuilder.LineOfBlock(blok, "do 09.12.2023"));
        Assert.Null(AiGuidedScheduleBuilder.LineOfBlock(blok, "Platí od 11.12.2022 do 09.12.2023"));
        Assert.Null(AiGuidedScheduleBuilder.LineOfBlock(Blok("Linka 940", 0, 0), "Linka 940"));
    }

    /// <summary>Rámeček kolem čísla i názvu linky patří oběma - název se přes něj roztáhl
    /// a překryl číslo (zkouška 2. 10.).</summary>
    [Fact]
    public void RamecekSeDvemaTextyNepatriZadnemuZNich()
    {
        var ramecek = new ExtractedRectangle(0, 4, 200, 9, null, "#FF000000", 0.3f);
        var cislo = Blok("330530", 2, 6, w: 14);
        var nazev = Blok("Kaplice-Velešín", 25, 6, w: 70);

        Assert.Null(AiGuidedScheduleBuilder.FindEnclosingRectangle([ramecek], nazev, [cislo, nazev]));

        var jenKolemCisla = ramecek with { Width = 20 };
        Assert.Equal(jenKolemCisla, AiGuidedScheduleBuilder.FindEnclosingRectangle([jenKolemCisla], cislo, [cislo, nazev]));
    }

    /// <summary>U linkového JŘ model volby sestavy hádal pokaždé jinak - hodnota v šabloně by
    /// přebila nastavení uživatele. Zastávkový JŘ je bere dál.</summary>
    [Fact]
    public void VolbySestavySeZPredlohyBerouJenUZastavkovehoJr()
    {
        Assert.False(AiVisionImportPipeline.TakesReportParameters(ScheduleImportType.LinkovyJr));
        Assert.True(AiVisionImportPipeline.TakesReportParameters(ScheduleImportType.ZastavkovyJrMhd));
    }

    // ── Co musí v odpovědi modelu být ───────────────────────────────────

    private static AiVisionResult Vision(params string[] types) =>
        new() { Elements = [.. types.Select(t => new AiVisionElement { Type = t })] };

    [Fact]
    public void LinkovyJrPotrebujeTabulkuSpoju()
    {
        Assert.Equal("tabulku spojů",
            AiVisionImportPipeline.MissingCoreElements(Vision("NotesLegend"), ScheduleImportType.LinkovyJr));
        Assert.Equal("",
            AiVisionImportPipeline.MissingCoreElements(Vision("LineTable"), ScheduleImportType.LinkovyJr));
    }

    [Fact]
    public void ZastavkovyJrDalPotrebujeZastavkyIOdjezdy()
    {
        Assert.Equal("seznam zastávek a mřížku odjezdů",
            AiVisionImportPipeline.MissingCoreElements(Vision("LineTable"), ScheduleImportType.ZastavkovyJrMhd));
    }
}
