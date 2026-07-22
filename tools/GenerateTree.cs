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
// formName (FQN) se odvozuje AUTOMATICKY z elementId — port legacy
// EDConversion.Form.GetTypeString (clsConversion.vb) nad jmény členů enumů
// EDISFormEnum / EDPackageEnum (parsované z EDFormInterfaceLic.vb). Stejná čistá
// funkce, kterou legacy používá při spouštění formy, takže FQN je korektní všude,
// kde legacy umí formu otevřít.
//
// exePath se spolehlivě z dat odvodit NELZE (je to fakt o nasazení — .exe vs .dll,
// bundling, subadresáře). Proto: (1) ručně ověřený override z aktuálního Tree.cls
// má přednost; (2) jinak best-effort sonda do C:\Edison (legacy LoadAssembly probe
// pořadí seg1+seg2 / seg2 / seg1, .exe pak .dll) — emituje se jen když soubor reálně
// existuje; (3) jinak "" → runtime ModuleLaunchabilityCheck ho zobrazí jako oranžový
// stub. Žádná fabrikace exe, která by spadla při kliknutí.
//
// Snapshot — re-run při změně legacy XML nebo enumů.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

const string EdisonRoot = @"c:\MLineRepos\EDISON";
const string Edison2Root = @"c:\MLineRepos\EDISON2";
const string DeployRoot = @"C:\Edison";

string navbarPath = Path.Combine(EdisonRoot, @"EDCORE\Resources\edisonnavbar.xml");
string treePath = Path.Combine(EdisonRoot, @"EDCORE\Resources\edisontree.xml");
string enumPath = Path.Combine(EdisonRoot, @"EDCORE\EDFormInterfaceLic.vb");
string currentTreeCls = Path.Combine(Edison2Root, @"EDISONMAIN2\CACHE\ALVA\EDISONMAIN2\API\Tree.cls");

// ---- override tabulka: elementId -> (exe, fqn, input) z aktuálního Tree.cls ----
// Drží ručně ověřené launch info (přednost před auto-derivací — nikdy neregresuje).
var overrides = ExtractOverrides(File.ReadAllText(currentTreeCls));
Console.Error.WriteLine($"// Override (ručně ověřené moduly) z Tree.cls: {overrides.Count}");

// ---- enum mapy z EDFormInterfaceLic.vb: id -> formName, packageValue -> packageName ----
var (formNames, packageNames) = ParseEnums(File.ReadAllText(enumPath));
Console.Error.WriteLine($"// EDISFormEnum: {formNames.Count} forem, EDPackageEnum: {packageNames.Count} packages");

