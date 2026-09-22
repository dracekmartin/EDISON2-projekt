using System.IO;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Services;

namespace TemplateDesigner.PdfImport;

/// <summary>
/// Orchestruje AI Vision import: extrakce PDF → rasterizace → AI vision → engine → Template.
/// Extrahováno z PdfImportDialogViewModel - VM volá tuto třídu, neobsahuje business logiku.
/// </summary>
public class AiVisionImportPipeline
{
    private readonly string _apiKey;
    private readonly IDataSchemaProvider? _schemaProvider;
    private readonly string? _agendaCode;
    private readonly IReadOnlyList<TemplateParameter>? _knownParameters;

    public string? AiDebugLog { get; private set; }

    /// <summary>Co import z předlohy nastavil a co nerozpoznal - pro uživatele, ne pro vývojáře.</summary>
    public List<string> ImportLog { get; } = [];

    /// <summary>Úplný soupis vzniklých prvků a jejich nastavení - pro kontrolu proti předloze
    /// a pro porovnání dvou běhů mezi sebou. Ukládá se vedle PDF.</summary>
    public List<string> DetailedLog { get; } = [];

    public AiVisionImportPipeline(
        string apiKey, IDataSchemaProvider? schemaProvider, string? agendaCode,
        IReadOnlyList<TemplateParameter>? knownParameters = null)
    {
        _apiKey = apiKey;
        _schemaProvider = schemaProvider;
        _agendaCode = agendaCode;
        _knownParameters = knownParameters;
    }

