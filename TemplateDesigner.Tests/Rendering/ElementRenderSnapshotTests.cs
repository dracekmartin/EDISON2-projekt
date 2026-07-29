using System.IO;
using System.Text;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Odtisky kreslení pěti tabulkových elementů - **migrační důkaz** pro refaktory kreslicí cesty
/// (row-plan vrstva / R1 extrakce). Použití:
///
/// 1. Před zásahem: <c>dotnet test --filter ElementRenderSnapshotTests</c> → zapíše odtisky do
///    souboru mimo repozitář (cesta v <see cref="BaselinePath"/>, vypsaná i v hlášce testu).
/// 2. Po zásahu: spustit znovu - test porovná proti tomu souboru a řekne, který element se rozešel.
/// 3. Po dokončení refaktoru soubor smazat (nebo přepsat novým baseline, pokud je změna zamýšlená).
///
/// ⚠️ Odtisky jsou **vázané na stroj** (projekt nemá zabalené textové fonty → text kreslí Arial
/// z OS), proto baseline **není v gitu** a test **neselže, když soubor chybí** - jen ho vytvoří.
/// Trvalou pixelovou regresní sadu by šlo mít až se zabalenými textovými fonty.
///
/// 💡 Poučení z prvního použití (29. 7.): baseline se dá omylem sejmout **proti zastaralé binárce** -
/// když je spuštěný designer, build testů selže na uzamčeném `TemplateDesigner.exe` a testy běží
/// proti staré DLL. Před snímáním baseline **zavři aplikaci** a ověř, že build proběhl.
/// Přesnímání: <c>TD_RENDER_REBASELINE=1</c>.
/// </summary>
public class ElementRenderSnapshotTests
{
    private static string BaselinePath => Path.Combine(
        Path.GetTempPath(), "td-render-baseline.txt");

    private static (string Label, string Hash)[] CaptureAll()
    {
        var stopData = ElementRenderFixtures.StopScheduleData();
        var lineData = ElementRenderFixtures.LineTableData();
        var dynData  = ElementRenderFixtures.DynamicTableData();

        return
        [
            ("StopList",     RenderSnapshot.Capture(ElementRenderFixtures.StopList(), stopData)),
            ("DepartureGrid",RenderSnapshot.Capture(ElementRenderFixtures.DepartureGrid(), stopData)),
            ("NotesLegend",  RenderSnapshot.Capture(ElementRenderFixtures.NotesLegend(), stopData)),
            ("LineTable",    RenderSnapshot.Capture(ElementRenderFixtures.LineTable(), lineData)),
            ("DynamicTable", RenderSnapshot.Capture(ElementRenderFixtures.DynamicTable(), dynData)),
        ];
    }

    /// <summary>Harness musí být deterministický - jinak by odtisky nic nedokazovaly.
    /// Tenhle test je jediný, který má smysl držet v gitu (nezávisí na konkrétních hodnotách).</summary>
    [Fact]
    public void Odtisky_JsouDeterministicke()
    {
        var first = CaptureAll();
        var second = CaptureAll();

        Assert.Equal(first, second);
        Assert.All(first, x => Assert.NotEmpty(x.Hash));
    }

    /// <summary>Neprázdné kreslení - fixtura, která nic nenakreslí, by odtiskem prošla vždy.</summary>
    [Fact]
    public void KazdyElement_NecoNakresli()
    {
        var blank = RenderSnapshot.Capture(ElementRenderFixtures.DynamicTable(), data: null);

        var drawn = CaptureAll();

        // Prázdný canvas (bez dat) má jiný odtisk než kterýkoli element s daty.
        Assert.All(drawn, x => Assert.NotEqual(blank, x.Hash));
        Assert.Equal(drawn.Length, drawn.Select(x => x.Hash).Distinct().Count());
    }

    /// <summary>Citlivost harnessu: posun o 0,1 mm (nejmenší jednotka, se kterou návrhář pracuje)
    /// **musí** změnit odtisk - jinak by odtisky nedokázaly, že refaktor nic neposunul.
    /// Nezávislé na stroji (porovnává dva odtisky z téhož běhu), proto patří do gitu.</summary>
    [Fact]
    public void Odtisk_ZachytiPosunODesetinuMilimetru()
    {
        var data = ElementRenderFixtures.StopScheduleData();

        var original = ElementRenderFixtures.StopList();
        var nudged = ElementRenderFixtures.StopList();
        nudged.Y += 0.1f;

        Assert.NotEqual(RenderSnapshot.Capture(original, data), RenderSnapshot.Capture(nudged, data));
    }

    /// <summary>Citlivost na styl: o půl bodu jiná velikost písma se musí projevit.</summary>
    [Fact]
    public void Odtisk_ZachytiZmenuVelikostiPisma()
    {
        var data = ElementRenderFixtures.LineTableData();

        var original = ElementRenderFixtures.LineTable();
        var bigger = ElementRenderFixtures.LineTable();
        bigger.DataStyle = new TemplateDesigner.Models.TextStyle
        {
            FontFamily = original.DataStyle.FontFamily,
            FontSize = (original.DataStyle.FontSize ?? 6f) + 0.5f,
        };

        Assert.NotEqual(RenderSnapshot.Capture(original, data), RenderSnapshot.Capture(bigger, data));
    }

    /// <summary>Porovnání proti baseline souboru mimo repozitář (viz dokumentace třídy).
    /// Chybí-li soubor, test ho vytvoří a projde - první běh = sejmutí baseline.</summary>
    [Fact]
    public void Odtisky_SouhlasiSBaseline()
    {
        var current = CaptureAll();
        var text = string.Join("\n", current.Select(x => $"{x.Label} = {x.Hash}"));

        // Přesnímání na vyžádání: TD_RENDER_REBASELINE=1. Nutné i po zamýšlené změně kreslení
        // (a po buildu proti zastaralé binárce - viz poučení níže).
        bool rebaseline = Environment.GetEnvironmentVariable("TD_RENDER_REBASELINE") == "1";
        if (rebaseline || !File.Exists(BaselinePath))
        {
            File.WriteAllText(BaselinePath, text, new UTF8Encoding(false));
            return;   // baseline sejmuta, není proti čemu porovnávat
        }

        var baseline = File.ReadAllText(BaselinePath, Encoding.UTF8)
            .TrimStart('﻿').Replace("\r\n", "\n").Trim();

        Assert.True(baseline == text,
            $"Kreslení se rozešlo proti baseline ({BaselinePath}).\n"
            + $"--- baseline ---\n{baseline}\n--- nyní ---\n{text}\n"
            + "Pokud je změna zamýšlená, baseline soubor smaž a sejmi znovu.");
    }
}
