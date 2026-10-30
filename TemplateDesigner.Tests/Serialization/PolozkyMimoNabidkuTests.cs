using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Položky, které data agend posílají mimo nabídku dat, drží Caché test kontraktu v seznamu
/// <c>KontraktAgendTest.PolozkyMimoNabidku</c> i s druhem (<c>B194f</c> G1). Druh <c>prvek</c> tvrdí,
/// že položku čte kód návrháře. Kdyby ji nic nečetlo, seznam by mrtvou položku schoval a test kontraktu
/// by ji pustil, proto se tvrzení ověřuje proti zdrojákům návrháře a druh <c>parametr</c> proti agendám.
/// Hledá se klíč v uvozovkách, takže krátké klíče (<c>ID</c>, <c>TYP</c>) projdou snadno - hlídá se
/// hlavně to, že klíč nezmizel z kódu úplně.
/// </summary>
public class PolozkyMimoNabidkuTests
{
    private static readonly Regex Polozka = new(
        @"set mimo\(""(?<kolekce>[A-Za-z_]+)""(?:, ""(?<klic>[A-Z_]+)"")?\) = ""(?<druh>[a-z]+)""",
        RegexOptions.Compiled);

    private static readonly HashSet<string> ZnameDruhy = ["prvek", "parametr", "sablony", "zbytek"];

    /// <summary>
    /// Položka druhu <c>prvek</c> je v kontraktu dat prvků (<see cref="TemplateDesigner.Models.KontraktDat"/>) se stejnou kolekcí i klíčem.
    /// Že kód návrháře každou položku kontraktu opravdu čte, hlídá <see cref="KontraktDatTests"/>. Do 30. 10. se tu
    /// klíč jen hledal v uvozovkách kdekoli v kódu, takže krátké klíče (<c>ID</c>, <c>TYP</c>) prošly vždy (revize B4).
    /// </summary>
    [Fact]
    public void PolozkaPrvku_JeVKontraktuDat()
    {
        var kontrakt = TemplateDesigner.Models.KontraktDat.Polozky;
        var mimoKontrakt = Polozky()
            .Where(p => p.Druh == "prvek")
            .Where(p => !kontrakt.Any(k => k.Kolekce == p.Kolekce && (p.Klic is null || k.Klic == p.Klic)))
            .Select(p => $"{p.Kolekce} {p.Klic}")
            .ToList();

        Assert.True(mimoKontrakt.Count == 0,
            "Položka má v seznamu druh „prvek“, ale kontrakt dat prvků (KontraktDat) ji nemá - doplň ji tam, nebo oprav druh:\n"
            + string.Join("\n", mimoKontrakt));
    }

    [Fact]
    public void PolozkaParametru_JeVDefiniciParametru()
    {
        var agendy = string.Join("\n", ZdrojeAgend.KodAgend().Select(File.ReadAllText));

        var chybi = Polozky()
            .Where(p => p.Druh == "parametr" && !agendy.Contains($"\"visibleWhenListedIn\", \"{p.Klic}\""))
            .Select(p => p.Klic)
            .ToList();

        Assert.True(chybi.Count == 0,
            "Položka má druh „parametr“, ale žádná agenda na ni neodkazuje z visibleWhenListedIn:\n" + string.Join("\n", chybi));
    }

    [Fact]
    public void Parser_NajdePolozkyZnamychDruhu()
    {
        var polozky = Polozky();

        Assert.True(polozky.Count(p => p.Druh == "prvek") > 20, "parser nenašel položky prvků - změnil se zápis seznamu?");
        Assert.Empty(polozky.Where(p => !ZnameDruhy.Contains(p.Druh)).Select(p => $"{p.Kolekce} {p.Klic}: {p.Druh}"));
    }

    private static List<(string Kolekce, string? Klic, string Druh)> Polozky()
    {
        var cls = File.ReadAllText(Path.Combine(Koren(), "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "Tests", "KontraktAgendTest.cls"));
        return Polozka.Matches(cls)
            .Select(m => (m.Groups["kolekce"].Value, m.Groups["klic"].Success ? m.Groups["klic"].Value : null, m.Groups["druh"].Value))
            .ToList();
    }

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
