using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Driftová pojistka duplikace schémat: Caché <c>EDInputForm/*Edit.cls</c> třídy jsou
/// manuální mirror C# properties (field key = property path, čte/zapisuje ElementFormBinder
/// reflexí). Přejmenování property v C# bez úpravy .cls = runtime chyba u zákazníka.
///
/// Test parsuje .cls soubory přímo z repa (offline, bez Caché serveru), vytáhne field keys
/// a reflexí ověří, že každá property path na cílovém C# typu existuje. Bloky
/// <c>StyleFields.Add(form, "Prefix", …)</c> expanduje podle suffixů vyčtených ze
/// <c>StyleFields.cls</c> - při přidání pole stylu se test aktualizuje sám.
/// </summary>
public class EDInputFormSchemaDriftTests
{
    /// <summary>Mapa .cls souboru na cílový C# typ - mirror `EDInputFormSchema` switche v MainWindow
    /// + dialogy StopList/DepartureGrid (tab Obecné) + Section/Page dialogy.</summary>
    public static TheoryData<string, Type> SchemaMap => new()
    {
        { "TextElementEdit.cls",          typeof(TextElement) },
        { "LineElementEdit.cls",          typeof(LineElement) },
        { "RectangleElementEdit.cls",     typeof(RectangleElement) },
        { "BarcodeElementEdit.cls",       typeof(BarcodeElement) },
        { "ImageElementEdit.cls",         typeof(ImageElement) },
        { "NotesLegendElementEdit.cls",   typeof(NotesLegendElement) },
        { "StopListElementEdit.cls",      typeof(StopListElement) },
        { "DepartureGridElementEdit.cls", typeof(DepartureGridElement) },
        { "DynamicTableElementEdit.cls",  typeof(DynamicTableElement) },
        { "TableElementEdit.cls",         typeof(TableElement) },
        { "LineTableElementEdit.cls",     typeof(LineTableElement) },
        { "TableRowEdit.cls",             typeof(TableRow) },
        { "TableInsertEdit.cls",          typeof(TableGridSize) },
        { "SectionEdit.cls",              typeof(Section) },
        { "PageEdit.cls",                 typeof(Template) },
    };

    /// <summary>Virtuální klíče, které záměrně nejsou property path (binder je řeší speciálně).</summary>
    private static readonly Dictionary<string, string[]> VirtualKeys = new()
    {
        // Format/Orientation jsou odvozené volby (A4/landscape…) přeložené na PageSize.Width/Height
        ["PageEdit.cls"] = ["Format", "Orientation"],
    };

    // Field key = 1. string argument Add* metod (AddGroup má jako 1. arg group id - vynechat)
    private static readonly Regex DirectKeyRx = new(
        @"form\.Add(?:Text|Bool|Number|Decimal|FieldToLastGroup)\(\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex StyleFieldsCallRx = new(
        @"StyleFields\)\.Add\(form,\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex StyleFieldsSuffixRx = new(
        @"prefix_""\.(\w+)""",
        RegexOptions.Compiled);

    // BaseElementFields = sdílený balíček base polí (rámeček/rohy, pozice, podmínky). Volání
    // v elementu se rozbalí na klíče té konkrétní metody, ať base pole zůstanou drift-testovaná
    // i po přesunu do balíčku.
    private static readonly Regex BaseFieldsCallRx = new(
        @"BaseElementFields\)\.(Add\w+)\(form",
        RegexOptions.Compiled);

    private static readonly Regex BaseFieldsMethodRx = new(
        @"ClassMethod (Add\w*)\([^)]*\)\s*\{(.*?)\n\}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private const string BaseFieldsFile = "BaseElementFields.cls";

    [Theory]
    [MemberData(nameof(SchemaMap))]
    public void VsechnyFieldKeys_ExistujiJakoPropertyNaCilovemTypu(string clsFile, Type targetType)
    {
        var keys = ExtractKeys(clsFile);
        Assert.NotEmpty(keys);   // prázdný výsledek = regex zrezivěl, ne čistý soubor

        var skip = VirtualKeys.GetValueOrDefault(clsFile, []);
        var missing = keys
            .Where(k => !k.StartsWith("__"))          // defenzivně: __KEY = ne-property field
            .Where(k => !skip.Contains(k))
            .Where(k => !PropertyPathExists(targetType, k))
            .ToList();

        Assert.True(missing.Count == 0,
            $"{clsFile}: field keys bez odpovídající C# property na {targetType.Name}: " +
            $"{string.Join(", ", missing)}. Buď přejmenuj zpět, uprav .cls, nebo doplň VirtualKeys.");
    }

    // ── Výchozí hodnoty: formulář nesmí nabízet jinou než skutečnou ─────────
    // Do 5. 8. hlídal test jen EXISTENCI property, ne hodnotu. Formulář přitom výchozí hodnotu
    // ze schématu ukáže vždy, když property nemá hodnotu (`ElementFormBinder.Prefill`: prázdná
    // reflexe → `field.Value` ze schématu) - u nullable („dědí se") property to znamená, že UI
    // tvrdí jednu hodnotu a tiskne se druhá. To je přesně ta třída chyb, na kterou má projekt
    // diagnostiku v CLAUDE.md. U ne-nullable property se default neukáže nikdy, ale zůstává jako
    // past: stačí property zesplatnit na nullable a lež se aktivuje.

    /// <summary>Velikost, se kterou prvek VZNIKÁ - drží ji C# factory, ne výchozí hodnota property
    /// (holá instance má nulu). Formulář ji tedy legitimně nabízí jinou a porovnávat ji tady nelze.</summary>
    private static readonly HashSet<string> CreationSizeKeys = new(StringComparer.Ordinal) { "Width", "Height" };

    /// <summary>`form.AddText|Bool|Number|Decimal("key", "label", <default>` - 3. argument je hodnota.</summary>
    private static readonly Regex DirectDefaultRx = new(
        @"form\.Add(?:Text|Bool|Number|Decimal)\(\s*""([^""]+)"",\s*""[^""]*"",\s*([^,)]*)",
        RegexOptions.Compiled);

    /// <summary>`AddFieldToLastGroup("key", …)` - hodnota se dává až přiřazením `s f.value = …`.</summary>
    private static readonly Regex FieldToLastGroupRx = new(
        @"AddFieldToLastGroup\(\s*""([^""]+)""",
        RegexOptions.Compiled);

    /// <summary>`s f.value = …`, včetně podmíněné varianty ze stylového balíčku
    /// (`s:defTextColor'="" f.value = defTextColor`) - po dosazení argumentu se podmínka
    /// vyhodnotí tím, že dosazená hodnota není prázdná.</summary>
    private static readonly Regex ValueAssignRx = new(
        @"^\s*s(?:et)?(?::[^\s]+)?\s+f\.value\s*=\s*(.+?)\s*$",
        RegexOptions.Compiled);

    [Theory]
    [MemberData(nameof(SchemaMap))]
    public void VychoziHodnotyVeFormulari_OdpovidajiCSharpu(string clsFile, Type targetType)
    {
        // Dno kaskády přebírá výchozí styl otevřené šablony - test musí měřit zapečený stav,
        // ne to, co po sobě nechal jiný test.
        DocumentDefaults.Style = null;

        var declared = ExtractDeclaredDefaults(clsFile);
        var target = Activator.CreateInstance(targetType)!;
        var skip = VirtualKeys.GetValueOrDefault(clsFile, []);

        var problems = new List<string>();
        foreach (var (key, formDefault) in declared)
        {
            if (key.StartsWith("__") || skip.Contains(key)) continue;
            if (CreationSizeKeys.Contains(key)) continue;
            if (!ElementPropertyAccessor.Exists(target, key)) continue;   // řeší jiný test

            string csharpDefault = ElementPropertyAccessor.Get(target, key);

            // Nenastavená („dědí se") property: formulář tam VĚDOMĚ ukazuje hodnotu, na kterou
            // dopadne kaskáda (rozhodnutí 4. 8. u pozadí záhlaví poznámek), takže se surovou
            // property porovnávat nelze - musí se zeptat kaskády.
            if (csharpDefault.Length == 0 && ElementPropertyAccessor.AcceptsNull(target, key))
            {
                var (cascaded, problem) = CascadedDefault(target, key, formDefault);
                if (problem is not null) { problems.Add(problem); continue; }
                if (!SameValue(formDefault, cascaded!))
                    problems.Add($"\"{key}\": formulář nabízí \"{formDefault}\", ale z kaskády "
                                 + $"dopadne \"{cascaded}\" (property se dědí, takže se vykreslí ta z kaskády)");
                continue;
            }

            if (!SameValue(formDefault, csharpDefault))
                problems.Add($"\"{key}\": formulář nabízí \"{formDefault}\", {targetType.Name} má \"{csharpDefault}\"");
        }

        Assert.True(problems.Count == 0,
            $"{clsFile}: výchozí hodnoty se rozešly s C# (formulář by uživateli ukázal jinou hodnotu, "
            + $"než se vykreslí):\n{string.Join("\n", problems)}");
    }

    /// <summary>
    /// Pojistka, že větev „porovnej s kaskádou" nezůstane mrtvá. Kdyby ta pole z formulářů zmizela
    /// (nebo se přestala poznávat), test výše by dál svítil zeleně a nikdo by nevěděl, že hlídá
    /// o třídu chyb méně - a je to ta třída, kterou uživatel reálně uvidí (formulář ukáže barvu,
    /// tisk vezme jinou).
    /// </summary>
    [Fact]
    public void PorovnaniSKaskadou_NezustaloBezPokryti()
    {
        DocumentDefaults.Style = null;

        var covered = new List<string>();
        foreach (var row in SchemaMap)
        {
            var clsFile = (string)row[0]!;
            var type = (Type)row[1]!;
            var target = Activator.CreateInstance(type)!;
            var skip = VirtualKeys.GetValueOrDefault(clsFile, []);

            foreach (var (key, _) in ExtractDeclaredDefaults(clsFile))
            {
                if (key.StartsWith("__") || skip.Contains(key) || CreationSizeKeys.Contains(key)) continue;
                if (!ElementPropertyAccessor.Exists(target, key)) continue;
                if (ElementPropertyAccessor.Get(target, key).Length != 0) continue;
                if (!ElementPropertyAccessor.AcceptsNull(target, key)) continue;

                covered.Add($"{type.Name}.{key}");
            }
        }

        Assert.True(covered.Count >= 8,
            "Proti kaskádě se porovnává jen " + covered.Count + " polí (čekáno aspoň 8: barva textu "
            + "a pozadí × záhlaví a buňky × legenda poznámek a linková tabulka). Nalezeno: "
            + string.Join(", ", covered));
    }

    /// <summary>
    /// Hodnota, kterou pro nenastavenou („dědí se") property vydá kaskáda. Zóna se odvodí z prefixu
    /// klíče (<c>HeaderStyle.TextColor</c> → <c>ResolveElementHeaderParent()</c>) - tenhle přístupový
    /// bod má na dnešek každá rodina tabulkových prvků (JŘ základ, dynamická i pevná tabulka).
    /// Sdílet ho přes interface se vědomě NEmá (`mvvm-patterns.md` §Pravidla pro inheritance),
    /// proto se hledá konvencí názvu - a když chybí, test to **nahlásí**, ne přeskočí.
    /// Vrací (hodnota, null) nebo (null, důvod selhání).
    /// </summary>
    private static (string? Value, string? Problem) CascadedDefault(object target, string key, string formDefault)
    {
        int dot = key.LastIndexOf('.');
        if (dot <= 0)
            return (null, $"\"{key}\": property se dědí a formulář u ní nabízí \"{formDefault}\", "
                          + "ale klíč není stylová zóna - nejde zjistit, co dopadne z kaskády");

        string prefix = key[..dot], propertyName = key[(dot + 1)..];
        if (!prefix.EndsWith("Style", StringComparison.Ordinal))
            return (null, $"\"{key}\": property se dědí, ale prefix \"{prefix}\" není stylová zóna "
                          + "- doplň způsob, jak se u ní zjistí hodnota z kaskády");

        string zone = prefix[..^"Style".Length];
        var resolver = target.GetType().GetMethod($"ResolveElement{zone}Parent",
            BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        if (resolver is null)
            return (null, $"\"{key}\": formulář nabízí výchozí hodnotu pro děděnou property, ale "
                          + $"{target.GetType().Name} nemá ResolveElement{zone}Parent() - bez něj nejde "
                          + "ověřit, že formulář neukazuje jinou barvu, než se vykreslí");

        var resolved = resolver.Invoke(target, null);
        var property = resolved?.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        if (property is null)
            return (null, $"\"{key}\": vyřešený styl nemá property \"{propertyName}\"");

        return (FormValue(property.GetValue(resolved)), null);
    }

    /// <summary>Hodnota z modelu ve tvaru, v jakém ji nese formulář (bool jako 1/0, jinak text).
    /// Zrcadlí konverzi v <c>ElementPropertyAccessor</c> - ta je private.</summary>
    private static string FormValue(object? value) => value switch
    {
        null      => "",
        bool b    => b ? "1" : "0",
        float f   => f.ToString(CultureInfo.InvariantCulture),
        double d  => d.ToString(CultureInfo.InvariantCulture),
        Enum e    => e.ToString(),
        _         => value.ToString() ?? "",
    };

    /// <summary>Deklarované výchozí hodnoty z .cls. Nezjistitelné (proměnná místo literálu,
    /// prázdná hodnota = „nech na C#") se vynechají - porovnávat se dá jen literál.</summary>
    private static Dictionary<string, string> ExtractDeclaredDefaults(string clsFile)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        string? lastKey = null;

        foreach (var line in ClsLinesWithBaseFields(clsFile))
        {
            if (DirectDefaultRx.Match(line) is { Success: true } direct)
            {
                lastKey = direct.Groups[1].Value;
                if (Literal(direct.Groups[2].Value) is { } v) result[lastKey] = v;
                continue;
            }
            if (FieldToLastGroupRx.Match(line) is { Success: true } f2l)
            {
                lastKey = f2l.Groups[1].Value;
                continue;
            }
            if (lastKey is not null && ValueAssignRx.Match(line) is { Success: true } assign
                && Literal(assign.Groups[1].Value) is { } val)
                result[lastKey] = val;
        }
        return result;
    }

    /// <summary>Řádky formuláře včetně rozbalených volání sdíleného balíčku base polí - jinak by
    /// base pole (mřížka, rámeček, pozice) zůstala bez kontroly výchozích hodnot. Rozbalení
    /// respektuje přepínače balíčku - `AddGrid(form, 0)` u legendy poznámek mřížkové přepínače
    /// nepřidává, takže se u ní nesmí kontrolovat.
    ///
    /// Výchozí šířka/výška se předává jako argument (`AddPosition(form, 30, 30, 1)`) a **záměrně
    /// se nekontroluje**: je to velikost, se kterou prvek vzniká (drží ji C# factory), ne výchozí
    /// hodnota property - holá instance má `Width = 0`. Porovnávat se dá jen literál v těle
    /// balíčku, proměnná se přeskočí.</summary>
    private static IEnumerable<string> ClsLinesWithBaseFields(string clsFile)
    {
        var methods = BaseFieldsMethods();
        foreach (var line in File.ReadAllLines(Path.Combine(EDInputFormDir(), clsFile)))
        {
            if (BaseFieldsCallWithArgsRx.Match(line) is { Success: true } call
                && methods.TryGetValue(call.Groups[1].Value, out var method))
            {
                var args = BindArguments(method.Parameters, call.Groups[2].Value);
                foreach (var inner in ExpandBody(method.Body, args)) yield return inner;
                continue;
            }
            yield return line;
        }
    }

    private static readonly Regex BaseFieldsCallWithArgsRx = new(
        @"(?:BaseElementFields|StyleFields)\)\.(Add\w*)\(form\s*(?:,\s*(.*?))?\s*\)\s*$",
        RegexOptions.Compiled);

    /// <summary>`ClassMethod AddGrid(form As …, withLineToggles As %Boolean = 1)` → parametry
    /// za `form` s jejich výchozími hodnotami, v deklarovaném pořadí.</summary>
    private static readonly Regex BaseFieldsSignatureRx = new(
        @"ClassMethod (Add\w*)\(([^)]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex ParamRx = new(
        @"(\w+)\s+As\s+[\w%.]+(?:\s*=\s*(""[^""]*""|[^,]+))?",
        RegexOptions.Compiled);

    /// <summary>`i &lt;param&gt; {` - jediná forma podmínky, kterou umíme vyhodnotit. Cokoli
    /// složitějšího se rozbalí celé (konzervativně), takže test nemůže tichým vyhodnocením
    /// pole ztratit.</summary>
    private static readonly Regex SimpleGuardRx = new(
        @"^\s*i(?:f)?\s+(\w+)\s*\{\s*$",
        RegexOptions.Compiled);

    private sealed record BaseFieldsMethod(List<(string Name, string? Default)> Parameters, string[] Body);

    private static Dictionary<string, BaseFieldsMethod>? _baseFieldsMethods;

    private static Dictionary<string, BaseFieldsMethod> BaseFieldsMethods()
    {
        if (_baseFieldsMethods is not null) return _baseFieldsMethods;

        // Oba sdílené balíčky: base pole (`AddBorder`/`AddGrid`/…) i stylové zóny (`Add`).
        // Bez stylového balíčku by z kontroly vypadly výchozí barvy a řezy záhlaví a buněk,
        // které si prvky předávají jako argumenty - a právě tam byla nalezena lež o tučnosti.
        var result = new Dictionary<string, BaseFieldsMethod>(StringComparer.Ordinal);
        foreach (var file in new[] { BaseFieldsFile, StyleFieldsFile })
        {
            string text = File.ReadAllText(Path.Combine(EDInputFormDir(), file));
            var bodies = BaseFieldsMethodRx.Matches(text)
                .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Split('\n'));

            foreach (Match sig in BaseFieldsSignatureRx.Matches(text))
            {
                if (!bodies.TryGetValue(sig.Groups[1].Value, out var body)) continue;
                var pars = ParamRx.Matches(sig.Groups[2].Value)
                    .Select(m => (Name: m.Groups[1].Value,
                                  Default: m.Groups[2].Success ? m.Groups[2].Value : null))
                    .Where(p => p.Name != "form")
                    .ToList();
                result[sig.Groups[1].Value] = new BaseFieldsMethod(pars, body);
            }
        }
        return _baseFieldsMethods = result;
    }

    private const string StyleFieldsFile = "StyleFields.cls";

    /// <summary>Pozičně napáruje argumenty volání na parametry; nedodané zůstanou na deklarované
    /// výchozí hodnotě.</summary>
    private static Dictionary<string, string> BindArguments(
        List<(string Name, string? Default)> parameters, string rawArgs)
    {
        var given = SplitArguments(rawArgs);
        var bound = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < parameters.Count; i++)
        {
            var value = i < given.Count ? given[i] : parameters[i].Default;
            if (value is not null) bound[parameters[i].Name] = value.Trim();
        }
        return bound;
    }

    /// <summary>Dělení na argumenty po vrchní úrovni - uvnitř uvozovek se čárka nepočítá.</summary>
    private static List<string> SplitArguments(string raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        bool inString = false;
        int start = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '"') inString = !inString;
            else if (raw[i] == ',' && !inString) { result.Add(raw[start..i]); start = i + 1; }
        }
        result.Add(raw[start..]);
        return result;
    }

    private static IEnumerable<string> ExpandBody(string[] body, Dictionary<string, string> args)
    {
        int skipUntilDepth = -1;
        int depth = 0;

        foreach (var raw in body)
        {
            if (skipUntilDepth < 0 && SimpleGuardRx.Match(raw) is { Success: true } guard
                && args.TryGetValue(guard.Groups[1].Value, out var flag)
                && (flag is "0" or "\"\"" || flag.Length == 0))
            {
                skipUntilDepth = depth;
                depth++;
                continue;
            }

            depth += raw.Count(c => c == '{') - raw.Count(c => c == '}');

            if (skipUntilDepth >= 0)
            {
                if (depth <= skipUntilDepth) skipUntilDepth = -1;
                continue;
            }

            yield return SubstituteArguments(raw, args);
        }
    }

    /// <summary>Dosadí hodnoty argumentů za názvy parametrů - jinak by výchozí hodnota, kterou si
    /// prvek předává do sdíleného balíčku, z kontroly vypadla jako „neliterál". Obsah uvozovek
    /// se nepřepisuje (popisky).</summary>
    private static string SubstituteArguments(string line, Dictionary<string, string> args)
    {
        // Hranice názvu jsou jen písmena a číslice - `_` je v ObjectScriptu operátor spojení,
        // ne součást názvu (proto ho konvence v proměnných zakazuje). Kdyby se bral jako součást
        // názvu, `prefix_".TextColor"` by se nedosadil a klíč zóny by se nepoznal.
        foreach (var (name, value) in args)
            line = Regex.Replace(line, $@"(?<![""A-Za-z0-9.]){Regex.Escape(name)}(?![A-Za-z0-9])", value);

        // Stylový balíček skládá klíč spojením (`prefix_".TextColor"`); po dosazení je z toho
        // `"HeaderStyle"_".TextColor"` a musí se to slít na jeden literál, jinak se klíč nepozná.
        string before;
        do
        {
            before = line;
            line = ConcatRx.Replace(line, "\"$1$2\"");
        } while (line != before);

        return line;
    }

    private static readonly Regex ConcatRx = new(@"""([^""]*)""\s*_\s*""([^""]*)""", RegexOptions.Compiled);

    /// <summary>Literál z argumentu .cls, nebo null když se staticky přečíst nedá (proměnná,
    /// výraz, makro) nebo je prázdný (= „výchozí hodnotu drží C#").</summary>
    private static string? Literal(string raw)
    {
        raw = raw.Trim();
        if (raw.Length == 0 || raw is "\"\"") return null;
        if (raw.StartsWith('"') && raw.EndsWith('"') && raw.Length >= 2)
            return raw[1..^1] is { Length: > 0 } inner ? inner : null;
        // Číslo bez uvozovek (0, 1, 10, 0.5); cokoli jiného je proměnná nebo výraz
        return double.TryParse(raw, CultureInfo.InvariantCulture, out _) ? raw : null;
    }

    /// <summary>Formulář a C# zapisují tutéž hodnotu různě - bool jako 1/0, čísla jako 0 vs 0.5.</summary>
    private static bool SameValue(string formValue, string csharpValue)
    {
        if (string.Equals(formValue, csharpValue, StringComparison.Ordinal)) return true;
        if (double.TryParse(formValue, CultureInfo.InvariantCulture, out var a)
            && double.TryParse(csharpValue, CultureInfo.InvariantCulture, out var b))
            return Math.Abs(a - b) < 1e-6;
        return false;
    }

    // ── Co jde nastavit v panelu, musí jít nastavit i ve formuláři ───────────
    // Hlavní editační cestou je Caché formulář; panelové deskriptory zůstaly pro
    // formátovací lištu, úpravu více prvků a fallback. Když ale formulář nějakou vlastnost nemá,
    // je u jednoho prvku hlavní cestou nedosažitelná - přesně tak zmizely 3 vlastnosti mřížky
    // odjezdů (zarovnání záhlaví, dvojice barev negativní značky), nalezené 4. 8. ručně.

    /// <summary>Vlastnosti, které panel VĚDOMĚ nabízí sám. Klíč = "Typ.PropertyPath".</summary>
    private static readonly Dictionary<string, string> PanelOnlyProperties = new(StringComparer.Ordinal)
    {
        // Dopočítané náhledy skutečné čárky/mezery - nejsou to nastavitelné hodnoty
        ["LineElement.EffectiveDashMm"] = "dopočítaná hodnota, ne nastavení",
        ["LineElement.EffectiveGapMm"]  = "dopočítaná hodnota, ne nastavení",
        // Šířka sloupce hodin se zadává v tabu Sloupce (řádek Hodiny) - formulář ji záměrně
        // nemá, aby existoval jediný zdroj (viz komentář v DepartureGridElementEdit.cls)
        ["DepartureGridElement.HourColumnWidth"] = "jediný zdroj je tab Sloupce",
        // Pevná tabulka řádky z dat negeneruje (na to je DynamicTableElement). Pole zůstává jen
        // v panelu kvůli starým šablonám, které tenhle prvek mají v režimu „z dat".
        ["TableElement.DataSource"] = "jen pro staré šablony v režimu z dat; nový prvek ho nemá",
    };

    private static readonly Regex DescriptorGetterRx = new(
        @"Get\s*=\s*\(\)\s*=>\s*([A-Za-z_][A-Za-z0-9_.]*)",
        RegexOptions.Compiled);

    [Theory]
    [MemberData(nameof(SchemaMap))]
    public void CoJdeNastavitVPanelu_JdeNastavitIVeFormulari(string clsFile, Type targetType)
    {
        if (!typeof(TemplateElement).IsAssignableFrom(targetType)) return;   // sekce/stránka panel nemá

        var panelProperties = PanelEditedProperties(targetType);
        Assert.NotEmpty(panelProperties);   // prázdno = regex zrezivěl, ne že prvek nic needituje

        var formKeys = ExtractKeys(clsFile).ToHashSet(StringComparer.Ordinal);
        var stylePrefixes = FormStylePrefixes(clsFile);
        var target = Activator.CreateInstance(targetType)!;

        var missing = panelProperties
            .Where(p => !formKeys.Contains(p))
            .Where(p => !stylePrefixes.Contains(p.Split('.')[0]))
            .Where(p => !PanelOnlyProperties.ContainsKey($"{targetType.Name}.{p}"))
            .Where(p => ElementPropertyAccessor.Exists(target, p))   // dopočítané get-only vlastnosti
            .ToList();

        Assert.True(missing.Count == 0,
            $"{targetType.Name}: panel umí nastavit vlastnosti, které ve formuláři ({clsFile}) nejsou, "
            + $"takže hlavní editační cestou nejdou změnit: {string.Join(", ", missing)}. "
            + "Doplň je do .cls, nebo je zapiš do PanelOnlyProperties s důvodem.");
    }

    /// <summary>Property paths z `GetPropertyDescriptors()` v .cs prvku. Base sada
    /// (`GetPositionDescriptors` a spol.) sem nespadá - žije ve své třídě a hlídají ji jiné testy.</summary>
    private static List<string> PanelEditedProperties(Type elementType)
    {
        string source = File.ReadAllText(ElementSourceFile(elementType));
        int start = source.IndexOf("GetPropertyDescriptors", StringComparison.Ordinal);
        if (start < 0) return [];

        // Konec kolekce deskriptorů: první "];" na úrovni odsazení metody
        int end = source.IndexOf("\n    ];", start, StringComparison.Ordinal);
        string block = end > start ? source[start..end] : source[start..];

        return DescriptorGetterRx.Matches(block)
            .Select(m => m.Groups[1].Value.TrimEnd('.'))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string ElementSourceFile(Type elementType)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;

        string path = Path.Combine(dir!.FullName, "TemplateDesigner", "dotnet", "Models", "Elements",
            elementType.Name + ".cs");
        Assert.True(File.Exists(path), $"Nenalezen zdroják prvku: {path}");
        return path;
    }

    /// <summary>Prefixy stylových zón ve formuláři (balíček `StyleFields`). Vlastnost pod takovým
    /// prefixem je ve formuláři dosažitelná.</summary>
    private static HashSet<string> FormStylePrefixes(string clsFile)
    {
        string text = File.ReadAllText(Path.Combine(EDInputFormDir(), clsFile));
        return StyleFieldsCallRx.Matches(text).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Sada Text (`bunka = 0`) je jen písmo a barva textu. Minuta, značky u odjezdů a 2. řádek
    /// záhlaví jsou text uvnitř buňky - pozadí, zarovnání ani okraje jim prvek nekreslí, takže je
    /// formulář nesmí nabízet (`B194` E3).</summary>
    [Fact]
    public void TextUvnitrBunky_MaJenSaduText()
    {
        var add = BaseFieldsMethods()["Add"];
        var sadaText = ExpandBody(add.Body, new Dictionary<string, string> { ["bunka"] = "0" })
            .SelectMany(l => StyleFieldsSuffixRx.Matches(l).Select(m => m.Groups[1].Value))
            .ToHashSet();
        Assert.Equal(new HashSet<string> { "FontFamily", "FontWeight", "FontSize", "Bold", "Italic",
            "Underline", "Strikethrough", "TextColor" }, sadaText);

        var bunkaPodleZony = File.ReadAllLines(Path.Combine(EDInputFormDir(), "DepartureGridElementEdit.cls"))
            .Select(l => BaseFieldsCallWithArgsRx.Match(l))
            .Where(m => m.Success && m.Groups[1].Value == "Add")
            .Select(m => BindArguments(add.Parameters, m.Groups[2].Value))
            .ToDictionary(a => a["prefix"].Trim('"'), a => a["bunka"]);
        foreach (var zona in new[] { "MinuteStyle", "AnotaceSymbolStyle", "AnotaceTextStyle", "HeaderLine2Style" })
            Assert.Equal("0", bunkaPodleZony[zona]);
        Assert.Equal("1", bunkaPodleZony["DataStyle"]);
        Assert.Equal("1", bunkaPodleZony["HeaderStyle"]);
    }

    /// <summary>Styl je ve formulářích všude rozepsaný, žádný prvek ho neschovává za tlačítko
    /// „Upravit styl…" (rozhodnutí Jan 28. 10., `B194` E3). Okno stylu, které tlačítko otevíralo,
    /// je pryč - nové tlačítko s klíčem <c>style:</c> by nic neudělalo.</summary>
    [Fact]
    public void Formulare_NemajiTlacitkoStylu()
    {
        var sTlacitkem = AllEditClsFiles()
            .Where(f => File.ReadAllText(Path.Combine(EDInputFormDir(), f)).Contains("\"style:"))
            .ToList();

        Assert.True(sTlacitkem.Count == 0, "Formulář s tlačítkem stylu: " + string.Join(", ", sTlacitkem));
    }

    [Fact]
    public void StyleFieldsSuffixy_SeNaparsovaly()
    {
        // Pojistka regexu - StyleFields.cls definuje balíček polí stylu (FontFamily, FontSize, …)
        var suffixes = StyleFieldsSuffixes();

        Assert.True(suffixes.Count >= 10, $"Načteno jen {suffixes.Count} suffixů - regex nesedí na StyleFields.cls?");
        Assert.Contains("FontSize", suffixes);
        Assert.Contains("TextColor", suffixes);
    }

    /// <summary>Pojistka pokrytí: každý EDInputForm `*Edit.cls` musí být buď v SchemaMap
    /// (drift-testovaný), nebo ve výjimkách (ne-elementové formy). Nový element se svým
    /// `*Edit.cls` se tak nemůže proklouznout bez drift testu.</summary>
    [Fact]
    public void VsechnyEditClsFormy_JsouPokryteNeboVyjimka()
    {
        // Ne-elementové formy - needitují property elementu/sekce/šablony:
        //   TableCellEdit = formulář vybrané buňky pevné tabulky. Do kontroly výchozích hodnot
        //     nepatří, protože se **nikdy nezobrazí**: klient je před vyplněním přepíše hodnotami
        //     z kaskády (buňka je překryv nad nastavením celé tabulky). Vazbu na C# i pokrytí
        //     kaskádou hlídá `FixedTableCellFormattingTests` - přísněji, než by uměl tenhle test.
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "TableCellEdit.cls" };

        var covered = SchemaMap.Select(row => (string)row[0]!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uncovered = Directory.GetFiles(EDInputFormDir(), "*Edit.cls")
            .Select(Path.GetFileName)
            .Cast<string>()
            .Where(f => !covered.Contains(f) && !excluded.Contains(f))
            .ToList();

        Assert.True(uncovered.Count == 0,
            $"EDInputForm `*Edit.cls` bez drift testu: {string.Join(", ", uncovered)}. " +
            "Přidej do SchemaMap (element ↔ C# typ), nebo do `excluded` (ne-elementová forma).");
    }

    // ── Base pole: jedno místo na každé straně ───────────────────────────────
    // Base sada (rámeček/rohy · pozice a velikost · podmínky) byla do 29. 7. opsaná ručně v každém
    // z deseti formulářů → rozešly se rozsahy (tloušťka rámečku 0-10 × 0-5 mm), dynamické tabulce
    // pole rámečku chybělo úplně a u tří elementů byl rámeček v cizí skupině. Tyhle dva testy drží
    // stav „jedna definice na straně Caché + jedna v C#, a obě říkají totéž".

    [Fact]
    public void ZadnyElementovyFormular_NeopisujeBasePoleRucne()
    {
        var baseKeys = BaseFieldsKeysPerMethod().Values.SelectMany(k => k).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(baseKeys);   // prázdno = regex zrezivěl, ne že je vše v pořádku

        var hits = new List<string>();
        foreach (var row in SchemaMap)
        {
            var file = (string)row[0]!;
            if (!typeof(TemplateElement).IsAssignableFrom((Type)row[1]!)) continue;

            string text = File.ReadAllText(Path.Combine(EDInputFormDir(), file));
            foreach (var key in DirectKeyRx.Matches(text).Select(m => m.Groups[1].Value).Distinct())
                if (baseKeys.Contains(key)) hits.Add($"{file}: \"{key}\"");
        }

        Assert.True(hits.Count == 0,
            "Base pole se v elementovém formuláři definují ručně - musí jít přes sdílený "
            + $"{BaseFieldsFile} (jinak se rozsahy a skupiny znovu rozejdou):\n{string.Join("\n", hits)}");
    }

    [Fact]
    public void BasePole_MajiVCsStejnyPopisekISkupinu()
    {
        var expected = BaseFieldsLabelToGroup();
        Assert.NotEmpty(expected);

        // Obdélník = element s rohy i pevnou výškou → pokrývá celou base sadu.
        var descriptors = new RectangleElement().GetPropertyDescriptors()
            .Where(d => !string.IsNullOrEmpty(d.Label))
            .ToDictionary(d => d.Label!, d => d.Group ?? "", StringComparer.Ordinal);

        var problems = expected
            .Where(kv => !descriptors.TryGetValue(kv.Key, out var group) || group != kv.Value)
            .Select(kv => descriptors.TryGetValue(kv.Key, out var g)
                ? $"\"{kv.Key}\": Caché skupina \"{kv.Value}\" × C# \"{g}\""
                : $"\"{kv.Key}\": v C# deskriptorech chybí (Caché skupina \"{kv.Value}\")")
            .ToList();

        Assert.True(problems.Count == 0,
            $"Base pole se rozešla mezi {BaseFieldsFile} a C# GetPositionDescriptors "
            + $"(dvě editační cesty téže sady):\n{string.Join("\n", problems)}");
    }

    [Fact]
    public void PoleMrizky_MajiVCsStejnyPopisekISkupinu()
    {
        // `AddGrid` je volitelný balíček (tabulkové prvky), takže se neověřuje na obdélníku jako
        // zbytek base sady, ale na prvku, který mřížku má.
        var expected = BaseFieldsLabelToGroup("AddGrid");
        Assert.NotEmpty(expected);

        var descriptors = new StopListElement().GetPropertyDescriptors()
            .Where(d => !string.IsNullOrEmpty(d.Label))
            .ToDictionary(d => d.Label!, d => d.Group ?? "", StringComparer.Ordinal);

        var problems = expected
            .Where(kv => !descriptors.TryGetValue(kv.Key, out var group) || group != kv.Value)
            .Select(kv => descriptors.TryGetValue(kv.Key, out var g)
                ? $"\"{kv.Key}\": Caché skupina \"{kv.Value}\" × C# \"{g}\""
                : $"\"{kv.Key}\": v C# deskriptorech chybí (Caché skupina \"{kv.Value}\")")
            .ToList();

        Assert.True(problems.Count == 0,
            $"Pole mřížky se rozešla mezi {BaseFieldsFile} a C# GetGridDescriptors:\n{string.Join("\n", problems)}");
    }

    // ── Názvoslovný drift (labely + volby) ───────────────────────────────────
    // Zamyká kanon z ux-conventions §8: tentýž field key musí mít všude tentýž label
    // a zakázané tvary termínů se nesmí objevit v labelech, volbách ani panelových deskriptorech.

    /// <summary>Zakázané tvary (kanon §8 + rozhodnutí Jan 2026-07-28). Exact = celý label.
    /// Sdílené s <see cref="AgendaSchemaDriftTests"/> - kanon je jeden, ať platí na formulářích
    /// i na slovníku polí agend.</summary>
    internal static readonly string[] BannedExactLabels =
    [
        "Zamčeno",          // → Uzamčeno
        "Viditelnost",      // → Podmínka viditelnosti
        "Viditelnost řádku", // → Podmínka viditelnosti řádku (je to podmínka, ne zaškrtávátko - Jan 28. 10.)
        "Rozdělit na více oddílů", // → Počet oddílů (číslo se jmenuje podle toho, co se zadává - Jan 28. 10.)
        "Střídání řádků",   // → Střídání řádků: Barva pozadí (barva se najde hledáním „barva" - Jan 28. 10.)
        "Rozestup odjezdů", // → Zarovnání minut (volby jsou o zarovnání - Jan 28. 10.)
        "Tučně",            // → Tučné
        "Podtržené",        // → Podtržení
        "Přeškrtnuté",      // → Přeškrtnutí
        "Horizontálně",     // → Zarovnání vodorovně
        "Vertikálně",       // → Zarovnání svisle
        "Vodorovně",        // → Zarovnání vodorovně (celý tvar jde hledat, rozhodnutí Jan 2026-10-26, `B194` E6)
        "Svisle",           // → Zarovnání svisle
        "Nahoru",           // → Nahoře (zarovnání je stav, ne pohyb)
        "Dolů",             // → Dole
        "Pokročilé",        // skupina → Podmínky
        "ZIndex",           // → Vrstva (rozhodnutí Jan 2026-10-26, `B194` E7)
        "Velikost značek",  // → Poměr velikosti značek („Velikost" = pevná hodnota v pt / mm, `B194` E9c)
        "Velikost minut",   // → Poměr velikosti minut
    ];

    /// <summary>Zakázané podřetězce v labelech/volbách (case-sensitive). Sdílené s
    /// <see cref="AgendaSchemaDriftTests"/>.</summary>
    internal static readonly string[] BannedLabelSubstrings =
    [
        "Šířka rámečku",    // → Tloušťka rámečku (Šířka patří elementu)
        "Zebra",            // → Střídání řádků: Barva pozadí
        "Min. ",            // zkratky ne → „Nejmenší …" (Jan 28. 10.)
        "Tl. ",             // → Tloušťka …
        "Řádek NZ",         // → Řádek negativních značek
        "Font aktuální",    // → Aktuální: Velikost (je to velikost písma, ne rodina)
        " per ",            // UI texty česky (kanon §8)
        "(vlak/bus)",       // → (vlak/autobus)
        "nástupiště",       // → označník
        "Nástupiště",       // → Označník
        "Tabulka linky",    // → Tabulka spojů (tak se jmenuje agenda, prvek by se s ní pletl)
        "Mřížka odjezdů",   // → Minuty odjezdů
        "Legenda poznámek", // → Poznámky
        // Hlavička tabulky oproti datovým buňkám = „záhlaví"; „nadpis" jen pro jinou věc
        // (text skupiny dnů v datech) - rozhodnutí Jan 2026-10-26, `B194` E4
        "Nadpisy skupin",   // → Záhlaví skupin dnů
        "Nadpis popisných", // → Text záhlaví popisných sloupců
        "řádku nadpisu",    // → 2. řádek záhlaví
        "Oddělovač skupin", // → Mezera mezi skupinami dnů (je to mezera, ne čára; `B194` E9a)
        "skupinami sloupců", // → Mezera mezi skupinami dnů (stejný pojem v Minutách i Tabulce spojů)
        "↔",                // → slovy „Mezera mezi značkou a minutou" (`B194` E9b)
        "aktuální zast.",   // → „Aktuální: Vlastnost" (zkratka; `B194` E10)
        "projetých",        // → „Projeté: Barva textu", „Projeté: Zobrazit čas"
        "element",          // → prvek (rozhodnutí Jan 2026-10-26, `B194` E7)
        "Element",
        "cílov",            // → konečná (poslední zastávka směru; Jan 2026-10-27, `B194` D3)
        "CIS číslo",        // → „Číslo … zastávky (CIS)" - CIS na konci jako u názvů (`B194` D3)
        // „stanoviště" už zakázané NENÍ (2026-08-03, #13088): označník a sloupek se ukázaly být dvě
        // různá pole a „stanoviště" je zastřešující pojem pro to z nich, které je zvolené parametrem
        // „Číslo stanoviště podle". Jako synonymum pro označník se dál nepoužívá - viz ux-conventions §8.
        "\u2014",      // em dash → běžná pomlčka "-" (rozhodnutí Jan 2026-07-28)
    ];

    private static readonly Regex LabeledFieldRx = new(
        @"form\.Add(?:Text|Bool|Number|Decimal|FieldToLastGroup)\(\s*""([^""]+)"",\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex ListItemRx = new(
        @"AddListItem\(\s*""[^""]*"",\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex GroupLabelRx = new(
        @"AddGroup\(\s*""[^""]*"",\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static IEnumerable<string> AllEditClsFiles() =>
        Directory.GetFiles(EDInputFormDir(), "*.cls").Select(Path.GetFileName).Cast<string>();

    [Fact]
    public void StejnyFieldKey_MaVsudeStejnyLabel()
    {
        // Legitimní rozdíly labelu téhož klíče mezi formuláři (kontextové upřesnění) - vědomé výjimky:
        // („Height" tu byl do 29. 7. kvůli rozdílnému popisku výšky - sdílený BaseElementFields
        //  ho sjednotil na „Výška" + nápovědu, takže výjimka padla.)
        //   NegativniZnacka*Color - v Poznámkách mají vlastní skupinu Negativní značky („Barva pozadí"), v mřížce
        //     odjezdů stojí hned vedle obecné barvy symbolu anotace, takže tam musí být v popisku
        //     rozlišené („Negativní značka: Barva pozadí"); bez toho se nedá poznat, co která barva řídí.
        //   Format - u stránky je to formát papíru („Formát"), u buňky formát vypisované hodnoty
        //     („Formát hodnoty"). Dvě různé věci, které jen shodou okolností mají stejný název
        //     vlastnosti na svém cíli.
        //   HeaderHeight - v Tabulce z dat je to nejmenší výška záhlaví (víc řádků ho zvětší), v Pevné
        //     tabulce pevná výška; stejný popisek by tvrdil, že se chovají stejně (`B194h` H6).
        var exempt = new HashSet<string>(StringComparer.Ordinal)
            { "NegativniZnackaBackColor", "NegativniZnackaForeColor", "Format", "HeaderHeight" };

        var labelsPerKey = new Dictionary<string, Dictionary<string, List<string>>>();
        foreach (var file in AllEditClsFiles())
        {
            string text = File.ReadAllText(Path.Combine(EDInputFormDir(), file));
            foreach (Match m in LabeledFieldRx.Matches(text))
            {
                var key = m.Groups[1].Value;
                if (key.StartsWith("__") || exempt.Contains(key)) continue;
                var perLabel = labelsPerKey.TryGetValue(key, out var d) ? d : labelsPerKey[key] = new();
                (perLabel.TryGetValue(m.Groups[2].Value, out var files) ? files : perLabel[m.Groups[2].Value] = new()).Add(file);
            }
        }

        var drift = labelsPerKey
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => $"{kv.Key}: " + string.Join(" × ", kv.Value.Select(l => $"\"{l.Key}\" ({string.Join(",", l.Value.Distinct())})")))
            .ToList();

        Assert.True(drift.Count == 0,
            "Tentýž field key má v různých .cls různý label - sjednoť (kanon ux-conventions §8):\n" +
            string.Join("\n", drift));
    }

    [Fact]
    public void ZadneZakazaneTerminy_VClsLabelechAVolbach()
    {
        var hits = new List<string>();
        foreach (var file in AllEditClsFiles())
        {
            string text = File.ReadAllText(Path.Combine(EDInputFormDir(), file));
            var labels = LabeledFieldRx.Matches(text).Select(m => m.Groups[2].Value)
                .Concat(ListItemRx.Matches(text).Select(m => m.Groups[1].Value))
                .Concat(GroupLabelRx.Matches(text).Select(m => m.Groups[1].Value));
            foreach (var label in labels)
            {
                if (BannedExactLabels.Contains(label)) hits.Add($"{file}: \"{label}\"");
                else foreach (var sub in BannedLabelSubstrings)
                    if (label.Contains(sub, StringComparison.Ordinal)) hits.Add($"{file}: \"{label}\" (obsahuje \"{sub}\")");
            }
        }
        Assert.True(hits.Count == 0,
            "Zakázané termíny v .cls labelech/volbách (kanon ux-conventions §8):\n" + string.Join("\n", hits.Distinct()));
    }

    [Fact]
    public void ZadneZakazaneTerminy_VPanelovychDescriptorech()
    {
        var hits = new List<string>();
        foreach (var row in SchemaMap)
        {
            var type = (Type)row[1]!;
            if (!typeof(TemplateElement).IsAssignableFrom(type)) continue;
            var element = (TemplateElement)Activator.CreateInstance(type)!;
            foreach (var d in element.GetPropertyDescriptors())
            {
                var texts = new List<string?> { d.Label, d.Group };
                if (d is EnumPropertyDescriptor e) texts.AddRange(e.Options.Select(o => o.Label));
                foreach (var t in texts.Where(t => !string.IsNullOrEmpty(t)))
                {
                    if (BannedExactLabels.Contains(t)) hits.Add($"{type.Name}: \"{t}\"");
                    else foreach (var sub in BannedLabelSubstrings)
                        if (t!.Contains(sub, StringComparison.Ordinal)) hits.Add($"{type.Name}: \"{t}\" (obsahuje \"{sub}\")");
                }
            }
        }
        Assert.True(hits.Count == 0,
            "Zakázané termíny v panelových deskriptorech (kanon ux-conventions §8):\n" + string.Join("\n", hits.Distinct()));
    }

    // ── Parsování .cls ────────────────────────────────────────────────────────

    private static List<string> ExtractKeys(string clsFile)
    {
        string text = File.ReadAllText(Path.Combine(EDInputFormDir(), clsFile));
        var keys = DirectKeyRx.Matches(text).Select(m => m.Groups[1].Value).ToList();

        // StyleFields.Add(form, "HeaderStyle", …) → HeaderStyle.FontFamily, HeaderStyle.FontSize, …
        var suffixes = StyleFieldsSuffixes();
        foreach (Match m in StyleFieldsCallRx.Matches(text))
            keys.AddRange(suffixes.Select(s => $"{m.Groups[1].Value}.{s}"));

        // BaseElementFields.AddPosition(form, …) → X, Y, Width, Height, Rotation, ZIndex, IsLocked
        var baseKeys = BaseFieldsKeysPerMethod();
        foreach (Match m in BaseFieldsCallRx.Matches(text))
            keys.AddRange(baseKeys.GetValueOrDefault(m.Groups[1].Value, []));

        return keys;
    }

    /// <summary>Klíče polí, které přidává každá metoda sdíleného balíčku (AddBorder → BorderColor…).</summary>
    private static Dictionary<string, List<string>> BaseFieldsKeysPerMethod()
    {
        string text = File.ReadAllText(Path.Combine(EDInputFormDir(), BaseFieldsFile));
        return BaseFieldsMethodRx.Matches(text).ToDictionary(
            m => m.Groups[1].Value,
            m => DirectKeyRx.Matches(m.Groups[2].Value).Select(k => k.Groups[1].Value).Distinct().ToList());
    }

    /// <summary>Popisky base polí ze sdíleného balíčku, mapované na skupinu, do které patří
    /// (skupinu určuje AddGroup na začátku metody).</summary>
    /// <param name="onlyMethod">Null = celý balíček; jinak jen jedna metoda (balíček `AddGrid`
    /// je volitelný - má ho tabulkový prvek, ne každý element).</param>
    private static Dictionary<string, string> BaseFieldsLabelToGroup(string? onlyMethod = null)
    {
        string text = File.ReadAllText(Path.Combine(EDInputFormDir(), BaseFieldsFile));
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in BaseFieldsMethodRx.Matches(text))
        {
            if (onlyMethod is not null && m.Groups[1].Value != onlyMethod) continue;
            if (onlyMethod is null && m.Groups[1].Value == "AddGrid") continue;
            var body = m.Groups[2].Value;
            var group = GroupLabelRx.Match(body) is { Success: true } g ? g.Groups[1].Value : "";
            foreach (Match f in LabeledFieldRx.Matches(body))
                result[f.Groups[2].Value] = group;
        }
        return result;
    }

    private static List<string> StyleFieldsSuffixes()
    {
        string text = File.ReadAllText(Path.Combine(EDInputFormDir(), "StyleFields.cls"));
        return StyleFieldsSuffixRx.Matches(text).Select(m => m.Groups[1].Value).Distinct().ToList();
    }

    private static string EDInputFormDir()
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
            "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "EDInputForm");
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;

    // ── Reflexe property path ─────────────────────────────────────────────────

    private static bool PropertyPathExists(Type type, string path)
    {
        var current = type;
        foreach (var segment in path.Split('.'))
        {
            var prop = current.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
            if (prop is null) return false;
            current = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        }
        return true;
    }
}
