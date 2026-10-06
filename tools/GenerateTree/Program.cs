// Regenerace Tree.cls z legacy XML: dotnet run --project tools/GenerateTree
// (sync-tree.ps1 navíc vynutí deploy, CI workflow tree-sync otevře PR).
//
// Cesty jde přepsat prostředím, aby generátor šel pustit i na CI runneru, kde žádné
// c:\MLineRepos ani C:\Edison neexistuje. EDISON_LEGACY_ROOT je záměrně stejná
// proměnná, jakou čte .claude/hooks/tree-drift.sh a parity test.

using System.Text;
using System.Xml.Linq;
using GenerateTree;

string edisonRoot = Environment.GetEnvironmentVariable("EDISON_LEGACY_ROOT") ?? @"c:\MLineRepos\EDISON";
string edison2Root = Environment.GetEnvironmentVariable("EDISON2_ROOT") ?? @"c:\MLineRepos\EDISON2";
string deployRoot = Environment.GetEnvironmentVariable("EDISON_DEPLOY_ROOT") ?? @"C:\Edison";

string[] sources = TreeGenerator.SourceFiles.Select(rel => Path.Combine(edisonRoot, rel)).ToArray();
string treeClsPath = Path.Combine(edison2Root, "EDISONMAIN2", "CACHE", "ALVA", "EDISONMAIN2", "API", "Tree.cls");

var result = TreeGenerator.Generate(
    XDocument.Load(sources[0]), XDocument.Load(sources[1]), File.ReadAllText(sources[2]),
    File.ReadAllText(treeClsPath), name => File.Exists(Path.Combine(deployRoot, name)));

foreach (var line in result.Report)
    Console.Error.WriteLine(line);

if (result.Tree is null)
{
    // Nenulový kód zastaví sync-tree.ps1 před deployem i CI krok - nasadila by se stará třída.
    Console.Write(result.FillMethods);
    return 1;
}

File.WriteAllText(treeClsPath, result.Tree, new UTF8Encoding(false));
Console.Error.WriteLine($"// Zapsáno in-place do {treeClsPath}");
WriteSourceFingerprint();

Console.Error.WriteLine($"// Leaves: {result.Leaves}, s formName: {result.WithFqn}, s exePath: {result.WithExe}, " +
                        $"oranžových stubů (bez exe): {result.Leaves - result.WithExe}");
if (result.FqnChanges > 0)
    Console.Error.WriteLine($"// POZOR: {result.FqnChanges} FQN přepsáno podle legacy enumu (viz výše) - ověř exe a spuštění.");
return 0;

// Fingerprint legacy zdrojů pro tree-drift.sh a parity test - bez něj se stale snapshot pozná až tím,
// že modul chybí v aplikaci. Zapisuje se jen po úspěšném splicu, aby nelhal.
void WriteSourceFingerprint()
{
    var lines = new List<string>
    {
        "# Fingerprint legacy zdroju, ze kterych je generovan Tree.cls (tools/GenerateTree).",
        "# Kontroluje .claude/hooks/tree-drift.sh pri startu chatu a parity test. Needitovat rucne.",
        "# Hash je pres obsah BEZ CR (kvuli CRLF/LF rozdilu Windows vs CI): tr -d '\\r' | sha256sum",
    };
    foreach (var rel in TreeGenerator.SourceFiles)
        lines.Add($"{TreeGenerator.Fingerprint(File.ReadAllBytes(Path.Combine(edisonRoot, rel)))}  {rel}");
    // LF a bez BOM: čte to bash hook, CRLF by zůstal v parsovaném hashi.
    string fpPath = Path.Combine(edison2Root, "tools", "tree-sources.sha256");
    File.WriteAllText(fpPath, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
    Console.Error.WriteLine($"// Fingerprint zapsán do {fpPath}");
}
