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
    [InlineData("Nadpis popisných sloupců")]
    [InlineData("Podbarvit lichá Tč")]
    public void NaViditelneVolbyTabulkySeModelPta(string popisek)
    {
        Assert.Contains(popisek, ElementSettingsFromImage.BuildCatalog(new LineTableElement()));
    }

    /// <summary>Tyhle řádky na předloze chybí i tehdy, když linka jen nemá platnost, značky nebo
    /// přípoje - vypnout je podle obrázku by je schovalo lince, která je má. Tučné zastávky a čáry
    /// u zastávek jsou údaj z trasy linky, ne vzhled.</summary>
    [Theory]
    [InlineData("Řádek platnosti")]
    [InlineData("Řádek negativních značek")]
    [InlineData("Řádky · Přípojné řádky")]
    [InlineData("Tučné zastávky dle trasy")]
    [InlineData("Čáry u zastávek dle trasy")]
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

        Assert.Equal(3, sections.Count);

        Assert.Equal(SectionRole.Header, sections[0].Role);
        Assert.Equal(SectionPrintBehavior.OnEveryPage, sections[0].PrintBehavior);
        Assert.Equal([nadpis], sections[0].Elements);

        Assert.Equal(SectionOverflowBehavior.SplitAcrossPages, sections[1].OverflowBehavior);
        Assert.Equal([tabulka], sections[1].Elements);
        Assert.Equal(0f, tabulka.Y);

        Assert.Equal(SectionOverflowBehavior.SplitAcrossPages, sections[2].OverflowBehavior);
        Assert.Equal(0f, poznamky.Y);
        Assert.Equal(25f, paticka.Y);
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

        Assert.Single(sections);
        Assert.Equal(SectionRole.Body, sections[0].Role);
    }

    [Fact]
    public void BezTabulkyZustaneJednaSekce()
    {
        var sections = AiVisionImportPipeline.SplitLineScheduleSections([Text(0), Text(30)]);

        Assert.Single(sections);
        Assert.Equal(2, sections[0].Elements.Count);
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
