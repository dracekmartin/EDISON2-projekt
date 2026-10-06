// Generátor stromu modulů pro EDISONMAIN2 z legacy XML - čistá logika bez souborů (IO drží Program.cs).
//
// Přečte legacy edisonnavbar.xml (kostra) + edisontree.xml (detail), rekurzivně
// resolve type="4" referencí, a vypíše ObjectScript Fill* metody (DSL volání
// ..AddFamily / ..AddExe / ..AddFamilyEnd) pro vložení do
// EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/Tree.cls.
//
// formName (FQN) se odvozuje AUTOMATICKY z elementId (přednost před FQN v Tree.cls) - port legacy
// EDConversion.Form.GetTypeString (clsConversion.vb) nad jmény členů enumů
// EDISFormEnum / EDPackageEnum (parsované z EDFormInterfaceLic.vb). Stejná čistá
// funkce, kterou legacy používá při spouštění formy, takže FQN je korektní všude,
// kde legacy umí formu otevřít.
//
// exePath se spolehlivě z dat odvodit NELZE (je to fakt o nasazení - .exe vs .dll,
// bundling, subadresáře). Proto: (1) ručně ověřený override z aktuálního Tree.cls
// má přednost; (2) jinak best-effort sonda do deploy adresáře (legacy LoadAssembly probe
// pořadí seg1+seg2 / seg2 / seg1, .exe pak .dll) - emituje se jen když soubor reálně
// existuje; (3) jinak "" → runtime ModuleLaunchabilityCheck ho zobrazí jako oranžový
// stub. Žádná fabrikace exe, která by spadla při kliknutí.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GenerateTree;

/// <summary>Výsledek běhu: <see cref="Tree"/> null = splice marker v Tree.cls chybí a nic se nezapíše.</summary>
internal sealed record TreeGeneration(string? Tree, string FillMethods, IReadOnlyList<string> Report, int Leaves, int WithFqn, int WithExe, int FqnChanges);

internal sealed class TreeGenerator
{
    // Splice anchor generovaného bloku. Záměrně provenance, ne WHAT narrace - předchozí
    // anchor smazal komentářový audit jako opis jména metody a tím generátor tiše rozbil.
    internal const string Marker = "/// Generováno z legacy edisonnavbar.xml id=";

    /// <summary>Legacy zdroje relativně ke kořeni repa EDISON; pořadí drží i fingerprint.</summary>
    internal static readonly string[] SourceFiles =
        ["EDCORE/Resources/edisonnavbar.xml", "EDCORE/Resources/edisontree.xml", "EDCORE/EDFormInterfaceLic.vb"];

