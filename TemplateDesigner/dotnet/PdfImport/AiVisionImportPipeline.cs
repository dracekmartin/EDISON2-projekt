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

    /// <summary>Obrázek stránky, který dostal model (PNG v base64) - pro výpis posledního importu.</summary>
    public string? PageImageBase64 { get; private set; }

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
        PageImageBase64 = base64Png;

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
                ScheduleSettingsCatalog(scheduleType), ReportParametersCatalog(scheduleType));
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

        // Klíčové JŘ elementy (StopList/DepartureGrid, LineTable) model občas stochasticky vynechá -
        // jeden opakovaný pokus to typicky spraví. Jinak se pokračuje s viditelným varováním.
        if (visionResult is not null && scheduleType != ScheduleImportType.None
            && MissingCoreElements(visionResult, scheduleType) is { Length: > 0 } missingFirst)
        {
            setStatus($"AI nevrátilo: {missingFirst} - zkouším znovu...");
            try
            {
                var retryResult = await visionService.AnalyzeAsync(
                    base64Png, firstPage, schema, scheduleType, _apiKey,
                ScheduleSettingsCatalog(scheduleType), ReportParametersCatalog(scheduleType));
                AiDebugLog += "\n\n══ RETRY (chyběly klíčové elementy) ══\n" + visionService.DebugLog;
                if (retryResult is not null
                    && (MissingCoreElements(retryResult, scheduleType).Length == 0
                        || retryResult.Elements.Count > visionResult.Elements.Count))
                    visionResult = retryResult;
            }
            catch { /* retry je best-effort - ponechat první výsledek */ }
        }

        // 4. Sestavit šablonu
        setStatus("Vytvářím prvky...");

        // Jízdní řád bez AI nemá cenu dodělávat náhradním rozpoznáním: vznikne z něj hrubá šablona,
        // která vypadá jako výsledek importu, jen je špatně - a uživatel se to nedozví, protože
        // hláška zmizí se zavřením dialogu. Přesně takhle prošel 18. 8. import s neplatným klíčem.
        if (scheduleType != ScheduleImportType.None && visionResult is null)
            throw new Exception(
                "Jízdní řád se nepodařilo rozpoznat - AI služba neodpověděla"
                + (visionError is null ? "." : $": {visionError}")
                + Environment.NewLine + Environment.NewLine
                + "Import jízdního řádu bez ní nedává smysl (vznikla by hrubá šablona bez jízdního "
                + "řádu, jen z textů). Zkuste to prosím znovu; pokud potíž trvá, kontaktujte podporu M-line.");

        Template template;
        if (visionResult is not null && visionResult.Confidence >= 0.5f && visionResult.Elements.Count > 0)
        {
            template = await Task.Run(() => BuildTemplateFromVision(
                visionResult, firstPage, pageWidthMm, pageHeightMm,
                pdfPath, schema, visionService.DebugLog, scheduleType));

            int elCount = template.Sections.Sum(s => s.Elements.Count);
            int mappedFields = visionResult.TextMappings.Count(m => m.DbField is not null && m.Confidence >= 0.7f);
            string missing = MissingCoreElements(visionResult, scheduleType);
            string missingWarn = missing.Length > 0
                ? $" · ⚠ AI nerozpoznalo: {missing} - doplňte ručně z palety"
                : "";
            if (missingWarn.Length > 0)
                ImportLog.Add($"⚠ Nerozpoznáno: {missing} - doplňte ručně z palety.");
            setStatus($"AI Vision: {visionResult.Elements.Count} JŘ prvků, {mappedFields} polí namapováno, celkem {elCount} prvků{missingWarn}");
        }
        else
        {
            // Sem se dojde jen u obecného importu (bez agendy JŘ) nebo když AI odpověděla, ale
            // s nízkou jistotou - tam je hrubý výsledek pořád lepší než nic.
            setStatus("AI rozpoznání nestačilo, skládám šablonu z rozvržení PDF...");
            ImportLog.Add("⚠ AI rozpoznání nestačilo - šablona vznikla jen z rozvržení PDF, "
                          + "bez jízdního řádu. Zkuste import zopakovat.");
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
        string pdfPath, DataSchema? schema, string? visionDebugLog, ScheduleImportType scheduleType)
    {
        var scheduleElements = AiGuidedScheduleBuilder.BuildFromVision(
            visionResult, firstPage, 0, 0, schema);

        ApplyElementSettings(visionResult, scheduleElements, scheduleType);
        if (scheduleType == ScheduleImportType.LinkovyJr)
            AddGraphicPlaceholders(scheduleElements, visionResult, firstPage);
        LogTextCoverage(visionResult, scheduleElements);
        LogElementSettings(scheduleElements);

        if (AiGuidedScheduleBuilder.LastDebugInfo is not null)
            AiDebugLog = (visionDebugLog ?? "") + "\n\n── AiGuidedScheduleBuilder ──\n" + AiGuidedScheduleBuilder.LastDebugInfo;

        var section = new Section
        {
            Id = Guid.NewGuid(),
            Name = scheduleType == ScheduleImportType.LinkovyJr ? "Jízdní řád" : "JŘ Zastávkový",
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

        if (scheduleType == ScheduleImportType.LinkovyJr)
        {
            int dropped = DropStaticTextsBelowTable(section.Elements);
            if (dropped > 0)
                ImportLog.Add($"Texty pod tabulkou ({dropped}) se nepřevzaly - jsou to poznámky linky "
                              + "a ty tiskne prvek Poznámky z dat.");

            bool notesFound = section.Elements.OfType<NotesLegendElement>().Any();
            sections = SplitLineScheduleSections(section.Elements, contentHeight);
            if (sections.Any(s => s.Role == SectionRole.Footer))
                ImportLog.Add("Dole na stránce je patička - vložená jako sekce Patička na každé stránce.");
            if (!notesFound && section.Elements.OfType<LineTableElement>().Any())
                ImportLog.Add("Poznámky na zvolené stránce nejsou - vložené s výchozím nastavením pod "
                              + "tabulku. Tisk je přesune na další stránku, když se nevejdou.");
        }

        bool takesParams = TakesReportParameters(scheduleType);
        if (!takesParams)
            ImportLog.Add("Nastavení sestavy se z předlohy nepřebírá - nastavte ho v okně tisku.");
        var templateParams = takesParams ? BuildTemplateParameters(visionResult) : null;
        var printParams = takesParams ? BuildPrintParameters(visionResult) : null;

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

    /// <summary>Svislá tolerance (mm), o kterou smí prvek hlavičky zasahovat do tabulky a pořád
    /// patřit nad ni - texty těsně nad tabulkou mají rámeček, který se s ní dotýká.</summary>
    private const float LineSectionToleranceMm = 1f;

    /// <summary>
    /// Odebere pevné texty pod tabulkou spojů. U linkového JŘ jsou to vysvětlivky značek a texty
    /// o omezení spojů, tedy poznámky linky, které tiskne prvek Poznámky z dat; jako texty by se
    /// vytiskly stejně u každé linky (nález Jan 2. 10. u 940 IDZK: model oblast poznámek nevrátil
    /// a vysvětlivky skončily jako desítka textů). Text s datovým polem zůstává.
    /// </summary>
    internal static int DropStaticTextsBelowTable(List<TemplateElement> elements)
    {
        var table = elements.OfType<LineTableElement>().FirstOrDefault();
        if (table is null) return 0;

        float tableBottom = table.Y + (table.Height ?? 0f);
        return elements.RemoveAll(e => e is TextElement t
                                       && t.Y >= tableBottom - LineSectionToleranceMm
                                       && !t.Text.Contains('{'));
    }

    /// <summary>Mezera mezi tabulkou a poznámkami vedle ní (mm) - jako v systémové šabloně.</summary>
    private const float NotesBesideGapMm = 5f;

    /// <summary>
    /// Rozvrhne prvky linkového JŘ do pevné kostry systémových šablon linkového JŘ. Import z předlohy
    /// nečte stránkování: kdy poznámky přejdou na další stránku, rozhodne tisk podle dat linky.
    /// Z předlohy se bere jen to, jestli jsou poznámky pod tabulkou, nebo vedle ní.
    /// <list type="bullet">
    /// <item>Pod tabulkou (<c>linkovy-jr-ad-poznamky-pod-a4.json</c>): hlavička na každé stránce,
    /// tabulka přes stránky, poznámky jednou za ní jako jeden celek.</item>
    /// <item>Vedle tabulky (<c>linkovy-jr-ad-poznamky-vedle-a4.json</c>): hlavička na každé stránce,
    /// tabulka a poznámky v jedné sekci vedle sebe.</item>
    /// </list>
    /// </summary>
    /// <remarks>Poznámky v šabloně jsou vždy - na zvolené stránce chybívají (bývají až na další)
    /// a bez prvku by se nevytiskly vysvětlivky ke značkám. Tabulka a poznámky dostanou prázdnou
    /// výšku (počítá ji tisk z dat); ostatní prvky se přepočítají vůči sekci, do které padnou.
    /// Bez tabulky zůstane jedna sekce. Shodu se systémovými šablonami hlídá test.</remarks>
    internal static List<Section> SplitLineScheduleSections(List<TemplateElement> elements,
        float? contentHeightMm = null)
    {
        var table = elements.OfType<LineTableElement>().FirstOrDefault();
        if (table is null)
            return [NewSection("Jízdní řád", SectionRole.Body, SectionPrintBehavior.Once,
                SectionOverflowBehavior.KeepTogether, elements)];

        float tableTop = table.Y;
        float tableBottom = table.Y + (table.Height ?? 0f);

        var notes = elements.OfType<NotesLegendElement>().FirstOrDefault()
                    ?? DefaultNotes(table, tableBottom, elements);
        bool beside = notes.X >= table.X + table.Width / 2 && notes.Y < tableBottom - LineSectionToleranceMm;
        if (beside && table.X + table.Width > notes.X - NotesBesideGapMm)
            table.Width = Math.Max(notes.X - NotesBesideGapMm - table.X, 10f);

        var others = elements.Where(e => e != table && e != notes).ToList();
        var header = others.Where(e => e.Y < tableTop - LineSectionToleranceMm).ToList();
        var below = others.Where(e => !header.Contains(e) && e.Y >= tableBottom - LineSectionToleranceMm).ToList();
        var band = others.Where(e => !header.Contains(e) && !below.Contains(e)).ToList();

        // Patička: co leží pod tabulkou ve spodní pětině stránky (pás log u 940 IDZK je dole na obou
        // stránkách). Na každé stránce u spodku - ne jednou za poznámkami. Spodek poznámek se jako
        // hranice nebere: oblast od modelu do pásu log zasahovala.
        var footer = contentHeightMm is float ch
            ? below.Where(e => e.Y >= ch * FooterZoneFrom).ToList()
            : [];
        below = [.. below.Except(footer)];

        var sections = new List<Section>();
        if (header.Count > 0)
            sections.Add(NewSection("Hlavička", SectionRole.Header, SectionPrintBehavior.OnEveryPage,
                SectionOverflowBehavior.KeepTogether, header));
        if (beside)
        {
            sections.Add(NewSection("Jízdní řád a poznámky", SectionRole.Body, SectionPrintBehavior.Once,
                SectionOverflowBehavior.SplitAcrossPages, [table, notes, .. band]));
            if (below.Count > 0)
                sections.Add(NewSection("Pod tabulkou", SectionRole.Body, SectionPrintBehavior.Once,
                    SectionOverflowBehavior.KeepTogether, below));
        }
        else
        {
            sections.Add(NewSection("Jízdní řád", SectionRole.Body, SectionPrintBehavior.Once,
                SectionOverflowBehavior.SplitAcrossPages, [table, .. band]));
            sections.Add(NewSection("Poznámky", SectionRole.Body, SectionPrintBehavior.Once,
                SectionOverflowBehavior.KeepTogether, [notes, .. below]));
        }
        if (footer.Count > 0)
            sections.Add(NewSection("Patička", SectionRole.Footer, SectionPrintBehavior.OnEveryPage,
                SectionOverflowBehavior.KeepTogether, footer));

        table.Height = null;
        notes.Height = null;
        return sections;
    }

    /// <summary>Odkud (podíl výšky tiskové plochy) začíná pásmo patičky.</summary>
    private const float FooterZoneFrom = 0.8f;

    /// <summary>Nejmenší počet tvarů, ze kterých je grafika - jedna čára pod poznámkami obrázek není.</summary>
    private const int MinShapesPerGraphic = 3;

    /// <summary>
    /// Grafika pod tabulkou spojů (pás log u 940 IDZK) jako prázdné obrázky o její velikosti -
    /// obrázek z vektorové grafiky vytáhnout nejde, ale místo a rozměr ano, takže uživateli stačí
    /// nahrát soubor. Tvary bez textu se seskupí po řádcích; změřené místo má přednost před
    /// rámečkem, který ohlásil model (ten je jen odhad).
    /// </summary>
    internal static void AddGraphicPlaceholders(List<TemplateElement> elements, AiVisionResult vision, ExtractedPage page)
    {
        var table = vision.Elements.Where(e => e.Type.Equals("LineTable", StringComparison.OrdinalIgnoreCase)).ToList();
        if (table.Count == 0) return;
        float pod = table.Max(e => e.Bottom) * page.HeightMm;

        bool NeseText(float l, float t, float r, float b) => page.Words.Any(w =>
            w.X + w.Width > l && w.X < r && w.Y + w.Height > t && w.Y < b);

        // Tvar uvnitř poznámek (piktogram, rámeček značky) je jejich obsah, ne obrázek. Spodek
        // poznámek je poslední řádek jejich textu, ne oblast od modelu - ta sahala až přes pás log
        // u spodku stránky a obrázek pro něj pak nevznikl (třetí běh nad 940, 2. 10.).
        var poznamky = vision.Elements.Where(e => e.Type.Equals("NotesLegend", StringComparison.OrdinalIgnoreCase))
            .Select(e => (L: e.Left * page.WidthMm, T: e.Top * page.HeightMm,
                          R: e.Right * page.WidthMm, B: e.Bottom * page.HeightMm))
            .Select(p => (p.L, p.T, p.R, B: page.Words
                .Where(w => w.X + w.Width / 2 > p.L && w.X + w.Width / 2 < p.R && w.Y >= p.T && w.Y < p.B)
                .Select(w => w.Y + w.Height).DefaultIfEmpty(p.T).Max()))
            .ToList();
        bool VPoznamkach(float cx, float cy) => poznamky.Any(p => cx > p.L && cx < p.R && cy > p.T && cy < p.B);

        var tvary = page.Rectangles.Select(x => (L: x.X, T: x.Y, R: x.X + x.Width, B: x.Y + x.Height))
            .Concat(page.Lines.Select(c => (L: Math.Min(c.X1, c.X2), T: Math.Min(c.Y1, c.Y2),
                                            R: Math.Max(c.X1, c.X2), B: Math.Max(c.Y1, c.Y2))))
            .Where(s => s.T >= pod && !NeseText(s.L, s.T, s.R, s.B)
                        && !VPoznamkach((s.L + s.R) / 2, (s.T + s.B) / 2))
            .OrderBy(s => s.T)
            .ToList();

        var shluky = new List<List<(float L, float T, float R, float B)>>();
        foreach (var s in tvary)
        {
            if (shluky.Count > 0 && s.T <= shluky[^1].Max(x => x.B) + 1.5f) shluky[^1].Add(s);
            else shluky.Add([s]);
        }

        foreach (var shluk in shluky.Where(sh => sh.Count >= MinShapesPerGraphic))
        {
            float l = shluk.Min(x => x.L), t = shluk.Min(x => x.T), r = shluk.Max(x => x.R), b = shluk.Max(x => x.B);
            if (r - l < 10f || b - t < 1f) continue;

            elements.RemoveAll(e => e is ImageElement { ImageBase64: null } im
                && im.X < r && im.X + im.Width > l && im.Y < b && im.Y + (im.Height ?? 0f) > t);
            elements.Add(new ImageElement
            {
                Id = Guid.NewGuid(),
                X = l, Y = t, Width = r - l, Height = b - t,
                ZIndex = elements.Count > 0 ? elements.Max(e => e.ZIndex) + 1 : 0,
                ImageBase64 = null,
            });
        }
    }

    /// <summary>Poznámky s výchozím nastavením pod tabulkou, když je předloha na zvolené stránce nemá.</summary>
    private static NotesLegendElement DefaultNotes(LineTableElement table, float tableBottom,
        List<TemplateElement> elements) => new()
    {
        Id = Guid.NewGuid(),
        X = table.X,
        Y = tableBottom + 2f,
        Width = table.Width,
        Height = 10f,
        ZIndex = elements.Count > 0 ? elements.Max(e => e.ZIndex) + 1 : 0,
    };

    /// <summary>Návrhová výška sekce s tabulkou spojů - jako v systémových šablonách.</summary>
    private const float TableSectionDesignerHeightMm = 20f;

    /// <summary>Návrhová výška sekce s poznámkami - jako v systémových šablonách.</summary>
    private const float NotesSectionDesignerHeightMm = 15f;

    /// <summary>
    /// Sekce, jejíž první prvek leží u horní hrany: souřadnice se posunou o nejvyšší prvek a výška
    /// sekce sedí na spodek nejnižšího prvku s pevnou výškou.
    /// </summary>
    /// <remarks>⚠️ Tabulka spojů a poznámky se do výšky sekce nepočítají. Tisk bere jako výšku sekce
    /// větší z obsahu a návrhové výšky, takže výška tabulky z předlohy (140 mm) by se držela i pro
    /// kratší linku a poznámky by přešly na další stránku, i když se vejdou (nález Jan 2. 10.).
    /// Sekce s nimi má malou návrhovou výšku jako systémové šablony a skutečnou určí data.</remarks>
    private static Section NewSection(string name, SectionRole role, SectionPrintBehavior print,
        SectionOverflowBehavior overflow, List<TemplateElement> elements)
    {
        float top = elements.Count > 0 ? elements.Min(e => e.Y) : 0f;
        foreach (var el in elements) el.Y = MmMath.Round(el.Y - top);

        var pevne = elements.Where(e => e is not (LineTableElement or NotesLegendElement)).ToList();
        float bottom = pevne.Count > 0 ? pevne.Max(e => e.Y + (e.Height ?? 0f)) : 0f;
        float minimum = elements.Any(e => e is LineTableElement) ? TableSectionDesignerHeightMm
            : elements.Any(e => e is NotesLegendElement) ? NotesSectionDesignerHeightMm
            : 10f;

        return new Section
        {
            Id = Guid.NewGuid(),
            Name = name,
            Role = role,
            PrintBehavior = print,
            OverflowBehavior = overflow,
            DesignerHeight = Math.Max(MmMath.Round(bottom), minimum),
            Elements = elements,
        };
    }

    /// <summary>
    /// Zapíše do prvků vzhled, který AI poznala z předlohy, a zaznamená to do protokolu importu.
    /// </summary>
    private void ApplyElementSettings(AiVisionResult vision, List<TemplateElement> elements,
        ScheduleImportType scheduleType)
    {
        foreach (var (typ, vzor) in SettingsTargets(scheduleType))
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
        foreach (var (typ, prvek) in SettingsTargets(scheduleType))
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
    private string? ReportParametersCatalog(ScheduleImportType scheduleType)
    {
        if (!TakesReportParameters(scheduleType)) return null;
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
    private static IEnumerable<(string Typ, TemplateElement Prvek)> SettingsTargets(ScheduleImportType scheduleType)
    {
        if (scheduleType == ScheduleImportType.LinkovyJr)
        {
            yield return ("LineTable", new LineTableElement());
        }
        else
        {
            yield return ("StopList", new StopListElement());
            yield return ("DepartureGrid", new DepartureGridElement());
        }
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

            // Předloha ukazuje, jak to má nastavené její linka; pevná hodnota v šabloně by přebila
            // nastavení všech ostatních linek (zkouška 2. 10.: „Sloupec tarifních čísel = Tisknout").
            if (RidiSeLinkou(klic))
            {
                ImportLog.Add($"{PopisParametru(klic)} - ponecháno podle nastavení linky.");
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

    /// <summary>
    /// Bere import volby sestavy z předlohy? U linkového JŘ ne (rozhodnutí Jan 2. 10.): ve třech
    /// zkušebních bězích model pokaždé uhodl jiné volby a zčásti špatně (obousměrný tisk, název pro
    /// MHD, stránka pro každou zastávku) - a hodnota v šabloně přebije nastavení uživatele (#12843).
    /// Import linkového JŘ řeší jen rozvržení; volby si uživatel nastaví v okně tisku.
    /// </summary>
    internal static bool TakesReportParameters(ScheduleImportType scheduleType) =>
        scheduleType != ScheduleImportType.LinkovyJr;

    /// <summary>Volba s výchozí možností „Automaticky (dle linky)" (konvence `ux-conventions §8`).</summary>
    private bool RidiSeLinkou(string name) =>
        _knownParameters?.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            ?.Options?.Any(o => o.StartsWith("Automaticky", StringComparison.OrdinalIgnoreCase)) == true;

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
            if (RidiSeLinkou(key)) continue;
            if (knownLookup.TryGetValue(key, out var known))
            {
                // Agenda má definici - převzít typ, label, options
                result.Add(new TemplateParameter
                {
                    Name = known.Name,
                    Label = known.Label,
                    Type = known.Type,
                    DefaultValue = val,
                    Options = known.Options,
                    OptionKeys = known.OptionKeys,
                    OptionLabels = known.OptionLabels
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
    /// Vrátí seznam klíčových JŘ elementů, které ve vision výsledku chybí (prázdný string = vše
    /// na místě). Řídí retry a varování uživateli.
    /// </summary>
    internal static string MissingCoreElements(AiVisionResult vision, ScheduleImportType scheduleType)
    {
        bool Has(string type) => vision.Elements.Any(e => e.Type.Equals(type, StringComparison.OrdinalIgnoreCase));

        var missing = new List<string>();
        if (scheduleType == ScheduleImportType.LinkovyJr)
        {
            if (!Has("LineTable")) missing.Add("tabulku spojů");
        }
        else
        {
            if (!Has("StopList")) missing.Add("seznam zastávek");
            if (!Has("DepartureGrid")) missing.Add("mřížku odjezdů");
        }
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
