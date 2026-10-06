using System.IO;
using System.Xml.Linq;
using GenerateTree;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Generátor stromu modulů (tools/GenerateTree): logika nad syntetickými zdroji běží vždy,
/// parita se skutečným <c>Tree.cls</c> jen s legacy repem (viz <see cref="LegacyTreeSourcesFactAttribute"/>).
/// </summary>
public class TreeGeneratorTests
{
    private const string EnumVb = """
        Public Enum EDPackageEnum
            USERS = 5000
            ISYJR = 7000
            TELMAX = 21000
        End Enum
        Public Enum EDISFormEnum
            formUSERSMain = EDPackageEnum.USERS + 1
            <Obsolete("x")> formISYJRList = EDPackageEnum.ISYJR + 2   ' komentář
            formTELMAXMain = EDPackageEnum.TELMAX + 1
            formTISKReportShow = 99001
        End Enum
        """;

    private static XDocument Navbar() => XDocument.Parse("""
        <tree>
          <node id="-2" type="0"><name>Hlavní</name>
            <node id="5000" type="0" licensed="1"><name>Uživatelé</name>
              <node id="5001" type="1"><name>Seznam</name></node>
            </node>
            <node id="7002" type="4" />
          </node>
          <node id="-3" type="0"><name>Systém</name>
            <node id="99001" type="1" input="X=1" hidden="1"><name>Tisk</name><nameEx>Tisk – sestavy</nameEx></node>
          </node>
          <node id="-4" type="0"><name>Partneři</name>
            <node id="21" type="0"><name>Telmax</name>
              <node id="21001" type="1" mlineOnly="1"><name>Telmax</name></node>
            </node>
          </node>
        </tree>
        """);

    private static XDocument Tree() => XDocument.Parse("""
        <tree><node id="7002" type="1" licensed="1"><name>Jízdní řády</name></node></tree>
        """);

    private static string TreeCls(string overrideLine) => $$"""
        Class ALVA.EDISONMAIN2.API.Tree
        {
        ClassMethod GetTree() { }
        /// Generováno z legacy edisonnavbar.xml id=-2 (tools/GenerateTree.cs).
        ClassMethod FillMain() [ Private ]
        {
        {{overrideLine}}
        }
        }
        """;

    private static TreeGeneration Generate(string overrideLine = "", Func<string, bool>? deploy = null) =>
        TreeGenerator.Generate(Navbar(), Tree(), EnumVb, TreeCls(overrideLine), deploy ?? (_ => false));

    [Fact]
    public void Generate_ExeAndInputFromOverride_FqnDerivedFromEnum()
    {
        var result = Generate("\td ..AddExe(\"Seznam\", \"USERS.exe\", \"ALVA.USERS.Stara\", \"5001\", 0, \"AdminMode=1\")");

        Assert.Contains("\t\td ..AddExe(\"Seznam\", \"USERS.exe\", \"ALVA.USERS.formUSERSMain\", \"5001\", 0, \"AdminMode=1\", 0, \"\", 0)", result.Tree);
        Assert.Contains("// FQN ZMENA id=5001: 'ALVA.USERS.Stara' -> 'ALVA.USERS.formUSERSMain'", result.Report);
    }

    [Fact]
    public void Generate_TypeFourReference_ResolvedFromTreeXml_ExeProbedInDeploy()
    {
        // ISY package má vlastní prefix FQN; exe se hledá jen mezi kandidáty legacy LoadAssembly.
        var result = Generate(deploy: name => name == "ISYJR.dll");

        Assert.Contains("\td ..AddExe(\"Jízdní řády\", \"ISYJR.dll\", \"ALVA.ISY.ISYJR.formISYJRList\", \"7002\", 1, \"\", 0, \"\", 0)", result.Tree);
    }

    [Fact]
    public void Generate_NoExeAnywhere_LeavesStub()
    {
        var result = Generate();

        Assert.Contains("\"Jízdní řády\", \"\", \"ALVA.ISY.ISYJR.formISYJRList\"", result.Tree);
        Assert.Equal(4, result.Leaves);
        Assert.Equal(0, result.WithExe);
    }

    [Fact]
    public void Generate_CaseElseForm_InputHiddenAndLongName()
    {
        var result = Generate();

        Assert.Contains("\td ..AddExe(\"Tisk\", \"\", \"ALVA.TISK.frmReportShow\", \"99001\", 0, \"X=1\", 1, \"\", 0, \"Tisk - sestavy\")", result.Tree);
    }

