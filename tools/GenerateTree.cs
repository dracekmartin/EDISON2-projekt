// Generátor stromu modulů pro EDISONMAIN2 z legacy XML.
//
// Spuštění (file-based app, .NET 10):
//   dotnet run tools/GenerateTree.cs
//
// Přečte legacy edisonnavbar.xml (kostra) + edisontree.xml (detail), rekurzivně
// resolve type="4" referencí, a vypíše ObjectScript Fill* metody (DSL volání
// ..AddFamily / ..AddExe / ..AddFamilyEnd) pro vložení do
// EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/Tree.cls.
//
// exe/fqn/inputParam pro již portované moduly se NEgenerují z XML (XML je nemá) —
// vytáhnou se z AKTUÁLNÍHO Tree.cls (override tabulka elementId → exe/fqn/input),
// takže re-run zachová launch info. Neportované leaves = orange stub (#FFA500).
//
// Snapshot — re-run při změně legacy XML.

using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

const string EdisonRoot = @"c:\MLineRepos\EDISON";
const string Edison2Root = @"c:\MLineRepos\EDISON2";
const string StubColor = "#FFA500";

string navbarPath = Path.Combine(EdisonRoot, @"EDCORE\Resources\edisonnavbar.xml");
string treePath = Path.Combine(EdisonRoot, @"EDCORE\Resources\edisontree.xml");
string currentTreeCls = Path.Combine(Edison2Root, @"EDISONMAIN2\CACHE\ALVA\EDISONMAIN2\API\Tree.cls");

// ---- override tabulka: elementId -> (exe, fqn, input) z aktuálního Tree.cls ----
var overrides = ExtractOverrides(File.ReadAllText(currentTreeCls));
Console.Error.WriteLine($"// Override (portované moduly) z Tree.cls: {overrides.Count}");

// ---- index edisontree.xml uzlů podle id ----
var tree = XDocument.Load(treePath);
var byId = new Dictionary<string, XElement>();
foreach (var n in tree.Descendants("node"))
{
    var id = (string?)n.Attribute("id");
    if (id is not null && !byId.ContainsKey(id)) byId[id] = n;
}

// ---- navbar sekce -> Fill* metody ----
var navbar = XDocument.Load(navbarPath);
var sb = new StringBuilder();
EmitSection("-2", "FillMain", "Hlavní nabídka (modulární agenda)");
EmitSection("-3", "FillSystem", "Správa systému (admin nástroje)");
EmitSection("-4", "FillPartners", "Partnerská řešení (3rd party agendy)");
Console.Write(sb.ToString());
return;

void EmitSection(string sectionId, string method, string desc)
{
    var section = navbar.Descendants("node").FirstOrDefault(n => (string?)n.Attribute("id") == sectionId);
    if (section is null) { Console.Error.WriteLine($"// VAROVÁNÍ: sekce {sectionId} nenalezena"); return; }

    sb.AppendLine($"/// Naplní strom \"{NodeName(section)}\" — {desc}.");
    sb.AppendLine($"/// Generováno z legacy edisonnavbar.xml id={sectionId} (tools/GenerateTree.cs).");
    sb.AppendLine($"ClassMethod {method}() [ Private ]");
    sb.AppendLine("{");
    foreach (var child in section.Elements("node"))
        EmitNode(child, 1);
    sb.AppendLine("}");
    sb.AppendLine();
}

// Rekurzivně vyemituje uzel (resolve type=4 do edisontree detailu).
void EmitNode(XElement node, int depth)
{
    // type=4 = reference do edisontree.xml → nahradíme resolved uzlem (děti i atributy z detailu).
    var type = (string?)node.Attribute("type") ?? "0";
    XElement effective = node;
    if (type == "4")
    {
        var id = (string?)node.Attribute("id");
        if (id is null || !byId.TryGetValue(id, out var resolved))
        {
            Console.Error.WriteLine($"// VAROVÁNÍ: type=4 ref id={id} nenalezeno v edisontree.xml — přeskakuji");
            return;
        }
        effective = resolved;
        type = (string?)effective.Attribute("type") ?? "0";
    }

    string indent = new string('\t', depth);
    string name = NodeName(effective);
    string id2 = (string?)effective.Attribute("id") ?? "";

    if (type == "0")
    {
        // skupina → FAMILY
        sb.AppendLine($"{indent}d ..AddFamily({Q(name)}, 0)");
        foreach (var child in effective.Elements("node"))
            EmitNode(child, depth + 1);
        sb.AppendLine($"{indent}d ..AddFamilyEnd()");
    }
    else
    {
        // list (form/CSP) → EXE
        bool licensed = (string?)effective.Attribute("licensed") == "1";
        bool hidden = (string?)effective.Attribute("hidden") == "1";
        string xmlInput = (string?)effective.Attribute("input") ?? "";

        overrides.TryGetValue(id2, out var ov);
        string exe = ov.exe ?? "";
        string fqn = ov.fqn ?? "";
        string input = ov.input is { Length: > 0 } ? ov.input : xmlInput;
        // Neportovaný leaf (bez exe) = orange stub; portovaný = bez barvy.
        string color = exe.Length == 0 ? StubColor : "";

        sb.AppendLine($"{indent}d ..AddExe({Q(name)}, {Q(exe)}, {Q(fqn)}, {Q(id2)}, {(licensed ? 1 : 0)}, {Q(input)}, {(hidden ? 1 : 0)}, {Q(color)})");
    }
}

// <name> dítě (první), trim. Fallback na id.
string NodeName(XElement node)
{
    var name = node.Elements("name").FirstOrDefault()?.Value?.Trim();
    return string.IsNullOrEmpty(name) ? $"#{(string?)node.Attribute("id")}" : name;
}

// ObjectScript string literal — escapuje " → "".
string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

// Vytáhne z Tree.cls override mapu: elementId -> (exe, fqn, input) jen pro neprázdné exe.
static Dictionary<string, (string exe, string fqn, string input)> ExtractOverrides(string cls)
{
    var map = new Dictionary<string, (string, string, string)>();
    // ..AddExe("name", "exe", "fqn", "id", licensed, "input", ...) — 6. arg (input) volitelný.
    var rx = new Regex(
        """\.\.AddExe\(\s*"((?:[^"]|"")*)"\s*,\s*"((?:[^"]|"")*)"\s*,\s*"((?:[^"]|"")*)"\s*,\s*"((?:[^"]|"")*)"(?:\s*,\s*[01])?(?:\s*,\s*"((?:[^"]|"")*)")?""");
    foreach (Match m in rx.Matches(cls))
    {
        string exe = m.Groups[2].Value.Replace("\"\"", "\"");
        if (exe.Length == 0) continue;
        string fqn = m.Groups[3].Value.Replace("\"\"", "\"");
        string id = m.Groups[4].Value;
        string input = m.Groups[5].Success ? m.Groups[5].Value.Replace("\"\"", "\"") : "";
        map[id] = (exe, fqn, input);
    }
    return map;
}
