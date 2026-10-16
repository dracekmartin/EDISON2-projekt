using System.IO;
using System.Text.RegularExpressions;
using EDISONMAIN2.Windows.ChangelogWindow;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Parita <see cref="InfChangelogSource.InfKeyFor"/> s legacy <c>Package.ToInformationFile</c> (EDLIBRARY/clsConversion.vb)
/// pro každý člen EDPackageEnum a pro každou agendu stromu: nový alias v legacy nebo exe pojmenované jinak než balíček
/// shodí test dřív, než agenda ukáže cizí nebo žádné novinky.
/// </summary>
public class InfKeyLegacyParityTests
{
    [LegacyRepoFact]
    public void InfKeyFor_MatchesLegacyToInformationFile_ForEveryPackage()
    {
        string enumPath = Path.Combine(LegacyTreeSources.Root!, "EDCORE", "EDFormInterfaceLic.vb");
        string conversionPath = Path.Combine(LegacyTreeSources.Root!, "EDLIBRARY", "clsConversion.vb");
        var (packages, unknownEnum) = Packages(File.ReadAllText(enumPath));
        var (mapped, unknown) = ParseToInformationFile(File.ReadAllText(conversionPath));
        Assert.True(unknownEnum.Count == 0, $"Nerozpoznané řádky EDPackageEnum v {enumPath} - rozšiř parser testu:\n{string.Join("\n", unknownEnum)}");
        Assert.True(unknown.Count == 0, $"Nerozpoznané řádky ToInformationFile v {conversionPath} - rozšiř parser testu:\n{string.Join("\n", unknown)}");
        Assert.NotEmpty(mapped);

        var differences = Differences(packages.Keys, mapped);

        Assert.True(differences.Count == 0,
            $"InfKeyFor se liší od {conversionPath}. Nejdřív git pull legacy repa, teprve aktuální legacy je důvod InfKeyFor měnit.\n" +
            string.Join("\n", differences));
    }

    // Legacy shell hledá novinky podle formuláře uzlu (Version.ToInformationFile, balíček = formId \ 1000 * 1000),
    // shell podle exe. Exe pojmenované jinak než balíček (ZauMzdy.exe v ZAUCTOVANIMzdy) by bez aliasu novinky neukázalo.
    // Formuláře Tree.cls drží EDFormInterfaceLic.vb, proto se při jeho driftu test přeskočí jako parita stromu.
    [LegacyTreeSourcesFact]
    public void InfKeyFor_MatchesLegacyFormLookup_ForEveryTreeModule()
    {
        string enumPath = Path.Combine(LegacyTreeSources.Root!, "EDCORE", "EDFormInterfaceLic.vb");
        string conversionPath = Path.Combine(LegacyTreeSources.Root!, "EDLIBRARY", "clsConversion.vb");
        var (packages, unknownEnum) = Packages(File.ReadAllText(enumPath));
        var (mapped, unknown) = ParseToInformationFile(File.ReadAllText(conversionPath));
        Assert.True(unknownEnum.Count == 0, $"Nerozpoznané řádky EDPackageEnum v {enumPath} - rozšiř parser testu:\n{string.Join("\n", unknownEnum)}");
        Assert.True(unknown.Count == 0, $"Nerozpoznané řádky ToInformationFile v {conversionPath} - rozšiř parser testu:\n{string.Join("\n", unknown)}");
        var modules = TreeModules(File.ReadAllText(LegacyTreeSources.RepoPath("EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/Tree.cls")));
        Assert.True(modules.Count > 100, $"Tree.cls dal jen {modules.Count} agend - rozšiř parser testu.");

        var differences = TreeDifferences(modules, packages, mapped);

        Assert.True(differences.Count == 0,
            $"InfKeyFor(exe) se liší od legacy novinek podle formuláře ({conversionPath}). Nejdřív git pull legacy repa.\n" +
            string.Join("\n", differences));
    }

