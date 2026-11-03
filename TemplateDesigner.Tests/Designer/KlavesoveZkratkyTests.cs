using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using TemplateDesigner.Designer;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Designer;

/// <summary>
/// Zkratky návrháře žijí v jedné tabulce (<see cref="KeyboardShortcuts"/>). Menu, bubliny, nápověda F1 a asistent
/// z ní berou text, obsluha kláves s ní porovnává. Do 30. 10. byly opsané na sedmi místech a rozcházely se
/// (Del × Delete, PgUp × PageUp, popis Ctrl+D + šipka v F1). Texty, které zkratku zmiňují ve větě (asistent, hlášky,
/// příručka, pravidla), se z tabulky skládat nedají - tady se hlídá, že neznají zkratku, kterou aplikace nemá.
/// </summary>
public class KlavesoveZkratkyTests
{
    // ── Tabulka ─────────────────────────────────────────────────────────────

    [Fact]
    public void Shoda_JePresna()
    {
        Assert.True(KeyboardShortcuts.Undo.Matches(Key.Z, ModifierKeys.Control));
        Assert.False(KeyboardShortcuts.Undo.Matches(Key.Z, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.True(KeyboardShortcuts.BringToFront.Matches(Key.PageUp, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.False(KeyboardShortcuts.BringForward.Matches(Key.PageUp, ModifierKeys.Control | ModifierKeys.Shift));
        // AltGr hlásí Windows jako Ctrl+Alt - psaní „{" nesmí sbalit levý panel.
        Assert.False(KeyboardShortcuts.ToggleLeftPanel.Matches(Key.B, ModifierKeys.Control | ModifierKeys.Alt));
        // Gesto myši klávesou spustit nejde.
        Assert.False(KeyboardShortcuts.AddToSelection.Matches(Key.None, ModifierKeys.None));
    }

    [Theory]
    [InlineData(nameof(KeyboardShortcuts.BringForward), "Ctrl+PageUp")]
    [InlineData(nameof(KeyboardShortcuts.SendToBack), "Ctrl+Shift+PageDown")]
    [InlineData(nameof(KeyboardShortcuts.Delete), "Delete")]
    [InlineData(nameof(KeyboardShortcuts.FitToPage), "Ctrl+0")]
    [InlineData(nameof(KeyboardShortcuts.MoveSectionUp), "Ctrl+Shift+↑")]
    [InlineData(nameof(KeyboardShortcuts.CellProperties), "Alt+Enter")]
    [InlineData(nameof(KeyboardShortcuts.Cancel), "Esc")]
    public void TextSeSkladaZKlavesy(string jmeno, string ocekavany) =>
        Assert.Equal(ocekavany, KeyboardShortcuts.Get(jmeno).Gesture);

    /// <summary>Dvě akce na téže klávese v témže místě by obsluha rozlišila jen pořadím podmínek.</summary>
    [Fact]
    public void ZkratkyVTemzeMisteSeNeprekryvaji()
    {
        string[] okno = [KeyboardShortcuts.Soubor, KeyboardShortcuts.Upravy, KeyboardShortcuts.Vyber,
                         KeyboardShortcuts.Sekce, KeyboardShortcuts.Zobrazeni];
        var kolize = KeyboardShortcuts.All
            .Where(z => z.Key != Key.None)
            .GroupBy(z => (Misto: okno.Contains(z.Category) ? "okno" : z.Category, z.Key, z.Modifiers))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.First().Gesture}: {string.Join(", ", g.Select(z => z.Action))}")
            .ToList();

        Assert.True(kolize.Count == 0, "Jedna klávesa, víc akcí:\n" + string.Join("\n", kolize));
    }

    [Fact]
    public void NeznameJmenoJeChyba() =>
        Assert.Throws<ArgumentException>(() => KeyboardShortcuts.Get("Neexistuje"));

    /// <summary>Okno s neznámým jménem by spadlo až při otevření.</summary>
    [Fact]
    public void JmenaVOknechExistuji()
    {
        var jmena = SouboryNavrhare("*.xaml")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\{\w+:(?:Gesture|Tip|Action)\s+(\w+)")
                .Select(m => m.Groups[1].Value))
            .ToList();

        Assert.True(jmena.Count > 20, $"v oknech se našlo jen {jmena.Count} zkratek");
        foreach (var jmeno in jmena)
            KeyboardShortcuts.Get(jmeno);
    }

    [Fact]
    public void PruvodceZnaVsechnyZkratky()
    {
        var seznam = CapabilityCatalog.BuildShortcutList();
        foreach (var zkratka in KeyboardShortcuts.All)
            Assert.Contains($"- {zkratka.Gesture} - {zkratka.Action}", seznam);
    }

    // ── Texty, které zkratku zmiňují ve větě ────────────────────────────────

    [Fact]
    public void TextyNavrhare_ZnajiJenZkratkyZTabulky() =>
        Over(TextyNavrhare(), "textech návrháře");

    [Fact]
    public void PriruckaAPravidla_ZnajiJenZkratkyZTabulky() =>
        Over(Prirucka().Concat(PravidlaUx()).ToList(), "příručce nebo v ux-conventions §3");

    /// <summary>Hledání musí zkratky opravdu najít - jinak by kontroly výš procházely nad prázdným seznamem.</summary>
    [Fact]
    public void Hledani_NajdeZkratky()
    {
        Assert.Contains(TextyNavrhare(), t => t.Zkratka == "Ctrl+Shift+A");
        Assert.Contains(Prirucka(), t => t.Zkratka == "Ctrl+kolečko");
        Assert.Contains(PravidlaUx(), t => t.Zkratka == "Ctrl+PageUp");
    }

    private static void Over(List<(string Kde, string Zkratka)> nalezene, string kde)
    {
        var zname = KeyboardShortcuts.All.SelectMany(z => Zkratky(z.Gesture)).Concat(ZkratkyOknaSablon()).ToHashSet();
        var nezname = nalezene.Where(n => !zname.Contains(n.Zkratka)).Select(n => $"{n.Kde}: {n.Zkratka}").ToList();

        Assert.True(nezname.Count == 0,
            $"Zkratka v {kde}, kterou KeyboardShortcuts nezná - oprav text, nebo zkratku doplň do tabulky:\n" +
            string.Join("\n", nezname));
    }

    /// <summary>Akce okna šablon (Nový / Upravit / Odstranit) mají zkratky seznamů EDISONu ze sdílených komponent,
    /// ne z tabulky hlavního okna; bere je z okna samotného, ať příručka nezná zkratku, kterou okno nemá.</summary>
    private static IEnumerable<string> ZkratkyOknaSablon()
    {
        EDControls.EDViewModel.EDServices.Initialize(new EDControls.EDViewModel.EDServices(
            NSubstitute.Substitute.For<EDControls.IMLRest>(),
            NSubstitute.Substitute.For<EDControls.EDViewModel.IWindowNavigator>(),
            NSubstitute.Substitute.For<EDControls.EDViewModel.IPopupDisplayer>()));
        return new CacheTemplatePickerDialogViewModel(null!).Actions!.Select(a => a.ShortcutText);
    }

    /// <summary>Zkratka s modifikátorem: „Ctrl+Shift+P", „Alt+klik", „Ctrl + kolečko". Samotné klávesy (F2, Enter)
    /// jsou i běžná slova a formáty čísel, proto se nehledají.</summary>
    private static readonly Regex Zkratka = new(
        @"(?<![\p{L}\d+])(?:(?:Ctrl|Shift|Alt)\s?\+\s?)+[^\s,;:.)\]/""“„”<>*|(=]+", RegexOptions.Compiled);

    private static IEnumerable<string> Zkratky(string text) =>
        Zkratka.Matches(text).Select(m => Tvar.Replace(Regex.Replace(m.Value, @"\s", ""), t => t.Groups[1].Value switch
        {
            "klik" => "klik",
            "kolečk" => "kolečko",
            _ => "šipky",
        }));

    /// <summary>Gesto myši ve větě se skloňuje („Alt+klikem").</summary>
    private static readonly Regex Tvar = new(@"(?<=\+)(klik|kolečk|šip)\w*$", RegexOptions.Compiled);

    /// <summary>Texty v kódu a oknech návrháře mimo komentáře.</summary>
    private static List<(string Kde, string Zkratka)> TextyNavrhare()
    {
        var kod = SouboryNavrhare("*.cs")
            .SelectMany(f => File.ReadLines(f).Select((radek, i) => (f, radek, i)))
            .Where(x => !x.radek.TrimStart().StartsWith("//", StringComparison.Ordinal)
                        && !x.radek.TrimStart().StartsWith('*'))
            .SelectMany(x => Zkratky(BezKomentare(x.radek)).Select(z => (Kde: $"{Path.GetFileName(x.f)}:{x.i + 1}", Zkratka: z)));

        var okna = SouboryNavrhare("*.xaml")
            .SelectMany(f => Zkratky(Regex.Replace(File.ReadAllText(f), "<!--.*?-->", "", RegexOptions.Singleline))
                .Select(z => (Kde: Path.GetFileName(f), Zkratka: z)));

        return [.. kod, .. okna];
    }

    /// <summary>Celá uživatelská příručka - zkratky zmiňuje i mimo kapitolu 12.</summary>
    private static List<(string Kde, string Zkratka)> Prirucka() =>
        [.. File.ReadLines(Path.Combine(Koren(), "TemplateDesigner", ".claude", "docs", "uzivatelska-prirucka.md"))
            .Select((radek, i) => (radek, i))
            .SelectMany(x => Zkratky(x.radek).Select(z => (Kde: $"uzivatelska-prirucka.md:{x.i + 1}", Zkratka: z)))];

    /// <summary>Řádky tabulek ux-conventions §3. Text kolem nich zmiňuje i zkratky, které vědomě nejsou
    /// (zrušené Ctrl+Shift+S, standardní Alt+F4).</summary>
    private static List<(string Kde, string Zkratka)> PravidlaUx()
    {
        var radky = File.ReadAllLines(Path.Combine(Koren(), "TemplateDesigner", ".claude", "rules", "ux-conventions.md"));
        int od = Array.FindIndex(radky, r => r.StartsWith("## 3.", StringComparison.Ordinal));
        int po = Array.FindIndex(radky, od + 1, r => r.StartsWith("## 4.", StringComparison.Ordinal));
        return [.. Enumerable.Range(od, po - od)
            .Where(i => radky[i].StartsWith('|'))
            .SelectMany(i => Zkratky(radky[i]).Select(z => (Kde: $"ux-conventions.md:{i + 1}", Zkratka: z)))];
    }

    /// <summary>Řádek bez komentáře na konci; „//" uvnitř textu (adresa) komentář nezačíná.</summary>
    private static string BezKomentare(string radek)
    {
        bool vTextu = false;
        for (int i = 0; i < radek.Length - 1; i++)
        {
            if (vTextu && radek[i] == '\\') { i++; continue; }
            if (radek[i] == '"') vTextu = !vTextu;
            else if (!vTextu && radek[i] == '/' && radek[i + 1] == '/') return radek[..i];
        }
        return radek;
    }

    private static IEnumerable<string> SouboryNavrhare(string maska) =>
        Directory.GetFiles(Path.Combine(Koren(), "TemplateDesigner", "dotnet"), maska, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    private static string Koren()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;
        return dir!.FullName;
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