// Konvenční packages → "ALVA.<package>.<form>" (clsConversion.vb řádky 16-158).
var conventionPackages = new HashSet<string>(StringComparer.Ordinal)
{
    "WFAPPLICATION", "MKTEST", "ZZTEST", "USERS", "USERS2", "FIRMA", "FIRMA2",
    "JRSETTINGS", "JRZAST", "JRLIC", "ZAKAZNICI2", "DOTACE", "ADDISPECING",
    "ADOBJEDNAVKY", "ADPLANDOPRAVY", "ADPLAN", "ADSETTINGS", "ADTURNUSY",
    "ADZPRACOVANI", "ADSERVIS", "DSSETUP", "DSREPORT", "DSVIEWER", "DSINPUT",
    "MZDY", "MZDY2", "JRTABULE", "VOZIDLA2", "DSPODILOVANI", "ESMAIN", "ESMAINEX",
    "ESREPORT", "ESSALDO", "ESUI", "ESUTILITY", "ESFINANCOVANI", "ESDOKLADY",
    "ESKOMPENZACE", "TICKETS", "REPORTGENERATOR", "FIRMACIS", "AURIS", "SHERLOCK",
    "UDALOSTI", "TM", "MLRESTTOOLS", "MLSERVERTEST", "LICENCEINFO", "AUTOOPRAVNA",
    "AUTOOPRAVNA2", "PREVODYAD", "PREVODYVOZIDLA", "PREVODYZAKAZNICI", "PREVODYES",
    "PREVODYAUTOOPRAVNA", "PREVODYDS", "PREVODYMHD", "PREVODYVLAKY", "PREVODYSH",
    "PREVODYND", "TMSYSTEM", "ECM", "MULTISERVER", "UTILITIES", "JRMAIN", "JRVLAK",
    "EDISONMAIN", "POKLADNA", "POKLADNAEX", "POKLADNANAST", "FAKTURACE",
    "FAKTURACEMAIN", "FAKTURACEEX", "PREPRAVNIPOKLADNA", "TREES", "FEEDBACK",
    "AUDIT", "BCCLEARING", "BCSTANDARD", "BCOPTIM1", "BCCENAJIZDNEHO", "BCINTDOP",
    "BCVRDOC", "BCNAVAZN", "TELMAX", "TELMAX2", "SKLADY", "SKLADYREPORT",
    "SKLADYMAIN", "SHMAIN", "MTZ", "GPSGOOGLE", "GPSGREATMAPS", "GPSSEZNAM",
    "TISKMHD", "PDFVIEWER", "ADCALCULATOR", "HIM", "HIMFUNDING", "DOCHAZKA",
    "FAKREPORT", "TISKJR", "TISKJRRELACE", "EVIDENCEVYBEROVYCHRIZENI", "ARES",
    "PARKKARTY", "TICKETSEXT", "FSSOFT", "EDREPORTS", "EDIMPORT",
    "ADPARTNERSKARESENI", "EDHELPDESK", "NAKLADNIDOPRAVA", "NDOBJEDNAVKY",
    "PRODEJ", "ISDSFRONTEND", "ISIR", "EETMONITOR", "EMSERVISIMPORT", "MO",
    "MHDZASTAVKY", "MHDCHRONOMETRAZE", "MHDMAIN", "MHDLINKY", "MHDDEFINICEKALENDARE",
    "MHDZPRACOVANI", "PHLASENI", "OBJEDNAVKY", "VLAKY", "VLAKY2", "VLAKYPLAN",
    "VLAKYPLANY", "VLAKYSOUPRAVY", "VLAKYNASTAVENI", "DEKZ", "GENERAL", "EDISONX",
    "PREVODYAD2", "JIRA", "IMPORT", "MAPY", "KONTROLNISEZNAMY", "TRANSDEV",
};

// ISY packages → "ALVA.ISY.<package>.<form>".
var isyPackages = new HashSet<string>(StringComparer.Ordinal)
{
    "ISYZastavky", "ISYCiselniky", "ISYObehy", "ISYNavaznosti", "ISYJR",
    "ISYPlanDopravy", "ISYObjednavky", "ISYTurnusy", "ISYVlaky", "ISYZpracovani",
    "ISYJBN", "ISYADNasCis", "ISYCloud", "ISYJR2",
};

// ---- index edisontree.xml uzlů podle id ----
var tree = XDocument.Load(treePath);
var byId = new Dictionary<string, XElement>();
foreach (var n in tree.Descendants("node"))
{
    var id = (string?)n.Attribute("id");
    if (id is not null && !byId.ContainsKey(id)) byId[id] = n;
}

// ---- statistika (report na stderr po vygenerování) ----
int leaves = 0, withFqn = 0, withExe = 0, mismatchCount = 0;

// ---- navbar sekce -> Fill* metody ----
var navbar = XDocument.Load(navbarPath);

// EDISONMAIN2 chce Telmax v hlavní nabídce, ne v partnerských řešeních. Legacy XML
// zůstává beze změny (sdílí ho starý EDISON) — přesun dělá generátor in-memory.
MoveNode("21", fromSection: "-4", toSection: "-2");

var sb = new StringBuilder();
EmitSection("-2", "FillMain", "Hlavní nabídka (modulární agenda)");
EmitSection("-3", "FillSystem", "Správa systému (admin nástroje)");
EmitSection("-4", "FillPartners", "Partnerská řešení (3rd party agendy)");

// Splice vygenerovaných Fill* metod do Tree.cls in-place: hlava (GetTree + Add* helpery)
// zůstává, blok od první "/// Naplní strom" po konec třídy se nahradí. Zachovává UTF-8 bez BOM.
string clsText = File.ReadAllText(currentTreeCls);
int idx = clsText.IndexOf("/// Naplní strom", StringComparison.Ordinal);
if (idx < 0)
{
    Console.Error.WriteLine("// CHYBA: marker '/// Naplní strom' v Tree.cls nenalezen — zapisuji na stdout místo in-place.");
    Console.Write(sb.ToString());
}
else
{
    string result = clsText[..idx] + sb.ToString() + "}" + Environment.NewLine;
    File.WriteAllText(currentTreeCls, result, new UTF8Encoding(false));
    Console.Error.WriteLine($"// Zapsáno in-place do {currentTreeCls}");
}

