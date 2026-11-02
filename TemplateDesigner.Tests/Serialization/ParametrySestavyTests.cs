using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Jména parametrů sestavy a zamčené popisky voleb, které kód návrháře čte nebo porovnává, jsou v jedné třídě
/// <see cref="ParametrySestavy"/>. Agenda je musí opravdu posílat - přejmenovaný parametr nebo
/// volba by na klientu tiše spadla na výchozí chování - a kód je nesmí psát napevno jinde, jinak by se zase rozešly.
/// </summary>
public class ParametrySestavyTests
{
    /// <summary>Soubory, které hodnoty smějí mít napevno: kontrakt dat se linkuje do integračních testů bez návrháře,
    /// zadání pro AI je text pro model, ne čtení parametru.</summary>
    private static readonly HashSet<string> Vyjimky = ["ParametrySestavy.cs", "KontraktDat.cs", "AiVisionImportService.cs"];

    /// <summary>Stejné slovo v jiném významu: soubor|text → důvod.</summary>
    private static readonly Dictionary<string, string> JinyVyznam = new(StringComparer.Ordinal)
    {
        ["StopListElement.cs|Sloupek"] = "záhlaví sloupce Sloupek v Seznamu zastávek, ne volba „Číslo stanoviště podle\"",
    };

    private static IReadOnlyList<(string Jmeno, string Hodnota)> Hodnoty() =>
        [.. typeof(ParametrySestavy).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (f.Name, (string)f.GetRawConstantValue()!))];

    [Fact]
    public void AgendyHodnotyPosilaji()
    {
        var agendy = string.Join("\n", Directory.GetFiles(RepoCesty.Cache("Agendy"), "*.cls", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

        var chybi = Hodnoty().Where(h => !agendy.Contains($"\"{h.Hodnota}\"", StringComparison.Ordinal))
            .Select(h => $"{h.Jmeno} = „{h.Hodnota}\"").ToList();

        Assert.True(Hodnoty().Count > 15, "ParametrySestavy nemá skoro žádné hodnoty - hledání je rozbité");
        Assert.True(chybi.Count == 0,
            "Parametr nebo volbu z ParametrySestavy žádná agenda neposílá (přejmenovaná na serveru?):\n" + string.Join("\n", chybi));
    }

    [Fact]
    public void KodHodnotyNepiseNapevno()
    {
        var hodnoty = Hodnoty().Select(h => h.Hodnota).ToHashSet(StringComparer.Ordinal);
        var nalezy = Directory.GetFiles(RepoCesty.Dotnet(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !Vyjimky.Contains(Path.GetFileName(f)))
            .SelectMany(f => File.ReadLines(f).Select((radek, i) => (f, radek, i)))
            .Where(x => !x.radek.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .SelectMany(x => Regex.Matches(x.radek, @"""((?:[^""\\]|\\.)*)""")
                .Where(m => hodnoty.Contains(m.Groups[1].Value)
                            && !JinyVyznam.ContainsKey($"{Path.GetFileName(x.f)}|{m.Groups[1].Value}"))
                .Select(m => $"{Path.GetFileName(x.f)}:{x.i + 1}: „{m.Groups[1].Value}\""))
            .ToList();

        Assert.True(nalezy.Count == 0,
            "Jméno parametru nebo zamčený popisek volby napevno - použij konstantu z ParametrySestavy:\n" + string.Join("\n", nalezy));
    }
}