    [Fact]
    public void Generate_TelmaxMovedFromPartnersToMain()
    {
        // EDISONMAIN2 chce Telmax v hlavní nabídce; legacy XML ho má v partnerských řešeních.
        var tree = Generate().Tree!;

        int main = tree.IndexOf("ClassMethod FillMain()", StringComparison.Ordinal);
        int system = tree.IndexOf("ClassMethod FillSystem()", StringComparison.Ordinal);
        int telmax = tree.IndexOf("\"ALVA.TELMAX.formTELMAXMain\"", StringComparison.Ordinal);
        Assert.InRange(telmax, main, system);
        Assert.Contains("\"21001\", 0, \"\", 0, \"\", 1)", tree);   // mlineOnly z XML uzlu
    }

    [Fact]
    public void Generate_KeepsHeadBeforeMarker_ReplacesRestOfClass()
    {
        var tree = Generate("\td ..AddExe(\"Stary\", \"X.exe\", \"Y\", \"1\")").Tree!;

        Assert.StartsWith("Class ALVA.EDISONMAIN2.API.Tree", tree);
        Assert.Contains("ClassMethod GetTree() { }", tree);
        Assert.DoesNotContain("\"Stary\"", tree);
        Assert.EndsWith("}" + Environment.NewLine, tree);
    }

    [Fact]
    public void Generate_MissingMarker_WritesNothing()
    {
        // Smazaný marker kdysi skončil tichým nezapsáním s exit 0 - null drží sync-tree.ps1 před deployem.
        var result = TreeGenerator.Generate(Navbar(), Tree(), EnumVb, "Class X\n{\n}\n", _ => false);

        Assert.Null(result.Tree);
        Assert.Contains("ClassMethod FillMain()", result.FillMethods);
    }

    [Fact]
    public void Fingerprint_IgnoresCarriageReturns()
    {
        // Git dává legacy soubory na Windows jako CRLF a na CI jako LF - hash se musí shodnout.
        Assert.Equal(TreeGenerator.Fingerprint("a\nb\n"u8.ToArray()), TreeGenerator.Fingerprint("a\r\nb\r\n"u8.ToArray()));
    }

    [LegacyTreeSourcesFact]
    public void Generate_FromFingerprintedLegacySources_ReproducesTreeCls()
    {
        // Chytí ruční zásah do generované části Tree.cls i změnu generátoru bez regenerace. Exe se nesonduje:
        // C:\Edison je fakt o stroji, Tree.cls nese exe jako override.
        var root = LegacyTreeSources.Root!;
        string[] sources = TreeGenerator.SourceFiles.Select(rel => Path.Combine(root, rel)).ToArray();
        string treeCls = File.ReadAllText(LegacyTreeSources.RepoPath("EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/Tree.cls"));

        var result = TreeGenerator.Generate(XDocument.Load(sources[0]), XDocument.Load(sources[1]),
            File.ReadAllText(sources[2]), treeCls, _ => false);

        Assert.NotNull(result.Tree);
        Assert.Equal(treeCls.Replace("\r\n", "\n"), result.Tree.Replace("\r\n", "\n"));
    }

    // Formulář EDISONAI leží v EDISONAIForms.dll, sonda ale najde EDISONAI.dll (legacy clsForm.LoadAssembly).
    private static TreeGeneration GenerateEdisonAi(Func<string, string, bool?> typeExists) =>
        TreeGenerator.Generate(
            XDocument.Parse("""
                <tree>
                  <node id="-2" type="0"><name>H</name><node id="277002" type="1"><name>EDISON AI</name></node></node>
                  <node id="-3" type="0"><name>S</name></node>
                  <node id="-4" type="0"><name>P</name><node id="21" type="0"><name>Telmax</name></node></node>
                </tree>
                """),
            XDocument.Parse("<tree/>"),
            """
            Public Enum EDPackageEnum
                EDISONAI = 277000
            End Enum
            Public Enum EDISFormEnum
                formEDISONAIChatHistory = EDPackageEnum.EDISONAI + 2
            End Enum
            """,
            TreeCls(""), name => name is "EDISONAI.dll" or "EDISONAIForms.dll", typeExists);