Console.Error.WriteLine($"// Leaves: {leaves}, s formName: {withFqn}, s exePath: {withExe}, " +
                        $"oranžových stubů (bez exe): {leaves - withExe}");
if (mismatchCount > 0)
    Console.Error.WriteLine($"// POZOR: {mismatchCount} ručně ověřených FQN se liší od auto-derivace (viz výše).");
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

    // mlineOnly je per-UMÍSTĚNÍ atribut (jako input) — vždy z XML uzlu, nikdy z override.
    bool mlineOnly = (string?)effective.Attribute("mlineOnly") == "1";

    if (type == "0")
    {
        // skupina → FAMILY. elementId (package ID) + licensed z legacy XML — legacy
        // BuildTreeXML gate-oval i skupiny proti licenci/accessTable, klient to replikuje.
        bool famLicensed = (string?)effective.Attribute("licensed") == "1";
        sb.AppendLine($"{indent}d ..AddFamily({Q(name)}, 0, {Q(id2)}, {(famLicensed ? 1 : 0)}, {(mlineOnly ? 1 : 0)})");
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

        // formName: auto-derivace z elementId; ručně ověřený override má přednost.
        long.TryParse(id2, out long idNum);
        string derivedFqn = GetTypeString(idNum);
        string fqn = !string.IsNullOrEmpty(ov.fqn) ? ov.fqn : derivedFqn;

        // Pojistka: hlas, když se ručně ověřené FQN liší od auto-derivace (drift v portu / enumech).
        if (!string.IsNullOrEmpty(ov.fqn) && !string.IsNullOrEmpty(derivedFqn) && ov.fqn != derivedFqn)
        {
            Console.Error.WriteLine($"// MISMATCH id={id2}: override='{ov.fqn}' vs derived='{derivedFqn}'");
            mismatchCount++;
        }

        // exePath: ručně ověřený override má přednost; jinak best-effort sonda (jen pokud soubor existuje).
        string exe = !string.IsNullOrEmpty(ov.exe) ? ov.exe : ResolveExe(fqn);
        // input je per-UMÍSTĚNÍ (stejné id může být ve stromu vícekrát s jiným inputem —
        // Události agd=..., Převody AD/AO/MHD/ND), override mapa je ale per-id (poslední
        // výskyt vyhrává) → XML input má přednost, override jen doplní chybějící.
        string input = xmlInput.Length > 0 ? xmlInput : ov.input ?? "";
        // Barvu (stub/broken) NEautorujeme — odvozuje ji runtime ModuleLaunchabilityCheck
        // z exe/launchovatelnosti. Tree.cls = čistá data.
        string color = "";

        leaves++;
        if (fqn.Length > 0) withFqn++;
        if (exe.Length > 0) withExe++;

        sb.AppendLine($"{indent}d ..AddExe({Q(name)}, {Q(exe)}, {Q(fqn)}, {Q(id2)}, {(licensed ? 1 : 0)}, {Q(input)}, {(hidden ? 1 : 0)}, {Q(color)}, {(mlineOnly ? 1 : 0)})");
    }
}

// Přesune uzel mezi navbar sekcemi; do cílové vloží abecedně (cs-CZ) dle resolved názvu.
void MoveNode(string nodeId, string fromSection, string toSection)
{
    var from = navbar.Descendants("node").First(n => (string?)n.Attribute("id") == fromSection);
    var to = navbar.Descendants("node").First(n => (string?)n.Attribute("id") == toSection);
    var node = from.Elements("node").First(n => (string?)n.Attribute("id") == nodeId);
    node.Remove();

    var cs = CultureInfo.GetCultureInfo("cs-CZ").CompareInfo;
    string name = ResolvedName(node);
    var next = to.Elements("node").FirstOrDefault(n => cs.Compare(ResolvedName(n), name) > 0);
    if (next is not null) next.AddBeforeSelf(node); else to.Add(node);
}

// Název uzlu; type=4 referenci resolvuje do edisontree.xml.
string ResolvedName(XElement node) =>
    (string?)node.Attribute("type") == "4"
        && byId.TryGetValue((string?)node.Attribute("id") ?? "", out var resolved)
        ? NodeName(resolved) : NodeName(node);