    // Konvenční packages → "ALVA.<package>.<form>" (clsConversion.vb GetTypeString); shodu s legacy hlídá
    // TreeGeneratorTests.PackageLists_MatchLegacyClsConversion.
    internal static readonly HashSet<string> ConventionPackages = new(StringComparer.Ordinal)
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
        "AUDO", "DISPECINK", "DOKUMENTY", "EDISONAI", "EKOSYSIISSP", "GISTEXPORT", "GTAB",
        "JIKORDPOKLADNA", "MZDYEX", "NDMAIN", "PDFVIEWEREX", "PREMIE", "PREPRAVNIKONTROLA",
        "PREPRAVNIKONTROLA2", "SKODY", "SUPPORT", "VOZIDLA", "ZAKAZNICI", "ZAKLISTY",
    };

    // ISY packages → "ALVA.ISY.<package>.<form>".
    internal static readonly HashSet<string> IsyPackages = new(StringComparer.Ordinal)
    {
        "ISYZastavky", "ISYCiselniky", "ISYObehy", "ISYNavaznosti", "ISYJR",
        "ISYPlanDopravy", "ISYObjednavky", "ISYTurnusy", "ISYVlaky", "ISYZpracovani",
        "ISYJBN", "ISYADNasCis", "ISYCloud", "ISYJR2",
    };

    private readonly XDocument _navbar;
    private readonly Dictionary<string, XElement> _byId = new();
    private readonly Dictionary<long, string> _formNames;
    private readonly Dictionary<long, string> _packageNames;
    private readonly Dictionary<string, (string exe, string fqn, string input)> _overrides;
    private readonly Func<string, bool> _deployFileExists;
    private readonly Func<string, string, bool?> _deployTypeExists;
    private readonly StringBuilder _sb = new();
    private readonly List<string> _report = [];
    private int _leaves, _withFqn, _withExe, _mismatchCount;

    private TreeGenerator(XDocument navbar, XDocument tree, string enumVb, string currentTreeCls,
                          Func<string, bool> deployFileExists, Func<string, string, bool?> deployTypeExists)
    {
        // Kopie: přesun uzlu (MoveNode) nesmí měnit dokument volajícího.
        _navbar = new XDocument(navbar);
        _deployFileExists = deployFileExists;
        _deployTypeExists = deployTypeExists;

        // ---- override tabulka: elementId -> (exe, fqn, input) z aktuálního Tree.cls ----
        // exe a input mají přednost (z dat neodvoditelné), fqn jen jako fallback za derivací.
        _overrides = ExtractOverrides(currentTreeCls);
        _report.Add($"// Override (exe/input/fqn z minulého běhu) z Tree.cls: {_overrides.Count}");

        // ---- enum mapy z EDFormInterfaceLic.vb: id -> formName, packageValue -> packageName ----
        (_formNames, _packageNames) = ParseEnums(enumVb);
        _report.Add($"// EDISFormEnum: {_formNames.Count} forem, EDPackageEnum: {_packageNames.Count} packages");

        // ---- index edisontree.xml uzlů podle id ----
        foreach (var n in tree.Descendants("node"))
        {
            var id = (string?)n.Attribute("id");
            if (id is not null && !_byId.ContainsKey(id)) _byId[id] = n;
        }
    }

    /// <summary>
    /// Vygeneruje Fill* metody a vloží je do <paramref name="currentTreeCls"/> místo bloku od prvního
    /// <see cref="Marker"/> po konec třídy. <paramref name="deployFileExists"/> = sonda exe v deploy adresáři,
    /// <paramref name="deployTypeExists"/> = je typ (FQN) v daném souboru deploy adresáře; null = nejde zjistit.
    /// </summary>
    public static TreeGeneration Generate(XDocument navbar, XDocument tree, string enumVb, string currentTreeCls,
                                          Func<string, bool> deployFileExists,
                                          Func<string, string, bool?>? deployTypeExists = null)
    {
        var gen = new TreeGenerator(navbar, tree, enumVb, currentTreeCls, deployFileExists,
                                    deployTypeExists ?? ((_, _) => null));
        return gen.Run(currentTreeCls);
    }

    /// <summary>SHA-256 obsahu bez CR - git checkoutuje legacy soubory na Windows jako CRLF a na CI jako LF.</summary>
    public static string Fingerprint(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content.Where(b => b != (byte)'\r').ToArray())).ToLowerInvariant();

    private TreeGeneration Run(string currentTreeCls)
    {
        // EDISONMAIN2 chce Telmax v hlavní nabídce, ne v partnerských řešeních. Legacy XML
        // zůstává beze změny (sdílí ho starý EDISON) - přesun dělá generátor in-memory.
        MoveNode("21", fromSection: "-4", toSection: "-2");

        EmitSection("-2", "FillMain");
        EmitSection("-3", "FillSystem");
        EmitSection("-4", "FillPartners");

        string fill = _sb.ToString();
        int idx = currentTreeCls.IndexOf(Marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            // Marker už jednou smazal komentářový audit jako opis jména metody; generátor tehdy
            // skončil s exit 0 a tiše nezapsal. Null zastaví zápis i sync-tree.ps1 před deployem.
            _report.Add($"// CHYBA: marker '{Marker}' v Tree.cls nenalezen - NEZAPSANO, vypisuji na stdout.");
            return new TreeGeneration(null, fill, _report, _leaves, _withFqn, _withExe, _mismatchCount);
        }

        string result = currentTreeCls[..idx] + fill + "}" + Environment.NewLine;
        return new TreeGeneration(result, fill, _report, _leaves, _withFqn, _withExe, _mismatchCount);
    }

    private void EmitSection(string sectionId, string method)
    {
        var section = _navbar.Descendants("node").FirstOrDefault(n => (string?)n.Attribute("id") == sectionId);
        if (section is null) { _report.Add($"// VAROVÁNÍ: sekce {sectionId} nenalezena"); return; }

        // Text je otisk v Tree.cls: jeho změna mění třídu a znamená regeneraci a atomický deploy stromu.
        _sb.AppendLine($"{Marker}{sectionId} (tools/GenerateTree.cs).");
        _sb.AppendLine($"ClassMethod {method}() [ Private ]");
        _sb.AppendLine("{");
        foreach (var child in section.Elements("node"))
            EmitNode(child, 1);
        _sb.AppendLine("}");
        _sb.AppendLine();
    }

    // Rekurzivně vyemituje uzel (resolve type=4 do edisontree detailu).
    private void EmitNode(XElement node, int depth)
    {
        // type=4 = reference do edisontree.xml → nahradíme resolved uzlem (děti i atributy z detailu).
        var type = (string?)node.Attribute("type") ?? "0";
        XElement effective = node;
        if (type == "4")
        {
            var id = (string?)node.Attribute("id");
            if (id is null || !_byId.TryGetValue(id, out var resolved))
            {
                _report.Add($"// VAROVÁNÍ: type=4 ref id={id} nenalezeno v edisontree.xml - přeskakuji");
                return;
            }
            effective = resolved;
            type = (string?)effective.Attribute("type") ?? "0";
        }

        string indent = new('\t', depth);
        string name = NodeName(effective);
        string id2 = (string?)effective.Attribute("id") ?? "";

        // mlineOnly je per-UMÍSTĚNÍ atribut (jako input) - vždy z XML uzlu, nikdy z override.
        bool mlineOnly = (string?)effective.Attribute("mlineOnly") == "1";

        if (type == "0")
        {
            // skupina → FAMILY. elementId (package ID) + licensed z legacy XML - legacy
            // BuildTreeXML gate-oval i skupiny proti licenci/accessTable, klient to replikuje.
            bool famLicensed = (string?)effective.Attribute("licensed") == "1";
            _sb.AppendLine($"{indent}d ..AddFamily({Q(name)}, 0, {Q(id2)}, {(famLicensed ? 1 : 0)}, {(mlineOnly ? 1 : 0)})");
            foreach (var child in effective.Elements("node"))
                EmitNode(child, depth + 1);
            _sb.AppendLine($"{indent}d ..AddFamilyEnd()");
        }
        else
        {
            // list (form/CSP) → EXE
            bool licensed = (string?)effective.Attribute("licensed") == "1";
            bool hidden = (string?)effective.Attribute("hidden") == "1";
            string xmlInput = (string?)effective.Attribute("input") ?? "";

            _overrides.TryGetValue(id2, out var ov);

            // formName: derivace z enumu má přednost - override je vlastní výstup minulého běhu,
            // takže by přejmenování formy v legacy zamrazil navždy. Override jen když derivace mlčí.
            long.TryParse(id2, out long idNum);
            string derivedFqn = GetTypeString(idNum);
            string fqn = !string.IsNullOrEmpty(derivedFqn) ? derivedFqn : ov.fqn ?? "";

            if (!string.IsNullOrEmpty(ov.fqn) && !string.IsNullOrEmpty(derivedFqn) && ov.fqn != derivedFqn)
            {
                _report.Add($"// FQN ZMENA id={id2}: '{ov.fqn}' -> '{derivedFqn}'");
                _mismatchCount++;
            }

            // exePath: ručně ověřený override má přednost; jinak best-effort sonda (jen pokud soubor existuje).
            string exe = !string.IsNullOrEmpty(ov.exe) ? ov.exe : ResolveExe(fqn);
            // input je per-UMÍSTĚNÍ (stejné id může být ve stromu vícekrát s jiným inputem -
            // Události agd=..., Převody AD/AO/MHD/ND), override mapa je ale per-id (poslední
            // výskyt vyhrává) → XML input má přednost, override jen doplní chybějící.
            string input = xmlInput.Length > 0 ? xmlInput : ov.input ?? "";
            // Barvu (stub/broken) NEautorujeme - odvozuje ji runtime ModuleLaunchabilityCheck
            // z exe/launchovatelnosti. Tree.cls = čistá data.
            string color = "";

            _leaves++;
            if (fqn.Length > 0) _withFqn++;
            if (exe.Length > 0) _withExe++;

            // Dlouhý název je poslední (volitelný) argument - emituje se jen když v XML je,
            // ať se do generovaného Tree.cls nesypou stovky prázdných řetězců.
            string longName = NodeNameEx(effective, name);
            string longNameArg = longName.Length > 0 ? $", {Q(longName)}" : "";

            _sb.AppendLine($"{indent}d ..AddExe({Q(name)}, {Q(exe)}, {Q(fqn)}, {Q(id2)}, {(licensed ? 1 : 0)}, {Q(input)}, {(hidden ? 1 : 0)}, {Q(color)}, {(mlineOnly ? 1 : 0)}{longNameArg})");
        }
    }

    // Přesune uzel mezi navbar sekcemi; do cílové vloží abecedně (cs-CZ) dle resolved názvu.
    private void MoveNode(string nodeId, string fromSection, string toSection)
    {
        var from = _navbar.Descendants("node").First(n => (string?)n.Attribute("id") == fromSection);
        var to = _navbar.Descendants("node").First(n => (string?)n.Attribute("id") == toSection);
        var node = from.Elements("node").First(n => (string?)n.Attribute("id") == nodeId);
        node.Remove();

        var cs = CultureInfo.GetCultureInfo("cs-CZ").CompareInfo;
        string name = ResolvedName(node);
        var next = to.Elements("node").FirstOrDefault(n => cs.Compare(ResolvedName(n), name) > 0);
        if (next is not null) next.AddBeforeSelf(node); else to.Add(node);
    }

    // Název uzlu; type=4 referenci resolvuje do edisontree.xml.
    private string ResolvedName(XElement node) =>
        (string?)node.Attribute("type") == "4"
            && _byId.TryGetValue((string?)node.Attribute("id") ?? "", out var resolved)
            ? NodeName(resolved) : NodeName(node);

    // <name> dítě (první), trim. Fallback na id.
    private static string NodeName(XElement node)
    {
        var name = node.Elements("name").FirstOrDefault()?.Value?.Trim();
        return string.IsNullOrEmpty(name) ? $"#{(string?)node.Attribute("id")}" : name;
    }

    // <nameEx> dítě = disambiguovaný dlouhý název (legacy ho ukazuje na ploše). "" když chybí
    // nebo se neliší od <name> - takový nenese informaci navíc. Pomlčky sjednoceny na "-".
    private static string NodeNameEx(XElement node, string name)
    {
        var nameEx = node.Elements("nameEx").FirstOrDefault()?.Value?.Trim().Replace('\u2013', '-').Replace('\u2014', '-');
        return string.IsNullOrEmpty(nameEx) || nameEx == name ? "" : nameEx;
    }

    // ObjectScript string literal - escapuje " → "".
    private static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

    // Port legacy EDConversion.Form.GetTypeString(id) - id → FQN třídy formy.
    // Závisí jen na jménech členů enumů (formName, packageName), které legacy zřetězuje
    // dle konvence package. Vrací "" pro neznámé id / package (neportovaná agenda).
    internal string GetTypeString(long id)
    {
        if (!_formNames.TryGetValue(id, out var form)) return "";
        long pkgVal = (id / 1000) * 1000;            // VB: (form \ 1000) * 1000 (trunc k nule)
        if (!_packageNames.TryGetValue(pkgVal, out var pkg)) return CaseElse(form);

        // Konvenční packages → "ALVA.<package>.<form>".
        if (ConventionPackages.Contains(pkg)) return $"ALVA.{pkg}.{form}";

        // Speciální prefixy (viz Select Case v clsConversion.vb).
        if (pkg == "EDFORMS") return $"ALVA.EDFORMS.FORMS.{form}";
        if (IsyPackages.Contains(pkg)) return $"ALVA.ISY.{pkg}.{form}";
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

    // Case Else z clsConversion.vb - formy, jejichž třída neodpovídá konvenci package.
    private static string CaseElse(string form) => form switch
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
    private string ResolveExe(string fqn)
    {
        if (string.IsNullOrEmpty(fqn)) return "";
        var seg = fqn.Split('.');
        if (seg.Length < 3) return "";

        var candidates = new List<string> { seg[1] + seg[2], seg[2], seg[1] };
        foreach (var c in candidates)
            foreach (var ext in new[] { ".exe", ".dll" })
                if (_deployFileExists(c + ext))
                    return SideFormsAssembly(seg[1], c + ext, fqn);
        return "";
    }

    // Legacy clsForm.LoadAssembly hledá typ, který v nalezeném sestavení není, ještě v {oblast}Forms
    // (formuláře EDISONAI leží v EDISONAIForms kvůli kruhu referencí). Host EDISONMAIN2API načte jen
    // zadané sestavení, proto strom nese rovnou to vedlejší. Nejde-li typ ověřit, zůstává nalezené.
    private string SideFormsAssembly(string area, string found, string fqn)
    {
        if (_deployTypeExists(found, fqn) != false) return found;
        foreach (var ext in new[] { ".dll", ".exe" })
            if (_deployFileExists(area + "Forms" + ext) && _deployTypeExists(area + "Forms" + ext, fqn) == true)
                return area + "Forms" + ext;
        return found;
    }

    // Vytáhne z Tree.cls override mapu: elementId -> (exe, fqn, input) jen pro neprázdné exe.
    private static Dictionary<string, (string exe, string fqn, string input)> ExtractOverrides(string cls)
    {
        var map = new Dictionary<string, (string, string, string)>();
        // ..AddExe("name", "exe", "fqn", "id", licensed, "input", ...) - 6. arg (input) volitelný.
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
    private static (Dictionary<long, string> forms, Dictionary<long, string> packages) ParseEnums(string vb)
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
    private static string EnumBlock(string vb, string name)
    {
        var m = Regex.Match(vb, $@"\bEnum\s+{Regex.Escape(name)}\b(.*?)\bEnd\s+Enum\b",
            RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value : "";
    }

    // Vrací (memberName, valueExpr) pro každý řádek enumu (přeskakuje komentáře / atributy bez "=").
    private static IEnumerable<(string name, string expr)> EnumMembers(string block)
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

    private static bool TryEvalLiteral(string expr, out long value) =>
        long.TryParse(expr.Trim(), out value);

    // Vyhodnotí "EDPackageEnum.PKG ± N", "EDPackageEnum.PKG", "OTHERNAME ± N" nebo literál.
    private static bool TryEvalExpr(string expr, Dictionary<string, long> packages, out long value)
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
}
