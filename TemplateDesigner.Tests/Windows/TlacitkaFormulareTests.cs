using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using EDControls.EDInputForm;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Tlačítko ve formuláři prvku funguje v každém okně, které ho ukazuje (<c>B194f</c> G5). Formulář tlačítko
/// jen ukáže a akci si každé okno píše samo, takže do 30. 10. ukazovalo okno buňky (Alt+Enter) tlačítko
/// „Nastavení celé tabulky", na které nic nereagovalo. Okno vlastností teď tlačítko bez akce skryje;
/// panel a vlastní okna tabulek hlídají testy níž.
/// </summary>
public class TlacitkaFormulareTests
{
    private static readonly Regex TlacitkoVeFormulari = new(
        @"""(__\w+)"",\s*""[^""]*"",\s*\$\$\$FieldTypeButton", RegexOptions.Compiled);

    /// <summary>Formulář prvku → jeho vlastní okno, které tlačítka formuláře obsluhuje samo
    /// (ostatní prvky ukazuje okno vlastností, které tlačítko bez akce skryje).</summary>
    private static readonly (string Formular, string Okno)[] VlastniOkna =
    [
        ("StopListElementEdit.cls", "StopColumnsEditorDialog.xaml.cs"),
        ("DynamicTableElementEdit.cls", "DynamicTableEditorDialog.xaml.cs"),
        ("LineTableElementEdit.cls", "LineTableColumnsEditorDialog.xaml.cs"),
    ];

    [Fact]
    public void TlacitkoBezAkce_OknoSkryje_OstatniPoleNecha()
    {
        var sAkci = new Field { Key = TlacitkaFormulare.DruhyPoznamek, FieldType = FieldType.Button };
        var bezAkce = new Field { Key = TlacitkaFormulare.NastaveniCeleTabulky, FieldType = FieldType.Button };
        var pole = new Field { Key = "Format" };
        var skrytePole = new Field { Key = "Skryte", Visibility = ItemVisibility.Collapsed };

        ElementFormBinder.SkrytTlacitkaBezAkce([sAkci, bezAkce, pole, skrytePole], [TlacitkaFormulare.DruhyPoznamek]);

        Assert.Equal(ItemVisibility.Visible, sAkci.Visibility);
        Assert.Equal(ItemVisibility.Collapsed, bezAkce.Visibility);
        Assert.Equal(ItemVisibility.Visible, pole.Visibility);
        Assert.Equal(ItemVisibility.Collapsed, skrytePole.Visibility);
    }

    [Fact]
    public void KazdeTlacitkoFormulare_MaKlicVTlacitkaFormulare()
    {
        var veFormularich = TlacitkaFormularu().SelectMany(t => t.Value).ToHashSet();
        var klice = Klice().Values.ToHashSet();

        Assert.True(veFormularich.SetEquals(klice),
            $"Tlačítka ve formulářích ({string.Join(", ", veFormularich.Order())}) a klíče v TlacitkaFormulare " +
            $"({string.Join(", ", klice.Order())}) se rozešly - nové tlačítko patří do TlacitkaFormulare a do obsluhy oken.");
    }

    /// <summary>Panel vlastností ukazuje formulář každého prvku i buňky, musí tedy obsloužit všechna tlačítka.</summary>
    [Fact]
    public void Panel_ObsluhujeVsechnaTlacitka()
    {
        // Okno je rozdělené do částí MainWindow.*.cs (30. 10.), obsluha může být v kterékoli.
        var kod = string.Concat(Directory.GetFiles(Path.Combine(Dotnet(), "Windows", "MainWindow"), "MainWindow*.cs")
            .Order(StringComparer.Ordinal).Select(File.ReadAllText));
        int od = kod.IndexOf("void OnInlineElementButtonClicked(", StringComparison.Ordinal);
        Assert.True(od >= 0, "obsluha tlačítek panelu nenalezena");
        int konec = kod.IndexOf("\n    private ", od, StringComparison.Ordinal);
        var obsluha = kod[od..(konec < 0 ? kod.Length : konec)];

        var chybi = Klice().Keys.Where(jmeno => !obsluha.Contains($"TlacitkaFormulare.{jmeno}", StringComparison.Ordinal)).ToList();

        Assert.True(chybi.Count == 0, "Panel neobsluhuje tlačítka: " + string.Join(", ", chybi));
    }

    [Fact]
    public void VlastniOknaTabulek_ObsluhujiTlacitkaSvehoFormulare()
    {
        var formulare = TlacitkaFormularu();
        var jmena = Klice().ToDictionary(k => k.Value, k => k.Key);
        var chybi = new List<string>();
        foreach (var (formular, okno) in VlastniOkna)
        {
            var soubor = Directory.GetFiles(Path.Combine(Dotnet(), "Windows"), okno, SearchOption.AllDirectories).Single();
            var kod = File.ReadAllText(soubor);
            foreach (var klic in formulare.GetValueOrDefault(formular, []))
                if (!kod.Contains($"TlacitkaFormulare.{jmena[klic]}", StringComparison.Ordinal))
                    chybi.Add($"{okno} neobsluhuje {klic} z {formular}");
        }

        Assert.True(chybi.Count == 0, string.Join("\n", chybi));
    }

    /// <summary>Tlačítka podle formuláře: soubor <c>*.cls</c> → klíče tlačítek.</summary>
    private static Dictionary<string, List<string>> TlacitkaFormularu() =>
        Directory.GetFiles(Path.Combine(Koren(), "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "EDInputForm"), "*.cls")
            .Select(f => (Soubor: Path.GetFileName(f), Klice: TlacitkoVeFormulari.Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value).ToList()))
            .Where(x => x.Klice.Count > 0)
            .ToDictionary(x => x.Soubor, x => x.Klice);

    /// <summary>Konstanty <see cref="TlacitkaFormulare"/>: jméno → klíč.</summary>
    private static Dictionary<string, string> Klice() =>
        typeof(TlacitkaFormulare).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);

    private static string Dotnet() => Path.Combine(Koren(), "TemplateDesigner", "dotnet");

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
