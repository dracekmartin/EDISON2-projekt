using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Driftová pojistka <b>datového slovníku agend</b> (<c>CACHE/ALVA/TemplateDesigner/Agendy/*.cls</c>).
///
/// <para><see cref="EDInputFormSchemaDriftTests"/> hlídá schémata <b>formulářů</b> (vlastnosti prvku).
/// Slovník polí, který uživatel vidí v nabídce dat a který jde do AI pomocníka, ale žije v agendách
/// a do 17. 8. ho nehlídalo nic - proto se linkový JŘ rozešel se zastávkovým: kolekce se jmenovaly
/// stejně (<c>ZASTAVKY</c>), ale řádky uvnitř nesly <c>POPIS</c> místo <c>NAZEV_ZASTAVKY</c>.
/// Vypadalo to kompatibilně a nebylo - tabulka s polem <c>{NAZEV_ZASTAVKY}</c> vykreslila
/// v linkovém JŘ tiše prázdno.</para>
///
/// <para><b>Porovnává se v rámci téhož místa</b> (skaláry × konkrétní kolekce), ne globálně:
/// <c>TYP</c> v kolekci zastávek („Typ řádku") a <c>TYP</c> v poznámkách („Typ poznámky") jsou
/// dvě různá pole ve dvou různých skupinách nabídky, ne drift.</para>
///
/// <para>Test čte <c>.cls</c> přímo z repa (offline, bez Caché serveru).</para>
/// </summary>
public class AgendaSchemaDriftTests
{
    /// <summary>
    /// Vědomé rozdíly popisku téhož pole mezi agendami - varianta upřesňuje význam pro svou doménu.
    /// Klíč = <c>&lt;místo&gt;|&lt;pole&gt;</c>. Nová výjimka jen s důvodem, jinak se to zvrhne
    /// v seznam nedodělků.
    /// </summary>
    private static readonly HashSet<string> ExemptLabel = new(StringComparer.Ordinal)
    {
        // AD tiskne číslo linky z licence pod svým názvem, MHD musí rozlišit licenční číslo od
        // veřejného označení - proto „(dle licence)". Rozdíl je doménový, ne drift (viz B56).
        "fields|CISLO_LINKY",
        "fields|LOKALNI_OZNACENI_LINKY",
    };

    /// <summary>Vědomé rozdíly typu téhož pole. Prázdné = žádný takový dnes není.</summary>
    private static readonly HashSet<string> ExemptType = new(StringComparer.Ordinal);

    private static readonly Regex AddFieldRx = new(
        """AddField\(\s*\w+\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*[,)]""",
        RegexOptions.Compiled);

    private static readonly Regex CollectionKeyRx = new(
        "collectionKey\\s*=\\s*\"([A-Z_0-9]+)\"",
        RegexOptions.Compiled);

    private record Entry(string Scope, string Key, string Label, string Type, string File);

    /// <summary>
    /// Vytáhne pole ze schématu jedné agendy. Čte se <b>sekvenčně</b>: dokud nepadne první
    /// <c>collectionKey</c>, jsou pole skalární; každý další <c>collectionKey</c> otevře novou
    /// kolekci. Proměnná držící pole se v agendách recykluje (<c>gf</c>), takže podle názvu
    /// proměnné to poznat nejde.
    /// </summary>
    private static List<Entry> ParseAgenda(string path)
    {
        var result = new List<Entry>();
        string scope = "fields";
        string file = Path.GetFileName(path);

        foreach (var line in File.ReadAllLines(path))
        {
            var col = CollectionKeyRx.Match(line);
            if (col.Success) scope = "collection:" + col.Groups[1].Value;

            foreach (Match m in AddFieldRx.Matches(line))
                result.Add(new Entry(scope, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, file));
        }
        return result;
    }

    private static List<Entry> AllFields() =>
        [.. Directory.GetFiles(AgendyDir(), "*.cls")
            .Where(p => !Path.GetFileName(p).Equals("BaseAgenda.cls", StringComparison.OrdinalIgnoreCase))
            .SelectMany(ParseAgenda)];

    /// <summary>
    /// Pojistka proti tiše zelenému testu: kdyby se změnil zápis schématu a regulární výraz
    /// přestal chytat, ostatní testy projdou na prázdném vstupu a nikdo si toho nevšimne.
    /// </summary>
    [Fact]
    public void Parser_NajdePoleVeVsechAgendach()
    {
        var perFile = AllFields().GroupBy(e => e.File).ToDictionary(g => g.Key, g => g.Count());

        var expected = new[]
        {
            "ZastavkoveJrAd.cls", "ZastavkoveJrMhd.cls", "LinkovyJrAd.cls", "LinkovyJrMhd.cls",
        };
        foreach (var f in expected)
            Assert.True(perFile.GetValueOrDefault(f) >= 10,
                $"{f}: parser našel {perFile.GetValueOrDefault(f)} polí schématu - čekalo se aspoň 10. " +
                "Nejspíš se změnil zápis AddField/collectionKey a test tiše přestal hlídat.");

        Assert.Contains(AllFields(),
            e => e.Scope == "collection:ZASTAVKY" && e.Key == "NAZEV_ZASTAVKY");
    }

    [Fact]
    public void StejnePole_MaVeVsechAgendachStejnyPopisek()
    {
        var drift = AllFields()
            .GroupBy(e => $"{e.Scope}|{e.Key}")
            .Where(g => !ExemptLabel.Contains(g.Key))
            .Select(g => (Id: g.Key, Labels: g.GroupBy(e => e.Label).ToList()))
            .Where(x => x.Labels.Count > 1)
            .Select(x => $"{x.Id}: " + string.Join(" × ", x.Labels.Select(
                l => $"\"{l.Key}\" ({string.Join(",", l.Select(e => e.File).Distinct())})")))
            .ToList();

        Assert.True(drift.Count == 0,
            "Totéž pole má v různých agendách různý popisek - uživatel by v nabídce dat viděl " +
            "dva názvy pro jednu věc (kanon ux-conventions §8). Sjednoť, nebo doplň výjimku s důvodem:\n" +
            string.Join("\n", drift));
    }

    [Fact]
    public void StejnePole_MaVeVsechAgendachStejnyTyp()
    {
        var drift = AllFields()
            .GroupBy(e => $"{e.Scope}|{e.Key}")
            .Where(g => !ExemptType.Contains(g.Key))
            .Select(g => (Id: g.Key, Types: g.GroupBy(e => e.Type).ToList()))
            .Where(x => x.Types.Count > 1)
            .Select(x => $"{x.Id}: " + string.Join(" × ", x.Types.Select(
                t => $"{t.Key} ({string.Join(",", t.Select(e => e.File).Distinct())})")))
            .ToList();

        Assert.True(drift.Count == 0,
            "Totéž pole je v různých agendách jiného typu - formátování hodnoty by se lišilo " +
            "podle toho, ze které agendy data přišla:\n" + string.Join("\n", drift));
    }

    [Fact]
    public void ZadneZakazaneTerminy_VPopiscichPoliAgend()
    {
        var hits = new List<string>();
        foreach (var e in AllFields())
        {
            if (EDInputFormSchemaDriftTests.BannedExactLabels.Contains(e.Label))
                hits.Add($"{e.File} {e.Scope}|{e.Key}: \"{e.Label}\"");
            else
                foreach (var sub in EDInputFormSchemaDriftTests.BannedLabelSubstrings)
                    if (e.Label.Contains(sub, StringComparison.Ordinal))
                        hits.Add($"{e.File} {e.Scope}|{e.Key}: \"{e.Label}\" (obsahuje \"{sub}\")");
        }

        Assert.True(hits.Count == 0,
            "Zakázané termíny v popiscích polí agend (kanon ux-conventions §8):\n" +
            string.Join("\n", hits.Distinct()));
    }

    private static string AgendyDir()
    {
        // Od bin/Debug/... nahoru k rootu repa (kde leží EDISON2.sln)
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;

        // Běh z izolovaného výstupu (dotnet test -o mimo repo) → root odvodit z cesty tohoto zdrojáku
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;

        Assert.True(File.Exists(Path.Combine(dir!.FullName, "EDISON2.sln")),
            "Nenalezen root repa (EDISON2.sln) - drift testy potřebují zdrojáky .cls");
        return Path.Combine(dir.FullName,
            "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "Agendy");
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