    [Fact]
    public void TreeDifferences_ExeNamedOtherThanPackage_IsReported()
    {
        var packages = new Dictionary<string, int> { ["ZAUCTOVANIMzdy"] = 168000, ["MZDY2"] = 146000 };
        var mapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ZAUCTOVANIMzdy"] = "ZAUCTOVANI" };
        var modules = TreeModules("""
            		d ..AddExe("Zaměstnanci", "MZDY2.exe", "ALVA.MZDY2.formMZDY2Main", "146001", 1, "", 0, "", 0)
            		d ..AddExe("Zaúčtování", "Obcasny.exe", "ALVA.ZAUCTOVANI.ZauMzdy.formZAUMZDYMain", "168001", 0, "", 0, "", 0)
            		d ..AddExe("Stub", "", "ALVA.TISK.frmReportShow", "99001", 0, "", 0, "", 0)
            		d ..AddExe("Cizí", "X.exe", "ALVA.X.formX", "999001", 0, "", 0, "", 0)
            """);

        var differences = TreeDifferences(modules, packages, mapped);

        Assert.Equal(new[]
        {
            "Obcasny.exe (form 168001, ZAUCTOVANIMzdy): legacy ZAUCTOVANI, shell OBCASNY",
            "X.exe (form 999001): balíček 999000 v EDPackageEnum chybí",
        }, differences);
    }

    [Fact]
    public void LegacyParser_ReadsCaseShapes_ReportsUnknownLines()
    {
        var vb = LegacyFunction("""
            Case EDFormInterface.EDPackageEnum.JRLIC,
                  EDFormInterface.EDPackageEnum.JRMAIN
                file = Strings.Left(package.ToString, 2)
            Case EDFormInterface.EDPackageEnum.NAKLADNIDOPRAVA, 'EDFormInterface.EDPackageEnum.NDMAIN,
                EDFormInterface.EDPackageEnum.NDOBJEDNAVKY
                file = "ND"
            Case EDFormInterface.EDPackageEnum.ISYJR
                file = package.ToString.ToUpper
            """);

        var (mapped, unknown) = ParseToInformationFile(vb);

        Assert.Equal(new Dictionary<string, string> { ["JRLIC"] = "JR", ["JRMAIN"] = "JR", ["NAKLADNIDOPRAVA"] = "ND", ["NDOBJEDNAVKY"] = "ND" },
            mapped);
        Assert.Contains("file = package.ToString.ToUpper", unknown);
        Assert.Contains(unknown, line => line.Contains("ISYJR"));
    }

    // Jiné pravidlo pro nevyjmenované balíčky by test bez povšimnutí porovnával se starým.
    [Fact]
    public void LegacyParser_ChangedCaseElse_IsUnknown()
    {
        var (_, unknown) = ParseToInformationFile(LegacyFunction("", caseElse: "file = package.ToString"));

        Assert.Single(unknown);
    }

    // Návrat nebo přepis file před Select Case či za ním změní výsledek mimo větve, které parser čte.
    [Theory]
    [InlineData("Dim file As String = \"\"\nIf package = EDFormInterface.EDPackageEnum.MZDY2 Then Return \"MZDY.INF\"", null)]
    [InlineData(null, "If package = EDFormInterface.EDPackageEnum.MZDY2 Then file = \"MZDY\"")]
    public void LegacyParser_CodeOutsideSelect_IsUnknown(string? prologue, string? epilogueLine)
    {
        var vb = LegacyFunction("", prologue: prologue ?? Prologue,
            epilogue: epilogueLine is null ? LegacyEpilogue : epilogueLine + "\n" + LegacyEpilogue);

        var (_, unknown) = ParseToInformationFile(vb);

        Assert.Single(unknown);
    }

    // VB nerozlišuje velikost písmen v identifikátorech: Mzdy2 v Case je týž člen jako MZDY2.
    [Fact]
    public void Differences_AliasWrittenInOtherCase_IsReported()
    {
        var (mapped, _) = ParseToInformationFile(LegacyFunction("""
            Case EDFormInterface.EDPackageEnum.Mzdy2
                file = "MZDY"
            """));

        var differences = Differences(["MZDY2"], mapped);

        Assert.Equal(new[] { "MZDY2: legacy MZDY, shell MZDY2" }, differences);
    }

    [Fact]
    public void Differences_NewLegacyAlias_IsReported()
    {
        var mapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["MZDY2"] = "MZDY" };

        var differences = Differences(["MZDY2", "USERS", "ADSETTINGS"], mapped);

