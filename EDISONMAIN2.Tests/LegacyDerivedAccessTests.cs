using System.IO;
using System.Text.RegularExpressions;
using EDISONMAIN2.Modules;

namespace EDISONMAIN2.Tests;

public class LegacyDerivedAccessTests
{
    [Fact]
    public void Auris_AddsAurisGroup()
    {
        var allowed = new HashSet<string> { "38001" };

        LegacyDerivedAccess.AddTo(allowed);

        Assert.Contains("30", allowed);
    }

    [Fact]
    public void WithoutTriggers_AccessUnchanged()
    {
        var allowed = new HashSet<string> { "8", "12" };

        LegacyDerivedAccess.AddTo(allowed);

        Assert.Equal(new HashSet<string> { "8", "12" }, allowed);
    }

    // Pravidla legacy s cílem mimo strom (zakomentované uzly navbaru) se nepřebírají - nový shell je nezobrazí.
    [LegacyRepoFact]
    public void Rules_MatchLegacyEdUsersForTreeTargets()
    {
        string path = Path.Combine(LegacyTreeSources.Root!, "EDUsers", "EDUsers", "EDUsers.vb");
        var (legacy, unknown) = ParseLegacyRules(File.ReadAllText(path));
        Assert.True(unknown.Count == 0, $"Nerozpoznané řádky GetEdisonAccess v {path} - rozšiř parser testu:\n{string.Join("\n", unknown)}");

        var treeIds = TreeElementIds();
        var expected = Describe(legacy.ToDictionary(r => r.Key, r => r.Value.Where(treeIds.Contains)));
        var actual = Describe(LegacyDerivedAccess.Rules.ToDictionary(r => r.Key, r => r.Value.AsEnumerable()));
        Assert.True(expected.SequenceEqual(actual),
            $"LegacyDerivedAccess.Rules se liší od {path}. Nejdřív git pull legacy repa, teprve aktuální legacy je důvod Rules měnit.\n" +
            $"Legacy: [{string.Join("; ", expected)}]\nShell:  [{string.Join("; ", actual)}]");
    }

    [Fact]
    public void LegacyParser_MergesRepeatedTrigger_ReportsUnknownForms()
    {
        const string vb = """
            Public Function GetEdisonAccess(ByVal lic As Lic) As Hashtable
                Case Else
                    For i As Integer = 2 To output.GetUpperBound(0)
                        If Not htEdison.ContainsKey(output(i)) Then htEdison.Add(output(i), Nothing)
                        If output(i) = "11001" AndAlso Not htEdison.ContainsKey("35") Then htEdison.Add("35", Nothing) ' Zakaznici
                        If output(i) = "6001" Then ' JRMAIN
                            If Not htEdison.ContainsKey("60") Then htEdison.Add("60", Nothing)
                        End If
                        If output(i) = "6001" Then
                            If Not htEdison.ContainsKey("60.4") Then htEdison.Add("60.4", Nothing)
                        End If
                        ' If output(i) = "1" Then htEdison.Add("2", Nothing)
                        If output(i) = "169001" OrElse output(i) = "6039" Then
                            htEdison("60.3") = Nothing
                        End If
                    Next
                    For Each element As String In lic.Elements
            """;

        var (rules, unknown) = ParseLegacyRules(vb);

        Assert.Equal(new[] { "11001 -> 35", "6001 -> 60, 60.4" }, Describe(rules.ToDictionary(r => r.Key, r => r.Value.AsEnumerable())));
        Assert.Equal(2, unknown.Count);
    }

    private const string Code = @"""([\d.]+)""";

    // Řádkový parser bloku Case Else s výčtem povolených tvarů: jiný řádek s htEdison/output( by šel přehlédnout
    // nebo připsat cizímu spouštěči, proto se vrací jako nerozpoznaný.
    private static (Dictionary<string, HashSet<string>> rules, List<string> unknown) ParseLegacyRules(string vb)
    {
        int fn = vb.IndexOf("Public Function GetEdisonAccess(", StringComparison.Ordinal);
        int start = fn < 0 ? -1 : vb.IndexOf("Case Else", fn, StringComparison.Ordinal);
        int end = start < 0 ? -1 : vb.IndexOf("For Each element As String In lic.Elements", start, StringComparison.Ordinal);
        Assert.True(end > start && start > fn && fn >= 0, "V EDUsers.vb chybí blok Case Else funkce GetEdisonAccess");

        var rules = new Dictionary<string, HashSet<string>>();
        var unknown = new List<string>();
        string? trigger = null;
        foreach (var raw in vb[start..end].Split('\n'))
        {
            string line = Regex.Replace(raw.Trim(), @"\s*'.*$", "");
            if (line == "End If") { trigger = null; continue; }
            if (!line.Contains("htEdison") && !line.Contains("output(")) continue;
            if (line == "If Not htEdison.ContainsKey(output(i)) Then htEdison.Add(output(i), Nothing)") continue;

            Match m;
            if ((m = Regex.Match(line, $@"^If output\(i\) = {Code} AndAlso Not htEdison\.ContainsKey\({Code}\) Then htEdison\.Add\(""\2"", Nothing\)$")).Success)
                AddRule(rules, m.Groups[1].Value, m.Groups[2].Value);
            else if ((m = Regex.Match(line, $@"^If output\(i\) = {Code} Then$")).Success)
                trigger = m.Groups[1].Value;
            else if (trigger is not null && (m = Regex.Match(line, $@"^If Not htEdison\.ContainsKey\({Code}\) Then htEdison\.Add\(""\1"", Nothing\)$")).Success)
                AddRule(rules, trigger, m.Groups[1].Value);
            else
                unknown.Add(line);
        }
        return (rules, unknown);
    }

    private static void AddRule(Dictionary<string, HashSet<string>> rules, string trigger, string target)
    {
        if (!rules.TryGetValue(trigger, out var targets)) rules[trigger] = targets = [];
        targets.Add(target);
    }

    private static HashSet<string> TreeElementIds()
    {
        string cls = File.ReadAllText(LegacyTreeSources.RepoPath("EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/Tree.cls"));
        const string str = @"""(?:[^""]|"""")*""";
        var families = Regex.Matches(cls, $@"\.\.AddFamily\({str}\s*,\s*\d+\s*,\s*""([^""]*)""");
        var exes = Regex.Matches(cls, $@"\.\.AddExe\({str}\s*,\s*{str}\s*,\s*{str}\s*,\s*""([^""]*)""");
        return families.Concat(exes).Select(m => m.Groups[1].Value).Where(id => id.Length > 0).ToHashSet();
    }

    private static string[] Describe(Dictionary<string, IEnumerable<string>> rules) =>
        rules.Where(r => r.Value.Any())
            .Select(r => $"{r.Key} -> {string.Join(", ", r.Value.Distinct().Order())}").Order().ToArray();
}

/// <summary>Parita s legacy repem EDISON - zapíná ji <c>EDISON_LEGACY_ROOT</c> (pre-push ji nastaví, když repo na stroji je).</summary>
public sealed class LegacyRepoFactAttribute : FactAttribute
{
    public LegacyRepoFactAttribute()
    {
        if (string.IsNullOrEmpty(LegacyTreeSources.Root)) Skip = "EDISON_LEGACY_ROOT není nastavená - parita potřebuje legacy repo EDISON.";
        else if (!Directory.Exists(LegacyTreeSources.Root)) Skip = $"EDISON_LEGACY_ROOT={LegacyTreeSources.Root} neexistuje.";
    }
}
