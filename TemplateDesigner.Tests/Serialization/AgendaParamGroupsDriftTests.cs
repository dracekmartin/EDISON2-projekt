using System.IO;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Driftová pojistka na <b>rozdělení parametrů sestavy do skupin</b> v tabu Parametry.
/// </summary>
/// <remarks>
/// <para>Parametr <b>bez</b> <c>group</c> nespadne nikam „neutrálně" - dispatch ho shodí do vlastní
/// skupiny „Parametry sestavy", takže uživatel uvidí osamocenou položku vedle ostatních skupin.
/// Přesně to potkalo `DELENI_SLOUPCU` u linkového JŘ (`L12`): nejdůležitější volba agendy si udělala
/// vlastní skupinu jen tím, že jí zařazení chybělo.</para>
/// <para>Druhá půlka je <b>pořadí</b>: skupiny se řadí podle prvního výskytu parametru v poli, ne
/// abecedně. „Obecné" proto musí být pushnuté první - je to nastavení celé sestavy, ne jednoho
/// prvku (konvence zapsaná u zastávkového JŘ).</para>
/// <para>⚠️ <b>Co test zatím nevidí:</b> parametry zakládané pomocnou metodou
/// (<c>AddBoolParam</c>, kde je skupina až pátým argumentem) - jejich volání se nedá spolehlivě
/// rozebrat regulárem, protože popis parametru běžně obsahuje čárky. Kontroluje se tvar
/// <c>arr.%Push(promenna)</c>, na kterém vada u linkového JŘ vznikla.</para>
/// <para>Test běží nad <c>.cls</c> z repa, tedy offline bez Caché serveru. ⚠️ Kontroluje zdroj, ne
/// nasazenou třídu - že se změna opravdu dostala na server, se ověřuje voláním <c>GetParameters</c>.</para>
/// </remarks>
public class AgendaParamGroupsDriftTests
{
    private static readonly Regex PushRx  = new(@"arr\.%Push\((\w+)\)", RegexOptions.Compiled);
    private static readonly Regex GroupRx = new(@"s\s+(\w+)\.group\s*=\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex NameRx  = new(@"s\s+(\w+)\.name\s*=\s*""([^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void KazdyParametrSestavy_MaSkupinu()
    {
        var bezSkupiny = new List<string>();

        foreach (var file in AgendaFiles())
        {
            string body = GetParametersBody(File.ReadAllText(file));
            if (body.Length == 0) continue;

            var groups = GroupRx.Matches(body).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
            var names = NameRx.Matches(body).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);

            foreach (var promenna in PushRx.Matches(body).Select(m => m.Groups[1].Value).Distinct())
                if (!groups.ContainsKey(promenna))
                    bezSkupiny.Add($"{Path.GetFileName(file)}: {names.GetValueOrDefault(promenna, promenna)}");
        }

        Assert.True(bezSkupiny.Count == 0,
            "Parametr sestavy bez `group` skončí ve vlastní skupině „Parametry sestavy“ a uživatel "
            + "ho uvidí osamoceně vedle ostatních:\n" + string.Join("\n", bezSkupiny)
            + "\nDoplň parametru `group` (a `description`, ať je z tabu poznat, co dělá).");
    }

    [Fact]
    public void ObecneJePrvniSkupina()
    {
        var spatne = new List<string>();

        foreach (var file in AgendaFiles())
        {
            string body = GetParametersBody(File.ReadAllText(file));
            if (body.Length == 0) continue;

            var groups = GroupRx.Matches(body).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
            var poradi = PushRx.Matches(body).Select(m => m.Groups[1].Value)
                .Where(groups.ContainsKey).Select(v => groups[v]).ToList();
            if (poradi.Count == 0) continue;

            if (poradi[0] != "Obecné")
                spatne.Add($"{Path.GetFileName(file)}: první skupina je „{poradi[0]}“");
        }

        Assert.True(spatne.Count == 0,
            "Skupiny se v tabu Parametry řadí podle prvního výskytu, takže „Obecné“ musí být "
            + "pushnuté první - je to nastavení celé sestavy, ne jednoho prvku:\n"
            + string.Join("\n", spatne));
    }

    /// <summary>Pojistka, že parser vůbec něco najde - jinak by testy výše svítily naprázdno.</summary>
    [Fact]
    public void ParserNajdeParametryLinkovehoJr()
    {
        string body = GetParametersBody(File.ReadAllText(AgendaFile("LinkovyJrAd.cls")));

        Assert.Contains("DELENI_SLOUPCU", body);
        Assert.True(PushRx.Matches(body).Count >= 8,
            $"Naparsováno jen {PushRx.Matches(body).Count} parametrů - regex nesedí na .cls?");
    }

    // ── Parsování ─────────────────────────────────────────────────────────────

    /// <summary>Tělo <c>GetParameters</c> - od hlavičky metody po další <c>ClassMethod</c>.</summary>
    private static string GetParametersBody(string text)
    {
        int i = text.IndexOf("ClassMethod GetParameters", StringComparison.Ordinal);
        if (i < 0) return "";
        int end = text.IndexOf("\nClassMethod ", i + 1, StringComparison.Ordinal);
        return end < 0 ? text[i..] : text[i..end];
    }

    private static string[] AgendaFiles() => Directory.GetFiles(AgendyDir(), "*.cls");

    private static string AgendaFile(string name) => Path.Combine(AgendyDir(), name);

    private static string AgendyDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "Agendy");
    }
}