    /// <summary>
    /// Spustí kompletní AI Vision import pipeline.
    /// Vrací šablonu nebo fallback na heuristiku pokud AI selže.
    /// </summary>
    public async Task<Template> ImportAsync(
        string pdfPath, int[]? pages, ScheduleImportType scheduleType,
        bool enableDebug, Action<string> setStatus)
    {
        setStatus("Extrahuji obsah PDF...");

        // 1. Extrahovat stránky
        var extractedPages = new List<ExtractedPage>();
        float pageWidthMm = 210f, pageHeightMm = 297f;

        await Task.Run(() =>
        {
            using var doc = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
            var pageNumbers = pages ?? Enumerable.Range(1, doc.NumberOfPages).ToArray();

            foreach (int pageNum in pageNumbers)
            {
                if (pageNum < 1 || pageNum > doc.NumberOfPages) continue;
                var extracted = PdfPageExtractor.Extract(doc, pageNum);
                pageWidthMm = extracted.WidthMm;
                pageHeightMm = extracted.HeightMm;
                extractedPages.Add(extracted);
            }
        });

        if (extractedPages.Count == 0)
            throw new Exception("PDF neobsahuje žádné stránky.");

        var firstPage = extractedPages[0];

        // 2. Rasterizovat stránku na PNG
        setStatus("Rasterizuji PDF stránku...");
        string base64Png = await Task.Run(() => PdfPageRasterizer.RasterizeToBase64Png(firstPage));

        // 3. AI Vision analýza
        setStatus("AI analyzuje strukturu...");
        var visionService = new AiVisionImportService();
        AiVisionResult? visionResult = null;
        string? visionError = null;

        DataSchema? schema = null;
        try
        {
            schema = !string.IsNullOrEmpty(_agendaCode)
                ? _schemaProvider?.GetSchema(_agendaCode)
                : null;

            visionResult = await visionService.AnalyzeAsync(
                base64Png, firstPage, schema, scheduleType, _apiKey,
                ScheduleSettingsCatalog(scheduleType), ReportParametersCatalog());
        }
        catch (Exception ex)
        {
            visionError = ex.Message;
            setStatus($"AI se nepodařilo zavolat ({ex.Message}).");
        }
        finally
        {
            AiDebugLog = visionService.DebugLog;
        }

        // Klíčové JŘ elementy (StopList/DepartureGrid) model občas stochasticky vynechá -
        // jeden opakovaný pokus to typicky spraví. Jinak se pokračuje s viditelným varováním.
        if (visionResult is not null && scheduleType != ScheduleImportType.None
            && MissingCoreElements(visionResult) is { Length: > 0 } missingFirst)
        {
            setStatus($"AI nevrátilo: {missingFirst} - zkouším znovu...");
            try
            {
                var retryResult = await visionService.AnalyzeAsync(
                    base64Png, firstPage, schema, scheduleType, _apiKey,
                ScheduleSettingsCatalog(scheduleType), ReportParametersCatalog());
                AiDebugLog += "\n\n══ RETRY (chyběly klíčové elementy) ══\n" + visionService.DebugLog;
                if (retryResult is not null
                    && (MissingCoreElements(retryResult).Length == 0
                        || retryResult.Elements.Count > visionResult.Elements.Count))
                    visionResult = retryResult;
            }
            catch { /* retry je best-effort - ponechat první výsledek */ }
        }

        // 4. Sestavit šablonu
        setStatus("Vytvářím elementy...");

        // Jízdní řád bez AI nemá cenu dodělávat náhradním rozpoznáním: vznikne z něj hrubá šablona,
        // která vypadá jako výsledek importu, jen je špatně - a uživatel se to nedozví, protože
        // hláška zmizí se zavřením dialogu. Přesně takhle prošel 18. 8. import s neplatným klíčem.
        if (scheduleType != ScheduleImportType.None && visionResult is null)
            throw new Exception(
                "Jízdní řád se nepodařilo rozpoznat - AI služba neodpověděla"
                + (visionError is null ? "." : $": {visionError}")
                + Environment.NewLine + Environment.NewLine
                + "Import jízdního řádu bez ní nedává smysl (vznikla by hrubá šablona bez zastávek "
                + "a odjezdů). Zkuste to prosím znovu; pokud potíž trvá, kontaktujte podporu M-line.");

        Template template;
        if (visionResult is not null && visionResult.Confidence >= 0.5f && visionResult.Elements.Count > 0)
        {
            template = await Task.Run(() => BuildTemplateFromVision(
                visionResult, firstPage, pageWidthMm, pageHeightMm,
                pdfPath, schema, visionService.DebugLog));

            int elCount = template.Sections.Sum(s => s.Elements.Count);
            int mappedFields = visionResult.TextMappings.Count(m => m.DbField is not null && m.Confidence >= 0.7f);
            string missingWarn = MissingCoreElements(visionResult) is { Length: > 0 } m
                ? $" · ⚠ AI nerozpoznalo: {m} - doplňte ručně z palety"
                : "";
            if (missingWarn.Length > 0)
                ImportLog.Add($"⚠ Nerozpoznáno: {MissingCoreElements(visionResult)} - doplňte ručně z palety.");
            setStatus($"AI Vision: {visionResult.Elements.Count} JŘ elementů, {mappedFields} polí namapováno, celkem {elCount} elementů{missingWarn}");
        }
        else
        {
            // Sem se dojde jen u obecného importu (bez agendy JŘ) nebo když AI odpověděla, ale
            // s nízkou jistotou - tam je hrubý výsledek pořád lepší než nic.
            setStatus("AI rozpoznání nestačilo, skládám šablonu z rozvržení PDF...");
            ImportLog.Add("⚠ AI rozpoznání nestačilo - šablona vznikla jen z rozvržení PDF, "
                          + "bez zastávek a odjezdů. Zkuste import zopakovat.");
            template = await Task.Run(() =>
            {
                var converter = new PdfToTemplateConverter
                {
                    EnableDebug = enableDebug,
                    ScheduleImportType = scheduleType
                };
                return converter.Convert(pdfPath, pages);
            });
        }

        return template;
    }