    [Fact]
    public void Generate_TypeOnlyInSideFormsAssembly_EmitsFormsAssembly()
    {
        var result = GenerateEdisonAi((file, fqn) => fqn == "ALVA.EDISONAI.formEDISONAIChatHistory" && file == "EDISONAIForms.dll");

        Assert.Contains("\td ..AddExe(\"EDISON AI\", \"EDISONAIForms.dll\", \"ALVA.EDISONAI.formEDISONAIChatHistory\", \"277002\"", result.Tree);
    }

    [Fact]
    public void Generate_TypeCheckUnavailable_KeepsProbedAssembly()
    {
        var result = GenerateEdisonAi((_, _) => null);

        Assert.Contains("\"EDISON AI\", \"EDISONAI.dll\", \"ALVA.EDISONAI.formEDISONAIChatHistory\"", result.Tree);
    }

    // Generátor přepisuje GetTypeString z clsConversion.vb: balíček, který legacy přidá jen u sebe, dá ve stromu
    // uzel bez formuláře (EDISON AI jako oranžový stub, i když ho starý shell spustí).
    [LegacyTreeSourcesFact]
    public void PackageLists_MatchLegacyClsConversion()
    {
        string vb = File.ReadAllText(Path.Combine(LegacyTreeSources.Root!, "EDLIBRARY", "clsConversion.vb"));

        Assert.Equal(CasePackages(vb, "Return \"ALVA.\" & package.ToString & \".\" & form.ToString"),
                     TreeGenerator.ConventionPackages.Order());
        Assert.Equal(CasePackages(vb, "Return \"ALVA.ISY.\" & package.ToString & \".\" & form.ToString"),
                     TreeGenerator.IsyPackages.Order());
    }

    // Balíčky z Case větve, která končí daným Return (první výskyt v GetTypeString).
    private static IOrderedEnumerable<string> CasePackages(string vb, string returnLine)
    {
        int ret = vb.IndexOf(returnLine, StringComparison.Ordinal);
        Assert.True(ret > 0, $"V clsConversion.vb chybí '{returnLine}'");
        int caseStart = vb.LastIndexOf("Case ", ret, StringComparison.Ordinal);
        return System.Text.RegularExpressions.Regex.Matches(vb[caseStart..ret], @"EDPackageEnum\.(\w+)")
            .Select(m => m.Groups[1].Value).Distinct().Order();
    }
}

/// <summary>
/// Parita s Tree.cls potřebuje legacy repo EDISON: zapíná ji <c>EDISON_LEGACY_ROOT</c> (pre-push ji nastaví,
/// když repo na stroji je). Drift legacy zdrojů proti <c>tools/tree-sources.sha256</c> = Skipped, ten hlásí hook a CI.
/// </summary>
public sealed class LegacyTreeSourcesFactAttribute : FactAttribute
{
    public LegacyTreeSourcesFactAttribute()
    {
        if (LegacyTreeSources.WhyUnavailable() is { } reason) Skip = reason;
    }
}

internal static class LegacyTreeSources
{
    public static string? Root => Environment.GetEnvironmentVariable("EDISON_LEGACY_ROOT");

    public static string? WhyUnavailable()
    {
        if (string.IsNullOrEmpty(Root)) return "EDISON_LEGACY_ROOT není nastavená - parita stromu potřebuje legacy repo EDISON.";
        if (!Directory.Exists(Root)) return $"EDISON_LEGACY_ROOT={Root} neexistuje.";

        // Volá se při discovery: výjimka tady by shodila celý dotnet test, ne jeden test.
        try
        {
            var expected = File.ReadAllLines(RepoPath("tools/tree-sources.sha256"))
                .Where(l => l.Length > 0 && !l.StartsWith('#'))
                .Select(l => l.Split("  ", 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[1].Trim(), p => p[0]);
            foreach (var rel in TreeGenerator.SourceFiles)
            {
                var path = Path.Combine(Root, rel);
                if (!File.Exists(path) || !expected.TryGetValue(rel, out var hash)
                    || TreeGenerator.Fingerprint(File.ReadAllBytes(path)) != hash)
                    return $"Legacy zdroj {rel} se od Tree.cls pohnul (drift) - regenerace tools/sync-tree.ps1, parita se ověří po ní.";
            }
            return null;
        }
        catch (Exception ex)
        {
            return $"Legacy zdroje stromu nejdou přečíst: {ex.Message}";
        }
    }

    public static string RepoPath(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Kořen repa (EDISON2.sln) nenalezen."), relative);
    }
}
