using System.IO;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Hodnoty, které agenda posílá do dat stránky, vidí uživatel na plátně i v tisku. Nesmí v nich být
/// interní věci databáze: název globálu (<c>^JRL</c>) ani kód firmy či jmenného prostoru
/// (<c>dataDB</c>, <c>IdFa</c>). Do 27. 10. ukazoval náhled bez linky „(žádná linka v ^JRL pro firmu
/// MLINE)" (`B194` D8).
///
/// <para>⚠️ Kontroluje zdroj v repu, ne nasazenou třídu.</para>
/// </summary>
public class AgendaUserTextTests
{
    // d obj.%Set("KLIC", hodnota) - hodnota až do konce řádku
    private static readonly Regex SetRx = new(@"%Set\(""[A-Z_0-9]+"",\s*(?<val>.+)$", RegexOptions.Compiled);

    // kód firmy / jmenného prostoru přilepený do textu: "…"_dataDB, IdFa_"…". Ne \b - podtržítko
    // (spojení textů) je pro regex písmeno, takže "_IdFa_" by hranici slova neměl.
    private static readonly Regex KodFirmyRx = new(
        @"(_\s*(dataDB|IdFa)(?![A-Za-z0-9]))|((?<![A-Za-z0-9])(dataDB|IdFa)\s*_)", RegexOptions.Compiled);

    // název globálu uvnitř textového literálu: "… ^JRL …"
    private static readonly Regex GlobalRx = new(@"""[^""]*\^[A-Z][^""]*""", RegexOptions.Compiled);

    [Fact]
    public void HodnotyStrankyNeukazujiGlobalyAniKodFirmy()
    {
        var nalezy = new List<string>();
        foreach (var file in ZdrojeAgend.KodAgend())
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimStart();
                if (line.StartsWith("//") || line.StartsWith("///")) continue;
                var m = SetRx.Match(line);
                if (!m.Success) continue;
                var val = m.Groups["val"].Value;
                int komentar = val.IndexOf("//", StringComparison.Ordinal);
                if (komentar >= 0) val = val[..komentar];
                if (KodFirmyRx.IsMatch(val) || GlobalRx.IsMatch(val))
                    nalezy.Add($"{Path.GetFileName(file)}:{i + 1}: {line}");
            }
        }
        Assert.True(nalezy.Count == 0,
            "Text pro uživatele obsahuje globál nebo kód firmy:\n" + string.Join("\n", nalezy));
    }

    /// <summary>Pojistka proti tiše zelenému testu: původní tvar textu musí kontrola chytit.</summary>
    [Theory]
    [InlineData("d v.%Set(\"POPIS_LINKY\", \"(žádná linka v ^JRL pro firmu \"_dataDB_\")\")")]
    [InlineData("d v.%Set(\"POPIS_LINKY\", \"(žádná MHD linka pro firmu \"_IdFa_\")\")")]
    public void KontrolaChytiPuvodniText(string radek)
    {
        var val = SetRx.Match(radek).Groups["val"].Value;
        Assert.True(KodFirmyRx.IsMatch(val) || GlobalRx.IsMatch(val));
    }
}