        Assert.Equal(new[] { "MZDY2: legacy MZDY, shell MZDY2" }, differences);
    }

    private const string Prologue = "Dim file As String = \"\"";

    private static readonly string LegacyCaseElse = string.Join("\n",
        "If package.ToString().StartsWith(\"AD\") Then",
        "file = Strings.Left(package.ToString(), 2)",
        "Else",
        "file = package.ToString",
        "End If");

    private static readonly string LegacyEpilogue = string.Join("\n",
        "If Len(file) > 0 Then",
        "If differenceOnly Then file &= \"_DIFF\"",
        "Return file & \".INF\"",
        "End If",
        "Return \"\"");

    private static string LegacyFunction(string selectBody, string? caseElse = null, string prologue = Prologue, string? epilogue = null) =>
        string.Join("\n",
            "Public Shared Function ToInformationFile(ByVal package As EDFormInterface.EDPackageEnum, ByVal differenceOnly As Boolean) As String",
            prologue,
            "Select Case package",
            selectBody,
            "Case Else",
            caseElse ?? LegacyCaseElse,
            "End Select",
            epilogue ?? LegacyEpilogue,
            "End Function");

    private static List<string> Differences(IEnumerable<string> packages, Dictionary<string, string> mapped)
    {
        var differences = new List<string>();
        foreach (var package in packages)
        {
            string legacy = LegacyFile(package, mapped);
            // Windows jména souborů nerozlišují velikost písmen, legacy píše ToString enumu (ISYCiselniky).
            string shell = InfChangelogSource.InfKeyFor(package + ".exe");
            if (!string.Equals(legacy, shell, StringComparison.OrdinalIgnoreCase))
                differences.Add($"{package}: legacy {legacy.ToUpperInvariant()}, shell {shell}");
        }
        return differences;
    }

    // Case Else legacy: AD* sdílí AD.INF, ostatní mají soubor podle jména balíčku.
    private static string LegacyFile(string package, Dictionary<string, string> mapped) =>
        mapped.TryGetValue(package, out var file) ? file
            : package.StartsWith("AD", StringComparison.Ordinal) ? package[..2] : package;

    private static List<string> TreeDifferences(IEnumerable<(string Exe, int FormId)> modules,
        Dictionary<string, int> packages, Dictionary<string, string> mapped)
    {
        var byValue = packages.ToDictionary(p => p.Value, p => p.Key);
        var differences = new List<string>();
        foreach (var (exe, formId) in modules)
        {
            int value = formId / 1000 * 1000;
            if (!byValue.TryGetValue(value, out var package))
            {
                differences.Add($"{exe} (form {formId}): balíček {value} v EDPackageEnum chybí");
                continue;
            }
            string legacy = LegacyFile(package, mapped);
            string shell = InfChangelogSource.InfKeyFor(exe);
            if (!string.Equals(legacy, shell, StringComparison.OrdinalIgnoreCase))
                differences.Add($"{exe} (form {formId}, {package}): legacy {legacy.ToUpperInvariant()}, shell {shell}");
        }
        return differences.Distinct().ToList();
    }

    // Agendy stromu s exe a formulářem; stub bez exe novinky nemá (shell je hledá jen u spustitelného modulu).
    private static List<(string Exe, int FormId)> TreeModules(string treeCls) =>
        Regex.Matches(treeCls, @"d \.\.AddExe\(""(?:[^""]|"""")*"",\s*""([^""]+)"",\s*""(?:[^""]|"""")*"",\s*""(\d+)""")
            .Select(m => (m.Groups[1].Value, int.Parse(m.Groups[2].Value)))
            .ToList();

    // Řádky bez komentářů (VB komentář začíná ') a bez prázdných, ořezané.
    private static IEnumerable<string> CodeLines(string vb) =>
        vb.Split('\n').Select(raw => Regex.Replace(raw.Trim(), @"\s*'.*$", "")).Where(line => line.Length > 0);

    private static (Dictionary<string, int> packages, List<string> unknown) Packages(string vb)
    {
        int start = vb.IndexOf("Enum EDPackageEnum", StringComparison.Ordinal);
        int end = start < 0 ? -1 : vb.IndexOf("End Enum", start, StringComparison.Ordinal);
        Assert.True(end > start && start >= 0, "V EDFormInterfaceLic.vb chybí Enum EDPackageEnum");

        var packages = new Dictionary<string, int>();
        var unknown = new List<string>();
        foreach (var line in CodeLines(vb[start..end]).Skip(1))
        {
            var m = Regex.Match(line, @"^(\w+)\s*=\s*(-?\d+)$");
            // Dva členy se stejnou hodnotou by formuláři nedaly jednoznačný balíček.
            if (m.Success && !packages.ContainsValue(int.Parse(m.Groups[2].Value)))
                packages.Add(m.Groups[1].Value, int.Parse(m.Groups[2].Value));
            else unknown.Add(line);
        }
        return (packages, unknown);
    }

    // Řádkový parser funkce s výčtem povolených tvarů: jiný tvar přiřazení nebo kód mimo Select Case by test
    // přešel a porovnával špatně. Kód před Select Case a za ním musí být přesně ten dnešní.
    private static (Dictionary<string, string> mapped, List<string> unknown) ParseToInformationFile(string vb)
    {
        int fn = vb.IndexOf("Public Shared Function ToInformationFile(ByVal package As EDFormInterface.EDPackageEnum", StringComparison.Ordinal);
        int body = fn < 0 ? -1 : vb.IndexOf('\n', fn);
        int start = body < 0 ? -1 : vb.IndexOf("Select Case package", body, StringComparison.Ordinal);
        int end = start < 0 ? -1 : vb.IndexOf("End Select", start, StringComparison.Ordinal);
        int fnEnd = end < 0 ? -1 : vb.IndexOf("End Function", end, StringComparison.Ordinal);
        Assert.True(fnEnd > end && end > start && start > body && body > fn && fn >= 0,
            "V clsConversion.vb chybí funkce ToInformationFile(package) se Select Case");

        const string member = @"EDFormInterface\.EDPackageEnum\.\w+";
        // VB nerozlišuje velikost písmen v identifikátorech, Case smí člen enumu napsat jinak než deklarace.
        var mapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var unknown = new List<string>();
        var pending = new List<string>();
        var caseElse = new List<string>();
        bool continues = false, assigned = true, inElse = false;

        void CloseCase()
        {
            if (!assigned) unknown.Add($"Case {string.Join(", ", pending)} bez přiřazení file");
            pending.Clear();
            assigned = false;
        }

        string prologue = string.Join("\n", CodeLines(vb[body..start]));
        if (prologue != Prologue) unknown.Add($"Před Select Case: {prologue.Replace("\n", " | ")}");

        foreach (var line in CodeLines(vb[start..end]).Skip(1))
        {
            if (inElse) { caseElse.Add(line); continue; }

            Match m;
            if (line == "Case Else")
            {
                CloseCase();
                inElse = true;
            }
            else if (Regex.IsMatch(line, $@"^Case {member}(\s*,\s*{member})*\s*,?$"))
            {
                CloseCase();
                pending.AddRange(Regex.Matches(line, @"EDPackageEnum\.(\w+)").Select(x => x.Groups[1].Value));
                continues = line.EndsWith(',');
            }
            else if (continues && Regex.IsMatch(line, $@"^{member}(\s*,\s*{member})*\s*,?$"))
            {
                pending.AddRange(Regex.Matches(line, @"EDPackageEnum\.(\w+)").Select(x => x.Groups[1].Value));
                continues = line.EndsWith(',');
            }
            else if ((m = Regex.Match(line, @"^file = ""(\w+)""$")).Success && !assigned)
            {
                foreach (var name in pending) mapped.TryAdd(name, m.Groups[1].Value);
                assigned = true;
            }
            else if ((m = Regex.Match(line, @"^file = Strings\.Left\(package\.ToString, (\d+)\)$")).Success && !assigned)
            {
                int length = int.Parse(m.Groups[1].Value);
                foreach (var name in pending) mapped.TryAdd(name, name[..Math.Min(length, name.Length)]);
                assigned = true;
            }
            else
            {
                unknown.Add(line);
            }
        }
        if (!inElse) CloseCase();
        if (string.Join("\n", caseElse) != LegacyCaseElse) unknown.Add($"Case Else: {string.Join(" | ", caseElse)}");

        string epilogue = string.Join("\n", CodeLines(vb[(end + "End Select".Length)..fnEnd]));
        if (epilogue != LegacyEpilogue) unknown.Add($"Za End Select: {epilogue.Replace("\n", " | ")}");
        return (mapped, unknown);
    }
}
