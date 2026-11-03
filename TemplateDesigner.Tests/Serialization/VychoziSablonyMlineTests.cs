using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Výchozí šablona M-line pro tisk (parametr <c>VychoziSablonaMline</c> třídy agendy) musí být mezi dodávanými
/// šablonami (<c>SystemTemplates/</c>) a patřit téže agendě. Překlep v Id by tisk z okna jízdních řádů tiše nechal
/// bez šablony, protože do MLINE větve ji nalévá jen návrhář. Od 3. 11. ji má každá agenda jízdních řádů.
/// </summary>
public class VychoziSablonyMlineTests
{
    [Fact]
    public void VychoziSablonaMline_JeDodavanaSablonaTeAgendy()
    {
        var dodavane = Directory.GetFiles(Path.Combine(RepoRoot(), "TemplateDesigner", "dotnet", "SystemTemplates"), "*.json")
            .Select(f => JsonNode.Parse(File.ReadAllText(f))!)
            .ToDictionary(t => t["id"]!.ToString(), t => t["agendaCode"]?.ToString() ?? "");

        var agendy = AgendyJr().ToList();
        Assert.Equal(4, agendy.Count);
        foreach (var (trida, kod, vychozi) in agendy)
        {
            Assert.False(string.IsNullOrEmpty(vychozi), $"{trida}: agenda jízdních řádů bez výchozí šablony M-line");
            Assert.True(dodavane.TryGetValue(vychozi!, out var agendaSablony), $"{trida}: šablona {vychozi} není v SystemTemplates");
            Assert.Equal(kod, agendaSablony);
        }
    }

    /// <summary>Zveřejněné agendy jízdních řádů (třída, AgendaVariantCode, VychoziSablonaMline) ze zdrojáků.</summary>
    private static IEnumerable<(string Trida, string Kod, string? Vychozi)> AgendyJr()
    {
        var slozka = Path.Combine(RepoRoot(), "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "Agendy");
        foreach (var soubor in Directory.GetFiles(slozka, "*.cls"))
        {
            var text = File.ReadAllText(soubor);
            if (Parametr(text, "OblastPrace") != "JIZDNI_RADY" || Parametr(text, "Released") != "1") continue;
            yield return (Path.GetFileNameWithoutExtension(soubor), Parametr(text, "AgendaVariantCode") ?? "",
                Parametr(text, "VychoziSablonaMline"));
        }
    }

    private static string? Parametr(string text, string jmeno)
    {
        var m = Regex.Match(text, $@"^Parameter {jmeno}(?: As %\w+)? = ""?([^"";\r\n]*)""?;", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln"))) dir = dir.Parent;
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;
        return dir.FullName;
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