// <name> dítě (první), trim. Fallback na id.
string NodeName(XElement node)
{
    var name = node.Elements("name").FirstOrDefault()?.Value?.Trim();
    return string.IsNullOrEmpty(name) ? $"#{(string?)node.Attribute("id")}" : name;
}

// ObjectScript string literal — escapuje " → "".
string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

// Port legacy EDConversion.Form.GetTypeString(id) — id → FQN třídy formy.
// Závisí jen na jménech členů enumů (formName, packageName), které legacy zřetězuje
// dle konvence package. Vrací "" pro neznámé id / package (neportovaná agenda).
string GetTypeString(long id)
{
    if (!formNames.TryGetValue(id, out var form)) return "";
    long pkgVal = (id / 1000) * 1000;            // VB: (form \ 1000) * 1000 (trunc k nule)
    if (!packageNames.TryGetValue(pkgVal, out var pkg)) return CaseElse(form);

    // Konvenční packages → "ALVA.<package>.<form>".
    if (conventionPackages.Contains(pkg)) return $"ALVA.{pkg}.{form}";

    // Speciální prefixy (viz Select Case v clsConversion.vb).
    if (pkg == "EDFORMS") return $"ALVA.EDFORMS.FORMS.{form}";
    if (isyPackages.Contains(pkg)) return $"ALVA.ISY.{pkg}.{form}";
    return pkg switch
    {
        "MHD2Ciselniky" => $"ALVA.MHD2.Ciselniky.{form}",
        "MHD2Linky" => $"ALVA.MHD2.Linky.{form}",
        "MHD2Ridici" => $"ALVA.MHD2.Ridici.{form}",
        "MHD2PlanDopravy" => $"ALVA.MHD2.PlanDopravy.{form}",
        "MAPY2" => $"ALVA.MAPY2.{form}",
        "ZAUCTOVANIMzdy" => $"ALVA.ZAUCTOVANI.ZauMzdy.{form}",
        "WORDGENERATOR" => $"ALVA.WORDGENERATOR.{form}",
        "EdisonWeb" => $"ALVA.EDISONWEB.{form}",
        "DISPECINK2" => $"ALVA.DISPECINK2.{form}",
        _ => CaseElse(form),
    };
}

// Case Else z clsConversion.vb — formy, jejichž třída neodpovídá konvenci package.
string CaseElse(string form) => form switch
{
    "formTISKPredtiskyShow" => "ALVA.TISK.frmPredtiskShow",
    "formTISKReportShow" => "ALVA.TISK.frmReportShow",
    "formTISKTableShow" => "ALVA.TISK.frmTableShow",
    "formTISKInformaceShow" => "ALVA.TISK.frmInfo",
    "formRIDICIList" => "ALVA.RIDICI.frmRidiciList",
    "formRIDICIEdit" => "ALVA.RIDICI.frmRidiciEdit",
    "formRIDICIPomuckyList" => "ALVA.RIDICI.frmRidiciPomuckyList",
    "formRIDICIPomuckyEdit" => "ALVA.RIDICI.frmRidiciPomuckyEdit",
    "formRIDICIProhlidkyList" => "ALVA.RIDICI.frmRidiciLPList",
    "formRIDICIProhlidkyEdit" => "ALVA.RIDICI.frmRidiciLPEdit",
    "formRIDICIPrukazkyList" => "ALVA.RIDICI.frmRidiciPrukazkyList",
    "formRIDICIPrukazkyEdit" => "ALVA.RIDICI.frmRidiciPrukazkyEdit",
    "formRIDICISkoleniList" => "ALVA.RIDICI.frmRidiciSkoleniList",
    "formRIDICISkoleniEdit" => "ALVA.RIDICI.frmRidiciSkoleniEdit",
    "formGEXPLORERShow" => "ALVA.GEXPLORER.frmEditGlobals",
    "formSETUPWIZARDSelectProject" => "ALVA.SETUPWIZARD.frmSelectProject",
    "formSETUPWIZARDMain" => "ALVA.SETUPWIZARD.frmMain",
    "formSETUPMain" => "ALVA.SETUP.frmMain",
    "formPREDTISKYTVORBASABLONShow" => "ALVA.PREDTISKYTVORBASABLON.frmPredtiskyTvorbaSablon",
    _ => "",
};

