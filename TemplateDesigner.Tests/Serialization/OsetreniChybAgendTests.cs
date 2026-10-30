using System.IO;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Každá metoda agendy volaná návrhářem (<c>ByRef sys As %String) As %Status</c>) dodržuje vzor
/// z <c>cache-api.md</c> (pravidla 14-15): <c>s err = ""</c>, <c>try</c> / <c>catch</c> s <c>$$$CatchErrRest</c>
/// a <c>ret $$$OK</c>. Bez <c>catch</c> chyba agendy neskončí v <c>^||ML("ERR")</c> a uživatel místo hlášky
/// dostane chybu spojení. Smí ještě jen přesměrovat na metodu jiné agendy (<c>q ##class(…).X(.sys)</c>),
/// ta vzor dodržuje sama. Do 30. 10. vzor nedodržovalo schéma a parametry všech agend JŘ i výchozí
/// metody <c>BaseAgenda</c> (<c>B194g</c> R6).
/// </summary>
public class OsetreniChybAgendTests
{
    /// <summary>Třídy mimo kontrolu, s důvodem. Dnes žádná (ukázkové Faktury smazané 30. 10.).</summary>
    private static readonly Dictionary<string, string> Vyjimky = new(StringComparer.Ordinal);

    private static readonly Regex Metoda = new(
        @"^ClassMethod (?<jmeno>\w+)\(ByRef sys As %String\) As %Status(?<abstraktni> \[ Abstract \])?\s*\n\{(?<telo>.*?)\n\}",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
    private static readonly Regex Presmerovani = new(
        @"^(q|quit|ret|return) ##class\([\w.]+\)\.\w+\(\.sys\)$", RegexOptions.Compiled);
    private static readonly Regex Zacatek = new(@"^(s|set) err = """"$", RegexOptions.Compiled);
    private static readonly Regex Konec = new(@"^(ret|return|q|quit) \$\$\$OK$", RegexOptions.Compiled);
    private static readonly Regex KomentarNaKonci = new(@"\s+//.*$", RegexOptions.Compiled);

    [Fact]
    public void MetodyAgendDodrzujiVzorOsetreniChyb()
    {
        var porusuji = Metody()
            .Where(m => !Vyjimky.ContainsKey(m.Soubor) && !DodrzujeVzor(m.Radky))
            .Select(m => $"{m.Soubor} {m.Jmeno}")
            .ToList();

        Assert.True(porusuji.Count == 0,
            "Metoda agendy nedodržuje vzor ošetření chyb (cache-api.md pravidla 14-15: s err = \"\", try/catch " +
            "s $$$CatchErrRest, ret $$$OK) - vzor viz BaseAgenda.GetParameters:\n" + string.Join("\n", porusuji));
    }

    /// <summary>Hledání musí najít metody všech agend JŘ, jinak by kontrola výš prošla naprázdno.</summary>
    [Fact]
    public void Hledani_NajdeMetodyAgend()
    {
        var metody = Metody();
        Assert.True(metody.Count > 30, $"našlo se jen {metody.Count} metod");
        foreach (var agenda in new[] { "BaseAgenda.cls", "ZastavkoveJrAd.cls", "ZastavkoveJrMhd.cls", "LinkovyJrAd.cls", "LinkovyJrMhd.cls" })
            Assert.Contains(metody, m => m.Soubor == agenda && m.Jmeno == "GetParameters");
    }

    private static bool DodrzujeVzor(List<string> radky) =>
        radky.Count == 1 && Presmerovani.IsMatch(radky[0])
        || radky.Count > 0 && Zacatek.IsMatch(radky[0]) && radky.Any(r => r.Contains("$$$CatchErrRest")) && Konec.IsMatch(radky[^1]);

    /// <summary>Metody s <c>ByRef sys</c> ze všech agend (bez abstraktních): soubor, jméno, řádky bez komentářů.</summary>
    private static List<(string Soubor, string Jmeno, List<string> Radky)> Metody() =>
        ZdrojeAgend.KodAgend()
            .SelectMany(f => Metoda.Matches(File.ReadAllText(f).Replace("\r\n", "\n"))
                .Where(m => !m.Groups["abstraktni"].Success)
                .Select(m => (Path.GetFileName(f), m.Groups["jmeno"].Value,
                    m.Groups["telo"].Value.Split('\n').Select(r => KomentarNaKonci.Replace(r, "").Trim())
                        .Where(r => r.Length > 0 && !r.StartsWith("//", StringComparison.Ordinal)).ToList())))
            .ToList();
}
