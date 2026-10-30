using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Driftová pojistka <b>parametrů sestavy</b> agend (<c>CACHE/ALVA/TemplateDesigner/Agendy/*.cls</c>).
///
/// <para>Slovník polí hlídá <see cref="AgendaSchemaDriftTests"/>, formuláře prvků
/// <see cref="EDInputFormSchemaDriftTests"/>. Parametry do 23. 10. nehlídalo nic, a tak se do linkových
/// agend dostala skupina „Tabulka linky", kterou kanon <c>ux-conventions §8</c> zakazuje - prvek se
/// jmenuje „Tabulka spojů" (review `B193`).</para>
///
/// <para>Parser vidí parametr zapsaný literálem (<c>s p.name = "X"</c>, <c>AddBoolParam(arr, "X", …)</c>)
/// kdekoli v agendě, i ve sdílené definici (<c>ParametrSmeruTisku</c>, <c>BaseAgenda</c>). Parametr
/// s názvem nebo popiskem skládaným za běhu (typy provozu linkového JŘ MHD) nevidí. Volby enumů se tu
/// nekontrolují - jejich popisky jsou zamčené (ukládá se zobrazený text).</para>
///
/// <para>Parametr <b>bez</b> <c>group</c> nespadne nikam „neutrálně" - dispatch ho shodí do vlastní
/// skupiny „Parametry sestavy" a uživatel uvidí osamocenou položku. Přesně to potkalo `DELENI_SLOUPCU`
/// u linkového JŘ (`L12`). Skupiny se navíc řadí podle prvního výskytu parametru, ne abecedně, takže
/// „Obecné" musí být pushnuté první.</para>
///
/// <para>⚠️ Kontroluje zdroj v repu, ne nasazenou třídu - že se změna dostala na server, se ověřuje
/// voláním <c>GetParameters</c>.</para>
/// </summary>
public class AgendaParameterDriftTests
{
    /// <summary>Skupiny, které nejsou názvem prvku. Konvence: skupina = prvek, kterého se parametr týká,
    /// nastavení celé sestavy jde do „Obecné". Nová výjimka jen s důvodem.</summary>
    private static readonly HashSet<string> ExtraGroups = new(StringComparer.Ordinal)
    {
        "Obecné",
    };

    /// <summary>Vědomé rozdíly popisku téhož parametru mezi agendami. Prázdné = žádný není.</summary>
    private static readonly HashSet<string> ExemptLabel = new(StringComparer.Ordinal);

    /// <summary>Parametry, které dělají v různých agendách totéž pod jiným názvem (název se ukládá
    /// v šablonách, přejmenovat ho nejde) - uživatel je musí vidět pod stejným popiskem.</summary>
    private static readonly (string A, string B, string Proc)[] StejnaVec =
    [
        ("ROZDELENI_SLOUPCU", "DELENI_SLOUPCU",
            "rozdělení spojů do sloupců podle platnosti, vývěska × linkový JŘ AD (Jan 23. 10.)"),
    ];

    [Fact]
    public void ParametrySeStejnymUcelem_MajiStejnyPopisek()
    {
        var labels = AllParams().Where(p => p.Label is not null)
            .GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.First().Label!);
        foreach (var (a, b, proc) in StejnaVec)
        {
            Assert.True(labels.ContainsKey(a) && labels.ContainsKey(b), $"Parser nenašel {a} nebo {b}.");
            Assert.True(labels[a] == labels[b], $"{a} „{labels[a]}\" × {b} „{labels[b]}\" - {proc}");
        }
    }

    private sealed record Param(string File, string Name)
    {
        public string? Label { get; set; }
        public string? Group { get; set; }
    }

    private static readonly Regex NewObjectRx = new(
        @"\bs\s+(\w+)\s*=\s*##class\(%DynamicObject\)\.%New\(\)", RegexOptions.Compiled);

    private static readonly Regex NameRx = new(
        @"\bs\s+(\w+)\.name\s*=\s*""([A-Z0-9_]+)""", RegexOptions.Compiled);

    private static readonly Regex PropRx = new(
        @"\bs\s+(\w+)\.(label|group)\s*=\s*""((?:[^""]|"""")*)""", RegexOptions.Compiled);

    /// <summary>AddBoolParam/AddNumberParam(arr, "NAME", "Label", výchozí[, "Skupina"…]).</summary>
    private static readonly Regex AddParamRx = new(
        @"Add(?:Bool|Number)Param\(\s*\w+\s*,\s*""([A-Z0-9_]+)""\s*,\s*""((?:[^""]|"""")*)""\s*,\s*(?:""[^""]*""|[^,)]*)\s*(?:,\s*""((?:[^""]|"""")*)"")?",
        RegexOptions.Compiled);

    private static List<Param> ParseAgenda(string path)
    {
        var result = new List<Param>();
        var current = new Dictionary<string, Param?>(StringComparer.Ordinal);
        string file = Path.GetFileName(path);

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                || line.TrimStart().StartsWith("///", StringComparison.Ordinal))
                continue;

            foreach (Match m in NewObjectRx.Matches(line))
                current[m.Groups[1].Value] = null;

            foreach (Match m in NameRx.Matches(line))
            {
                var p = new Param(file, m.Groups[2].Value);
                current[m.Groups[1].Value] = p;
                result.Add(p);
            }

            foreach (Match m in PropRx.Matches(line))
            {
                if (current.GetValueOrDefault(m.Groups[1].Value) is not { } p) continue;
                var value = m.Groups[3].Value.Replace("\"\"", "\"");
                if (m.Groups[2].Value == "label") p.Label = value;
                else p.Group = value;
            }

            foreach (Match m in AddParamRx.Matches(line))
                result.Add(new Param(file, m.Groups[1].Value)
                {
                    Label = m.Groups[2].Value.Replace("\"\"", "\""),
                    Group = m.Groups[3].Success ? m.Groups[3].Value : null,
                });
        }
        return result;
    }

    private static List<Param> AllParams() =>
        [.. ZdrojeAgend.KodAgend().SelectMany(ParseAgenda)];

    /// <summary>Pojistka proti tiše zelenému testu: kdyby se změnil zápis parametrů a parser je
    /// přestal vidět, ostatní testy projdou na prázdném vstupu.</summary>
    [Fact]
    public void Parser_NajdeZnameParametry()
    {
        var names = AllParams().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var expected in new[]
        {
            "TVAR_NAZVU_ZASTAVKY", "SMER_TISKU", "CASOVA_OSA", "ROZDELENI_SLOUPCU",
            "DELENI_SLOUPCU", "TISK_SIPKY", "ZASTAVKY_BEZ_ZASTAVENI", "POCET_STRAN_SESTAVY",
        })
            Assert.True(names.Contains(expected),
                $"Parser nenašel parametr {expected} - nejspíš se změnil zápis v .cls a test přestal hlídat.");
    }

    [Fact]
    public void KazdyParametr_MaSkupinuObecneNeboNazevPrvku()
    {
        var allowed = new HashSet<string>(ExtraGroups, StringComparer.Ordinal);
        foreach (var f in typeof(ElementTypes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (f.IsLiteral && f.GetRawConstantValue() is string type)
                allowed.Add(ElementNames.ForType(type));

        var bad = AllParams()
            .Where(p => p.Group is null || !allowed.Contains(p.Group))
            .Select(p => $"{p.File} {p.Name}: skupina \"{p.Group ?? "(žádná)"}\"")
            .Distinct()
            .ToList();

        Assert.True(bad.Count == 0,
            "Parametr bez skupiny skončí v osamocené skupině „Parametry sestavy\"; skupina má být " +
            "„Obecné\" nebo název prvku, kterého se týká (ElementNames):\n" + string.Join("\n", bad));
    }

    [Fact]
    public void StejnyParametr_MaVeVsechAgendachStejnyPopisek()
    {
        var drift = AllParams()
            .Where(p => p.Label is not null && !ExemptLabel.Contains(p.Name))
            .GroupBy(p => p.Name)
            .Select(g => (Name: g.Key, Labels: g.GroupBy(p => p.Label).ToList()))
            .Where(x => x.Labels.Count > 1)
            .Select(x => $"{x.Name}: " + string.Join(" × ", x.Labels.Select(
                l => $"\"{l.Key}\" ({string.Join(", ", l.Select(p => p.File).Distinct())})")))
            .ToList();

        Assert.True(drift.Count == 0,
            "Tentýž parametr se v agendách jmenuje jinak:\n" + string.Join("\n", drift));
    }

    [Fact]
    public void ZadneZakazaneTerminy_VPopiscichASkupinachParametru()
    {
        var hits = new List<string>();
        foreach (var p in AllParams())
            foreach (var (what, text) in new[] { ("popisek", p.Label), ("skupina", p.Group) })
            {
                if (text is null) continue;
                if (EDInputFormSchemaDriftTests.BannedExactLabels.Contains(text))
                    hits.Add($"{p.File} {p.Name} {what}: \"{text}\"");
                else
                    foreach (var sub in EDInputFormSchemaDriftTests.BannedLabelSubstrings)
                        if (text.Contains(sub, StringComparison.Ordinal))
                            hits.Add($"{p.File} {p.Name} {what}: \"{text}\" (obsahuje \"{sub}\")");
            }

        Assert.True(hits.Count == 0,
            "Zakázané termíny v parametrech agend (kanon ux-conventions §8):\n" +
            string.Join("\n", hits.Distinct()));
    }

    private static readonly Regex PushVarRx = new(@"arr\.%Push\((\w+)\)", RegexOptions.Compiled);
    private static readonly Regex GroupVarRx = new(@"s\s+(\w+)\.group\s*=\s*""([^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void ObecneJePrvniSkupina()
    {
        var spatne = new List<string>();
        int pushu = 0;

        foreach (var file in ZdrojeAgend.Agendy())
        {
            string body = GetParametersBody(File.ReadAllText(file));
            if (body.Length == 0) continue;

            var groups = GroupVarRx.Matches(body).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
            var poradi = PushVarRx.Matches(body).Select(m => m.Groups[1].Value)
                .Where(groups.ContainsKey).Select(v => groups[v]).ToList();
            pushu += poradi.Count;
            if (poradi.Count > 0 && poradi[0] != "Obecné")
                spatne.Add($"{Path.GetFileName(file)}: první skupina je „{poradi[0]}\"");
        }

        Assert.True(pushu >= 8, $"Naparsováno jen {pushu} parametrů s pořadím - regex nesedí na .cls?");
        Assert.True(spatne.Count == 0,
            "Skupiny se v tabu Parametry řadí podle prvního výskytu, takže „Obecné\" musí být "
            + "pushnuté první - je to nastavení celé sestavy, ne jednoho prvku:\n"
            + string.Join("\n", spatne));
    }

    /// <summary>Tělo <c>GetParameters</c> - od hlavičky metody po další <c>ClassMethod</c>.</summary>
    private static string GetParametersBody(string text)
    {
        int i = text.IndexOf("ClassMethod GetParameters", StringComparison.Ordinal);
        if (i < 0) return "";
        int end = text.IndexOf("\nClassMethod ", i + 1, StringComparison.Ordinal);
        return end < 0 ? text[i..] : text[i..end];
    }
}