// exePath best-effort: replikuje legacy LoadAssembly probe (seg1+seg2 / seg2 / seg1),
// ale místo trial-loadu jen ověří existenci souboru v deploy rootu. Vrací "" když nenajde
// → strom zobrazí oranžový stub. Žádná fabrikace neexistujícího exe.
string ResolveExe(string fqn)
{
    if (string.IsNullOrEmpty(fqn)) return "";
    var seg = fqn.Split('.');
    if (seg.Length < 3) return "";

    var candidates = new List<string> { seg[1] + seg[2], seg[2], seg[1] };
    foreach (var c in candidates)
        foreach (var ext in new[] { ".exe", ".dll" })
            if (File.Exists(Path.Combine(DeployRoot, c + ext)))
                return c + ext;
    return "";
}

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

// Naparsuje EDPackageEnum + EDISFormEnum z EDFormInterfaceLic.vb.
// EDPackageEnum: NAME = literal (multiply of 1000). EDISFormEnum: NAME = EDPackageEnum.PKG ± N.
// Vrací (id -> formName, packageValue -> packageName).
static (Dictionary<long, string> forms, Dictionary<long, string> packages) ParseEnums(string vb)
{
    var packages = new Dictionary<string, long>(StringComparer.Ordinal);   // name -> value (pro resolve)
    var packagesByValue = new Dictionary<long, string>();
    var forms = new Dictionary<long, string>();

    string pkgBlock = EnumBlock(vb, "EDPackageEnum");
    string formBlock = EnumBlock(vb, "EDISFormEnum");

    // EDPackageEnum: literály i odkazy (NAME = OTHER ± N). Dvě pasáže pro odkazy.
    var pending = new List<(string name, string expr)>();
    foreach (var (name, expr) in EnumMembers(pkgBlock))
    {
        if (TryEvalLiteral(expr, out long v)) { packages[name] = v; packagesByValue.TryAdd(v, name); }
        else pending.Add((name, expr));
    }
    foreach (var (name, expr) in pending)
        if (TryEvalExpr(expr, packages, out long v)) { packages[name] = v; packagesByValue.TryAdd(v, name); }

    // EDISFormEnum: NAME = EDPackageEnum.PKG ± N (nebo literál).
    foreach (var (name, expr) in EnumMembers(formBlock))
        if (TryEvalExpr(expr, packages, out long v)) forms.TryAdd(v, name);

    return (forms, packagesByValue);
}

// Vyřízne tělo "Enum <name> ... End Enum".
static string EnumBlock(string vb, string name)
{
    var m = Regex.Match(vb, $@"\bEnum\s+{Regex.Escape(name)}\b(.*?)\bEnd\s+Enum\b",
        RegexOptions.Singleline);
    return m.Success ? m.Groups[1].Value : "";
}

// Vrací (memberName, valueExpr) pro každý řádek enumu (přeskakuje komentáře / atributy bez "=").
static IEnumerable<(string name, string expr)> EnumMembers(string block)
{
    foreach (var raw in block.Split('\n'))
    {
        string line = raw;
        int c = line.IndexOf('\'');                 // strip line comment
        if (c >= 0) line = line[..c];
        line = line.Trim();
        if (line.Length == 0) continue;
        line = Regex.Replace(line, @"^<[^>]*>\s*", ""); // strip <Obsolete(...)> apod.
        var m = Regex.Match(line, @"^([A-Za-z_]\w*)\s*=\s*(.+)$");
        if (m.Success) yield return (m.Groups[1].Value, m.Groups[2].Value.Trim());
    }
}

static bool TryEvalLiteral(string expr, out long value) =>
    long.TryParse(expr.Trim(), out value);

// Vyhodnotí "EDPackageEnum.PKG ± N", "EDPackageEnum.PKG", "OTHERNAME ± N" nebo literál.
static bool TryEvalExpr(string expr, Dictionary<string, long> packages, out long value)
{
    value = 0;
    expr = expr.Trim();
    if (TryEvalLiteral(expr, out value)) return true;

    // strip kvalifikátory (EDFormInterface. / CORE.EDCoreInterface. / EDPackageEnum.)
    var m = Regex.Match(expr, @"(?:[\w.]*\.)?([A-Za-z_]\w*)\s*(?:([+\-])\s*(\d+))?$");
    if (!m.Success) return false;
    if (!packages.TryGetValue(m.Groups[1].Value, out long baseVal)) return false;
    value = baseVal;
    if (m.Groups[2].Success)
        value += (m.Groups[2].Value == "-" ? -1 : 1) * long.Parse(m.Groups[3].Value);
    return true;
}