    private Template BuildTemplateFromVision(
        AiVisionResult visionResult, ExtractedPage firstPage,
        float pageWidthMm, float pageHeightMm,
        string pdfPath, DataSchema? schema, string? visionDebugLog)
    {
        var scheduleElements = AiGuidedScheduleBuilder.BuildFromVision(
            visionResult, firstPage, 0, 0, schema);

        ApplyElementSettings(visionResult, scheduleElements);
        LogTextCoverage(visionResult, scheduleElements);
        LogElementSettings(scheduleElements);

        if (AiGuidedScheduleBuilder.LastDebugInfo is not null)
            AiDebugLog = (visionDebugLog ?? "") + "\n\n── AiGuidedScheduleBuilder ──\n" + AiGuidedScheduleBuilder.LastDebugInfo;

        var section = new Section
        {
            Id = Guid.NewGuid(),
            Name = "JŘ Zastávkový",
            PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether,
            Elements = scheduleElements
        };

        var sections = new List<Section> { section };
        var (mTop, mBottom, mLeft, mRight) = DetectMarginsFromElements(sections, pageWidthMm, pageHeightMm);

        foreach (var el in section.Elements)
        {
            el.X -= mLeft;
            el.Y -= mTop;
            if (el.X < 0) el.X = 0;
            if (el.Y < 0) el.Y = 0;
        }

        float contentHeight = pageHeightMm - mTop - mBottom;
        section.DesignerHeight = Math.Max(contentHeight, 10f);

        // Pojistka proti přetečení hned po importu: AI souřadnice můžou mírně přetéct stránku
        // (rel. bottom > 1) - element s pevnými rozměry clampnout do tiskové plochy, jinak
        // šablona rovnou svítí varováním. JŘ elementy obsah do menšího boxu vejdou auto-scalem.
        float printableWidth = pageWidthMm - mLeft - mRight;
        foreach (var el in section.Elements)
        {
            if (el.Height is float h && el.Y + h > contentHeight)
                el.Height = Math.Max(contentHeight - el.Y, 3f);
            if (el.X + el.Width > printableWidth)
                el.Width = Math.Max(printableWidth - el.X, 3f);
        }

        var templateParams = BuildTemplateParameters(visionResult);
        var printParams = BuildPrintParameters(visionResult);

        return new Template
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileNameWithoutExtension(pdfPath),
            AgendaCode = _agendaCode,
            PageSize = new PageSize
            {
                Width = pageWidthMm,
                Height = pageHeightMm,
                MarginTop = mTop,
                MarginBottom = mBottom,
                MarginLeft = mLeft,
                MarginRight = mRight
            },
            Sections = sections,
            Parameters = templateParams,
            PrintParameters = printParams,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Zapíše do prvků vzhled, který AI poznala z předlohy, a zaznamená to do protokolu importu.
    /// Zatím jen seznam zastávek - na něm se ověřuje, co model zvládne poznat (rozhodnutí Jan 18. 8.).
    /// </summary>
    private void ApplyElementSettings(AiVisionResult vision, List<TemplateElement> elements)
    {
        foreach (var (typ, vzor) in SettingsTargets())
        {
            var cil = elements.FirstOrDefault(e => e.GetType() == vzor.GetType());
            if (cil is null) continue;
            if (!vision.ElementSettings.TryGetValue(typ, out var settings)) continue;

            var applied = ElementSettingsFromImage.Apply(cil, settings);
            if (applied.Count == 0) continue;

            ImportLog.Add($"{ElementNazev(cil)} - z předlohy nastaveno:");
            ImportLog.AddRange(applied.Select(l => "   " + l));
        }
    }

    /// <summary>
    /// Kolik textů z předlohy skončilo v šabloně. Bez tohohle čísla nejde poznat, jestli model
    /// texty nenašel, nebo jsme je zahodili sami (text uvnitř jízdního řádu si kreslí prvek sám,
    /// dekorace bez datového pole se nevkládá) - a to je první otázka, když v šabloně texty chybí.
    /// </summary>
    private void LogTextCoverage(AiVisionResult vision, List<TemplateElement> elements)
    {
        int found = vision.TextMappings.Count;
        int placed = elements.OfType<TextElement>().Count();
        if (found == 0 && placed == 0) return;

        ImportLog.Add(placed >= found
            ? $"Texty z předlohy: {placed}"
            : $"Texty z předlohy: {placed} z {found}, které model našel "
              + "(zbytek leží uvnitř jízdního řádu, kde si texty kreslí prvek sám, nebo je to dekorace).");

        foreach (var note in AiGuidedScheduleBuilder.UserNotes)
            ImportLog.Add("⚠ " + note);

        if (AiGuidedScheduleBuilder.SkippedNumericBlocks is > 0 and var cisla)
            ImportLog.Add($"Vynecháno {cisla} samostatných čísel - patří do jízdního řádu, "
                          + "data si prvek při tisku načte sám.");
    }

    /// <summary>
    /// Do podrobného protokolu vypíše u každého vzniklého prvku, čím se liší od čerstvě vloženého -
    /// tedy co všechno import nastavil, ať už z odpovědi modelu, nebo změřením z PDF. Bez toho jde
    /// zkontrolovat jen to, co řekl model, a ne skutečný výsledek.
    /// </summary>
    private void LogElementSettings(List<TemplateElement> elements)
    {
        foreach (var el in elements)
        {
            var rozdily = ElementSettingsFromImage.DiffFromDefault(el);
            DetailedLog.Add("");
            DetailedLog.Add($"{ElementNazev(el)} - pozice {el.X:0.#}, {el.Y:0.#} mm, "
                            + $"velikost {el.Width:0.#} × {el.Height?.ToString("0.#") ?? "auto"} mm");
            if (rozdily.Count == 0)
            {
                DetailedLog.Add("   (vše ponecháno na výchozích hodnotách)");
                continue;
            }
            foreach (var r in rozdily) DetailedLog.Add("   " + r);
        }
    }

    // Text se v protokolu pozná jen podle svého obsahu (jich je na stránce dvacet), zbytek
    // jménem z palety.
    private static string ElementNazev(TemplateElement el) =>
        el is TextElement t ? "Text: " + t.Text : ElementNames.ForUser(el);

    /// <summary>
    /// Na co se u jízdněřádových prvků ptát - soupis jejich vlastních nastavitelných vlastností.
    /// Klíč skupiny je typ prvku, jak ho model používá v odpovědi (StopList, DepartureGrid…).
    /// </summary>
    private static string? ScheduleSettingsCatalog(ScheduleImportType scheduleType)
    {
        if (scheduleType == ScheduleImportType.None) return null;

        var sb = new System.Text.StringBuilder();
        foreach (var (typ, prvek) in SettingsTargets())
        {
            sb.AppendLine($"[{typ}] {ElementNazev(prvek)}:");
            sb.Append(ElementSettingsFromImage.BuildCatalog(prvek));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>
    /// Volby sestavy tak, jak je popisuje agenda - popisek, typ, nabídka i podmínka platnosti.
    /// Ručně psaný výčet v promptu uměl tři volby z patnácti a u druhé jízdněřádové agendy
    /// neplatil vůbec; agenda přitom každou volbu popisuje sama.
    /// </summary>
    private string? ReportParametersCatalog()
    {
        if (_knownParameters is null || _knownParameters.Count == 0) return null;

        var sb = new System.Text.StringBuilder();
        foreach (var p in _knownParameters)
        {
            string tvar = p.Type switch
            {
                "bool"   => "\"true\" nebo \"false\"",
                "number" => "číslo",
                "enum" when p.Options is { Count: > 0 } o => "jedna z hodnot " + string.Join(" / ", o.Select(x => $"\"{x}\"")),
                _        => "text"
            };
            sb.Append($"- \"{p.Name}\" ({p.Label}): {tvar}");
            if (!string.IsNullOrWhiteSpace(p.VisibleWhen))
                sb.Append($"; uplatní se jen při {p.VisibleWhen}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>Prvky, na jejichž vzhled se model ptá - typ v odpovědi × čerstvý prvek jako vzor.</summary>
    private static IEnumerable<(string Typ, TemplateElement Prvek)> SettingsTargets()
    {
        yield return ("StopList", new StopListElement());
        yield return ("DepartureGrid", new DepartureGridElement());
        yield return ("NotesLegend", new NotesLegendElement());
    }

    /// <summary>
    /// Hodnoty nastavení sestavy, které model poznal z předlohy - do místa, ze kterého je čte tisk.
    /// Do 19. 8. se ukládaly jen k definicím parametrů, kam se při tisku nekouká, takže rozpoznaná
    /// hodnota se zahodila (vývěska bez šipek se stejně vytiskla se šipkami).
    ///
    /// <para>Zapisuje se <b>jen to, co model opravdu poznal</b>, ne celá sada (rozhodnutí Jan
    /// 19. 8.). Hodnoty uložené v šabloně mají přednost před tím, co si uživatel nastavil pro
    /// agendu - kdyby import uložil i volby, kterých se nikdo nedotkl, začala by šablona přebíjet
    /// nastavení uživatele (#12843).</para>
    /// </summary>
    internal Dictionary<string, string>? BuildPrintParameters(AiVisionResult vision)
    {
        if (vision.TemplateParameters.Count == 0) return null;

        var znameParametry = new HashSet<string>(
            (_knownParameters ?? []).Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

        var hodnoty = new Dictionary<string, string>();
        foreach (var (klic, hodnota) in vision.TemplateParameters)
        {
            // Volbu, kterou agenda nezná, tisk stejně zahodí - a v šabloně by jen mátla.
            if (znameParametry.Count > 0 && !znameParametry.Contains(klic))
            {
                ImportLog.Add($"⚠ Volbu {klic} tahle agenda nemá - přeskočena.");
                continue;
            }
            hodnoty[klic] = hodnota;
        }

        if (hodnoty.Count > 0)
        {
            ImportLog.Add("Nastavení sestavy z předlohy:");
            foreach (var (klic, hodnota) in hodnoty)
                ImportLog.Add($"   {PopisParametru(klic)} = {hodnota}");
        }

        return hodnoty.Count > 0 ? hodnoty : null;
    }

    /// <summary>Popisek volby tak, jak ho zná uživatel z nastavení tisku; jinak holý klíč.</summary>
    private string PopisParametru(string name) =>
        _knownParameters?.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.Label ?? name;

    /// <summary>
    /// Převede AI-detekované parametry na TemplateParameter objekty.
    /// Typ a label se odvozují z knownParameters (z AgendaAPI.GetParameters), ne hardcoded.
    /// </summary>
    private List<TemplateParameter>? BuildTemplateParameters(AiVisionResult visionResult)
    {
        if (visionResult.TemplateParameters.Count == 0) return null;

        // Pokud máme definice parametrů z agendy, použít jejich typy a labely
        var knownLookup = new Dictionary<string, TemplateParameter>(StringComparer.OrdinalIgnoreCase);
        if (_knownParameters is not null)
        {
            foreach (var p in _knownParameters)
                knownLookup[p.Name] = p;
        }

        var result = new List<TemplateParameter>();
        foreach (var (key, val) in visionResult.TemplateParameters)
        {
            if (knownLookup.TryGetValue(key, out var known))
            {
                // Agenda má definici - převzít typ, label, options
                result.Add(new TemplateParameter
                {
                    Name = known.Name,
                    Label = known.Label,
                    Type = known.Type,
                    DefaultValue = val,
                    Options = known.Options
                });
            }
            else
            {
                // Fallback: heuristika z hodnoty
                result.Add(new TemplateParameter
                {
                    Name = key,
                    Label = key,
                    Type = InferParameterType(val),
                    DefaultValue = val
                });
            }
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>Heuristická inference typu z hodnoty (fallback pokud schema nemá parametr).</summary>
    private static string InferParameterType(string value)
    {
        var v = value.Trim();
        return v.ToLowerInvariant() switch
        {
            "true" or "false" => "bool",
            _ when int.TryParse(v, out _) => "number",
            _ when float.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _) => "number",
            _ => "string"
        };
    }

    /// <summary>
    /// Vrátí čárkami oddělený seznam klíčových JŘ elementů, které ve vision výsledku chybí
    /// (prázdný string = vše na místě). Řídí retry a varování uživateli.
    /// </summary>
    private static string MissingCoreElements(AiVisionResult vision)
    {
        var missing = new List<string>();
        if (!vision.Elements.Any(e => e.Type.Equals("StopList", StringComparison.OrdinalIgnoreCase)))
            missing.Add("seznam zastávek");
        if (!vision.Elements.Any(e => e.Type.Equals("DepartureGrid", StringComparison.OrdinalIgnoreCase)))
            missing.Add("mřížku odjezdů");
        return string.Join(" a ", missing);
    }

    /// <summary>Detekce okrajů z pozic elementů.</summary>
    private static (float Top, float Bottom, float Left, float Right) DetectMarginsFromElements(
        List<Section> sections, float pageWidthMm, float pageHeightMm)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxRight = 0f, maxBottom = 0f;

        foreach (var section in sections)
        {
            foreach (var el in section.Elements)
            {
                minX = Math.Min(minX, el.X);
                minY = Math.Min(minY, el.Y);
                maxRight = Math.Max(maxRight, el.X + el.Width);
                maxBottom = Math.Max(maxBottom, el.Y + (el.Height ?? 0));
            }
        }

        if (minX == float.MaxValue)
            return (10f, 10f, 10f, 10f);

        float marginLeft = Math.Clamp(minX - 0.1f, 0.5f, 50f);
        float marginTop = Math.Clamp(minY - 0.1f, 0.5f, 50f);
        float marginRight = Math.Clamp(pageWidthMm - maxRight - 0.1f, 0.5f, 50f);
        float marginBottom = Math.Clamp(pageHeightMm - maxBottom - 0.1f, 0.5f, 50f);

        return (marginTop, marginBottom, marginLeft, marginRight);
    }
}
