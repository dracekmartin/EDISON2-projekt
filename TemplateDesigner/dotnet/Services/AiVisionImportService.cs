using System.Text;
using System.Text.Json;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.PdfImport;

namespace TemplateDesigner.Services;

/// <summary>
/// AI Vision import: pošle screenshot PDF stránky do Claude Sonnet s DataSchema.
/// AI vrátí sémantickou strukturu s relativními souřadnicemi (0.0–1.0).
/// Engine pak přepočítá na mm a zpřesní rozměry z PdfPig geometrie.
/// </summary>
public class AiVisionImportService
{
    private readonly string _visionModel;

    public AiVisionImportService(string? visionModel = null)
    {
        _visionModel = visionModel
            ?? Config.AppSettings.Current?.AiAssistant.VisionModel
            ?? "claude-sonnet-5";
    }

    /// <summary>Debug log z posledního volání.</summary>
    public string? DebugLog { get; private set; }

    /// <summary>
    /// Analyzuje screenshot PDF stránky pomocí AI vision.
    /// Vrací sémantickou strukturu s relativními souřadnicemi nebo null při selhání.
    /// </summary>
    /// <param name="elementSettingsCatalog">Soupis nastavitelných vlastností prvku (klíč → povolené
    /// hodnoty) sestavený z prvku samotného; null = na vzhled prvků se neptáme.</param>
    public async Task<AiVisionResult?> AnalyzeAsync(
        string base64Png,
        ExtractedPage page,
        DataSchema? schema,
        ScheduleImportType scheduleType,
        string apiKey,
        string? elementSettingsCatalog = null)
    {
        var log = new StringBuilder();
        var systemPrompt = GetSystemPrompt(scheduleType, elementSettingsCatalog);
        var userPrompt = BuildUserPrompt(page, schema);

        log.AppendLine("╔══════════════════════════════════════════════════════════════╗");
        log.AppendLine("║              AI VISION IMPORT - DEBUG LOG                   ║");
        log.AppendLine("╚══════════════════════════════════════════════════════════════╝");
        log.AppendLine();
        log.AppendLine($"Model: {_visionModel}");
        log.AppendLine($"ScheduleImportType: {scheduleType}");
        log.AppendLine($"Schema: {schema?.AgendaCode ?? "(none)"}");
        log.AppendLine($"Page: {page.WidthMm:0.#} x {page.HeightMm:0.#} mm");
        log.AppendLine($"Image size: ~{base64Png.Length / 1024} KB (base64)");
        log.AppendLine();

        log.AppendLine("┌──────────────────────────────────────────────────────────────┐");
        log.AppendLine("│ SYSTEM PROMPT                                                │");
        log.AppendLine("└──────────────────────────────────────────────────────────────┘");
        log.AppendLine(systemPrompt);
        log.AppendLine();

        log.AppendLine("┌──────────────────────────────────────────────────────────────┐");
        log.AppendLine("│ USER PROMPT (text part)                                      │");
        log.AppendLine("└──────────────────────────────────────────────────────────────┘");
        log.AppendLine(userPrompt);
        log.AppendLine();

        string? responseText;
        try
        {
            // Sonnet 5: adaptivní myšlení necháváme ZAPNUTÉ (jednorázová percepční úloha -
            // rozmyšlení layoutu zlepší detekci) → max_tokens s rezervou (myšlení se do něj počítá).
            // Temperature 0.2 = deterministická detekce; Sonnet 5+ nedefaultní hodnoty odmítá → null.
            responseText = await AnthropicApiClient.CallApiWithImageAsync(
                apiKey, systemPrompt, base64Png, userPrompt,
                model: _visionModel, maxTokens: 16000,
                temperature: AnthropicApiClient.ModelSupportsTemperature(_visionModel) ? 0.2 : null);
        }
        catch (Exception ex)
        {
            log.AppendLine("┌──────────────────────────────────────────────────────────────┐");
            log.AppendLine("│ API ERROR                                                    │");
            log.AppendLine("└──────────────────────────────────────────────────────────────┘");
            log.AppendLine(ex.ToString());
            DebugLog = log.ToString();
            throw;
        }

        log.AppendLine("┌──────────────────────────────────────────────────────────────┐");
        log.AppendLine("│ AI RAW RESPONSE                                              │");
        log.AppendLine("└──────────────────────────────────────────────────────────────┘");
        log.AppendLine(responseText ?? "(null/empty)");
        log.AppendLine();

        if (string.IsNullOrWhiteSpace(responseText))
        {
            log.AppendLine("→ VÝSLEDEK: null (prázdná odpověď)");
            DebugLog = log.ToString();
            return null;
        }

        AiVisionResult? result;
        try
        {
            result = ParseResponse(responseText);
        }
        catch (Exception ex)
        {
            log.AppendLine("┌──────────────────────────────────────────────────────────────┐");
            log.AppendLine("│ PARSE ERROR                                                  │");
            log.AppendLine("└──────────────────────────────────────────────────────────────┘");
            log.AppendLine(ex.ToString());
            DebugLog = log.ToString();
            return null;
        }

        // Logovat výsledek
        if (result is not null)
        {
            log.AppendLine("┌──────────────────────────────────────────────────────────────┐");
            log.AppendLine("│ PARSED RESULT (relative 0.0–1.0)                             │");
            log.AppendLine("└──────────────────────────────────────────────────────────────┘");
            log.AppendLine($"Confidence: {result.Confidence:0.##}");
            log.AppendLine($"ScheduleType: {result.ScheduleType}");
            log.AppendLine($"Elements: {result.Elements.Count}");
            foreach (var el in result.Elements)
            {
                log.AppendLine($"  {el.Type}: [{el.Left:0.###},{el.Top:0.###} → {el.Right:0.###},{el.Bottom:0.###}]");
                log.AppendLine($"    → mm: [{el.Left * page.WidthMm:0.#},{el.Top * page.HeightMm:0.#} → {el.Right * page.WidthMm:0.#},{el.Bottom * page.HeightMm:0.#}]");
                if (el.DayGroups.Count > 0)
                    log.AppendLine($"    DayGroups: {string.Join(", ", el.DayGroups.Select(g => $"\"{g.Header}\""))}");
                if (el.Columns.Count > 0)
                    log.AppendLine($"    Columns: [{string.Join(", ", el.Columns)}]");
                if (el.Properties.Count > 0)
                    log.AppendLine($"    Properties: {string.Join(", ", el.Properties.Select(p => $"{p.Key}={p.Value}"))}");
            }
            log.AppendLine($"TextMappings: {result.TextMappings.Count}");
            foreach (var m in result.TextMappings)
            {
                string autoApply = m.Confidence >= 0.7f && m.PlaceholderText is not null ? " ✅ AUTO-APPLY" : "";
                log.AppendLine($"  \"{Truncate(m.Text, 40)}\" → role={m.Role}, pos=[{m.X:0.###},{m.Y:0.###}], dbField={m.DbField ?? "null"}, conf={m.Confidence:0.##}{autoApply}");
            }
            if (result.ImageMappings.Count > 0)
            {
                log.AppendLine($"ImageMappings: {result.ImageMappings.Count}");
                foreach (var im in result.ImageMappings)
                    log.AppendLine($"  role={im.Role}, pos=[{im.X:0.###},{im.Y:0.###} {im.W:0.###}×{im.H:0.###}], dbField={im.DbField ?? "null"}");
            }
            if (result.TemplateParameters.Count > 0)
            {
                log.AppendLine($"TemplateParameters: {result.TemplateParameters.Count}");
                foreach (var tp in result.TemplateParameters)
                    log.AppendLine($"  {tp.Key} = {tp.Value}");
            }
            if (result.Notes is not null)
                log.AppendLine($"Notes: {result.Notes}");
        }

        DebugLog = log.ToString();
        return result;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";

    // ── System prompt - composable per agenda type ────────────────────

    private static string GetSystemPrompt(ScheduleImportType type, string? elementSettingsCatalog = null)
    {
        var sb = new StringBuilder();
        var context = GetAgendaPromptContext(type);

        sb.AppendLine(context.RoleIntro);
        sb.AppendLine();
        sb.AppendLine(CoordinateSystemSection);
        sb.AppendLine();
        sb.AppendLine(context.StructureDescription);
        sb.AppendLine();
        sb.AppendLine(BuildRulesSection(context));
        sb.AppendLine();
        sb.AppendLine(BuildPropertiesSection(context));
        sb.AppendLine();
        if (context.TemplateParameters.Count > 0)
        {
            sb.AppendLine(BuildTemplateParametersSection(context));
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(elementSettingsCatalog))
        {
            sb.AppendLine(BuildElementSettingsSection(elementSettingsCatalog));
            sb.AppendLine();
        }
        sb.AppendLine(BuildOutputSection(context));

        return sb.ToString();
    }

    /// <summary>
    /// Otázka na vzhled prvku poskládaná z jeho vlastních vlastností (ne ručně psaný seznam - ten
    /// se s prvky rozešel). Model má odpovídat jen na to, co z obrázku **opravdu vidí**; nastavení,
    /// která se vizuálně neliší, patří uživateli, ne odhadu.
    /// </summary>
    private static string BuildElementSettingsSection(string catalog) =>
        @"NASTAVENÍ VZHLEDU SEZNAMU ZASTÁVEK (""elementSettings"" → ""StopList""):
Projdi obrázek a vyplň JEN ty vlastnosti, které z něj bezpečně poznáš (šipka vlevo/vpravo, značky
před názvem zastávky nebo za ním, počet oddílů vedle sebe, barvy…). Klíč použij PŘESNĚ tak, jak je
uvedený níže, hodnotu právě v uvedeném tvaru.
Co nevidíš nebo si nejsi jistý, VYNECH - výchozí hodnota je lepší než hádaná.
" + catalog;

    /// <summary>
    /// Kontextové informace pro generování promptu per agenda.
    /// Pro přidání nové agendy: přidat case do GetAgendaPromptContext a vytvořit context.
    /// </summary>
    private static AgendaPromptContext GetAgendaPromptContext(ScheduleImportType type) => type switch
    {
        ScheduleImportType.ZastavkovyJrMhd => ZastavkovyJrMhdPromptContext(),
        _ => GenericPromptContext()
    };

    // ── Shared prompt sections ──────────────────────────────────────────

    private const string CoordinateSystemSection = @"SOUŘADNICOVÝ SYSTÉM - RELATIVNÍ (0.0–1.0):
- VŠECHNY pozice vrať jako poměr: 0.0 = levý/horní okraj, 1.0 = pravý/dolní okraj stránky.
- Příklad: objekt uprostřed stránky → left: 0.5, top: 0.5.
- Objekt v pravém dolním rohu → right: 1.0, bottom: 1.0.
- NIKDY nepoužívej pixely ani milimetry - vždy poměr 0.0–1.0.";

    private static string BuildRulesSection(AgendaPromptContext ctx)
    {
        var sb = new StringBuilder("PRAVIDLA:\n");
        sb.AppendLine("1. Souřadnice: RELATIVNÍ (0.0–1.0). Objekt na 30% šířky a 10% výšky → x: 0.3, y: 0.1.");
        foreach (string rule in ctx.AdditionalRules)
            sb.AppendLine(rule);
        sb.AppendLine($"{ctx.AdditionalRules.Count + 2}. Texty mimo hlavní oblast: role (title/header/validity/company/route/direction/footnote/unknown), mapování na DB pole z DataSchema. Pokud si nejsi jistý → dbField: null.");
        sb.AppendLine($"{ctx.AdditionalRules.Count + 3}. Obrázky: pro každé logo, QR kód nebo grafický prvek MIMO hlavní oblast vrať imageMappings s pozicí a rolí (\"logo\", \"qrCode\", \"decoration\"). Pokud odpovídá DB poli → nastav dbField. Roli \"qrCode\" hlas JEN pokud QR kód skutečně vidíš na obrázku nebo je v seznamu detekovaných grafických oblastí - NIKDY ho nevymýšlej. Každý fyzický objekt hlas právě jednou.");
        sb.AppendLine($"{ctx.AdditionalRules.Count + 4}. Použij POUZE pole z dodaného DataSchema. Nevymýšlej vlastní.");
        return sb.ToString();
    }

    private static string BuildPropertiesSection(AgendaPromptContext ctx)
    {
        if (ctx.ElementProperties.Count == 0) return "";
        var sb = new StringBuilder("VIZUÁLNÍ VLASTNOSTI ELEMENTŮ (\"properties\" - nastav podle toho co vidíš):\n");
        foreach (var (elementType, properties) in ctx.ElementProperties)
        {
            sb.AppendLine($"- {elementType}:");
            foreach (string prop in properties)
                sb.AppendLine($"  - {prop}");
        }
        return sb.ToString();
    }

    private static string BuildTemplateParametersSection(AgendaPromptContext ctx)
    {
        var sb = new StringBuilder("PARAMETRY ŠABLONY (\"templateParameters\" - nastav hodnoty podle toho co vidíš):\n");
        foreach (var (key, desc) in ctx.TemplateParameters)
            sb.AppendLine($"- {key}: {desc}");
        return sb.ToString();
    }

    private static string BuildOutputSection(AgendaPromptContext ctx)
    {
        return $"VÝSTUP: Vrať POUZE validní JSON (bez markdown, bez komentářů):\n{ctx.JsonExample}";
    }

    // ── Agenda-specific contexts ────────────────────────────────────────

    private record AgendaPromptContext(
        string RoleIntro,
        string StructureDescription,
        List<string> AdditionalRules,
        List<(string ElementType, List<string> Properties)> ElementProperties,
        List<(string Key, string Description)> TemplateParameters,
        string JsonExample);

    private static AgendaPromptContext ZastavkovyJrMhdPromptContext() => new(
        RoleIntro: "Jsi expert na rozpoznávání zastávkových jízdních řádů MHD z obrázků.\n\nÚKOL: Analyzuj screenshot PDF a vrať sémantickou strukturu - jaké elementy kde jsou.",
        StructureDescription: @"TYPICKÁ STRUKTURA MHD ZASTÁVKOVÉHO JŘ:
1. **StopList** (vlevo) - tabulka zastávek: sloupce jízdní doba, tarifní číslo, název zastávky, zóna
2. **DepartureGrid** (vpravo od StopList) - minutová mřížka: hodiny vlevo, minuty vpravo, skupiny dnů
3. **NotesLegend** - legenda poznámek (symboly + vysvětlení). Může být VPRAVO vedle StopList NEBO POD mřížkou - podívej se kde skutečně je.
4. **Texty** mimo JŘ oblast - číslo linky, název trasy, dopravce, platnost, směr
5. **Obrázky** mimo JŘ oblast - logo dopravce, QR kód, grafické prvky",
        AdditionalRules:
        [
            "2. DepartureGrid: identifikuj skupiny dnů. Rozepiš zkratky (\"Prac.\" → \"Pracovní den\").",
            "3. StopList: identifikuj sloupce (\"TravelTime\", \"TariffNumber\", \"StopName\", \"Zone\", \"Platform\" = nástupiště/stanoviště).",
            "4. POVINNÉ: vrať VŠECHNY hlavní oblasti, které na obrázku jsou - StopList (tabulka zastávek) ani DepartureGrid (minutová mřížka) NESMÍ v odpovědi chybět, pokud jsou vidět.",
            "5. Oblast prvku MUSÍ zahrnovat i jeho ZÁHLAVÍ a VŠECHNY jeho řádky - u StopListu řádek "
                + "s popisky sloupců (\"min.\", \"Zastávky\") a první i poslední zastávku, u DepartureGridu "
                + "řádek se skupinami dnů a popisky \"hod\". NIKDY nevracej oblast, která začíná až u dat: "
                + "co zůstane venku, skončí jako samostatné textové prvky, a záhlaví jedné tabulky "
                + "rozpadlé na dvacet popisků je vždycky špatně."
        ],
        ElementProperties:
        [
            // Šipku směru řídí parametr sestavy TISK_SIPKY, ne vlastnost prvku - ptát se na ni
            // v tomhle bloku nemělo smysl, odpověď neměl kdo přečíst.
            ("StopList", [
                "showRowSeparators (bool): true pokud jsou viditelné vodorovné čáry mezi řádky",
                "headerBgColor (hex): barva pozadí záhlaví sloupců, null pokud není"
            ]),
            ("DepartureGrid", [
                "hourColumnPerGroup (bool): true pokud je sloupec hodin u KAŽDÉ skupiny dnů, false pokud jen vlevo",
                "minuteBold (bool): true pokud jsou minuty tučné",
                "headerBgColor (hex): barva pozadí záhlaví skupin dnů, null pokud není",
                "alternateRowColor (hex): barva střídavých řádků, null pokud není",
                "timeAxisRangeFrom (int): první zobrazená hodina (např. 4)",
                "timeAxisRangeTo (int): poslední zobrazená hodina (např. 23)"
            ]),
            ("NotesLegend", [
                "headerText (string): nadpis legendy (\"Poznámky\" nebo jiný)"
            ])
        ],
        TemplateParameters:
        [
            ("TISK_SIPKY", "(bool): \"true\" pokud vidíš šipku směru u zastávek"),
            ("HODINY_OD", "(number): první hodina v mřížce (např. \"4\")"),
            ("HODINY_DO", "(number): poslední hodina v mřížce (např. \"23\")")
        ],
        JsonExample: @"{
  ""scheduleType"": ""ZastavkovyJrMhd"",
  ""confidence"": 0.95,
  ""elements"": [
    {
      ""type"": ""StopList"",
      ""left"": 0.02, ""top"": 0.15, ""right"": 0.25, ""bottom"": 0.85,
      ""columns"": [""TravelTime"", ""StopName""],
      ""hasHeader"": true,
      ""properties"": { ""showRowSeparators"": ""true"" }
    },
    {
      ""type"": ""DepartureGrid"",
      ""left"": 0.25, ""top"": 0.13, ""right"": 0.85, ""bottom"": 0.85,
      ""dayGroups"": [
        { ""header"": ""Pracovní den"", ""relativeLeft"": 0.26 },
        { ""header"": ""Sobota, neděle a svátky"", ""relativeLeft"": 0.6 }
      ],
      ""properties"": { ""hourColumnPerGroup"": ""false"", ""minuteBold"": ""true"" }
    },
    { ""type"": ""NotesLegend"", ""left"": 0.85, ""top"": 0.15, ""right"": 0.98, ""bottom"": 0.45, ""properties"": {} }
  ],
  ""textMappings"": [
    { ""text"": ""Linka č. 3"", ""role"": ""title"", ""dbField"": ""CISLO_LINKY"", ""placeholderText"": ""Linka č. {CISLO_LINKY}"", ""x"": 0.02, ""y"": 0.01, ""w"": 0.2, ""h"": 0.06, ""approxFontSize"": 14, ""confidence"": 0.95 }
  ],
  ""imageMappings"": [
    { ""role"": ""qrCode"", ""description"": ""QR kód"", ""dbField"": ""QR_KOD"", ""x"": 0.9, ""y"": 0.01, ""w"": 0.08, ""h"": 0.08 }
  ],
  ""templateParameters"": { ""TISK_SIPKY"": ""true"", ""HODINY_OD"": ""4"", ""HODINY_DO"": ""23"" },
  ""notes"": ""volitelné poznámky""
}");

    private static AgendaPromptContext GenericPromptContext() => new(
        RoleIntro: "Jsi expert na rozpoznávání tiskových sestav z obrázků.\n\nÚKOL: Analyzuj screenshot PDF stránky a pokus se identifikovat strukturu.",
        StructureDescription: "Pokud rozpoznáš JŘ strukturu, vrať elementy (StopList, DepartureGrid, NotesLegend).\nPokud ne, vrať prázdný elements array a namapuj texty na DB pole pokud je DataSchema k dispozici.",
        AdditionalRules: [],
        ElementProperties: [],
        TemplateParameters: [],
        JsonExample: @"{
  ""scheduleType"": ""Unknown"",
  ""confidence"": 0.0,
  ""elements"": [],
  ""textMappings"": [],
  ""imageMappings"": [],
  ""notes"": ""popis co bylo rozpoznáno""
}");

    // ── User prompt building ─────────────────────────────────────────────

    private static string BuildUserPrompt(ExtractedPage page, DataSchema? schema)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"ROZMĚRY STRÁNKY: {page.WidthMm:0.#} × {page.HeightMm:0.#} mm");
        sb.AppendLine();
        sb.AppendLine("Všechny souřadnice v odpovědi vrať jako RELATIVNÍ poměr 0.0–1.0 (ne pixely, ne mm).");
        sb.AppendLine();

        // Detekce grafických oblastí z PdfPig geometrie (logo, QR kód)
        var graphicAreas = DetectGraphicAreas(page);
        if (graphicAreas.Count > 0)
        {
            sb.AppendLine("DETEKOVANÉ GRAFICKÉ OBLASTI (vektorová grafika v PDF, nemusí být viditelná na screenshotu):");
            foreach (var area in graphicAreas)
                sb.AppendLine($"  - {area.Description}: relativní pozice [{area.RelLeft:0.##},{area.RelTop:0.##} → {area.RelRight:0.##},{area.RelBottom:0.##}]");
            sb.AppendLine("DŮLEŽITÉ: Tyto oblasti obsahují vektorovou grafiku (logo, QR kód), která nemusí být viditelná na screenshotu.");
            sb.AppendLine("Přesto je zahrň do imageMappings s odpovídající rolí (logo/qrCode).");
            sb.AppendLine();
        }

        AiPromptHelpers.AppendDataSchemaSection(sb, schema);

        sb.AppendLine();
        sb.AppendLine("Analyzuj přiložený screenshot PDF a vrať JSON se strukturou.");

        return sb.ToString();
    }

    // ── Detekce grafických oblastí z PdfPig geometrie ────────────────────

    private record GraphicArea(string Description, float RelLeft, float RelTop, float RelRight, float RelBottom);

    /// <summary>
    /// Detekuje oblasti s hustou koncentrací obdélníků - potenciální QR kódy nebo loga.
    /// PdfPig extrahuje vektorovou grafiku jako obdélníky, ale rasterizer je nemusí vykreslit
    /// jako rozpoznatelné obrázky. Tato metoda identifikuje takové oblasti a sdělí je AI.
    /// </summary>
    private static List<GraphicArea> DetectGraphicAreas(ExtractedPage page)
    {
        var areas = new List<GraphicArea>();
        float pageW = page.WidthMm;
        float pageH = page.HeightMm;

        if (page.Rectangles.Count < 5) return areas;

        // 1. QR kód: mnoho malých čtverečků stejné velikosti seskupených na malé ploše
        var smallSquares = page.Rectangles
            .Where(r => r.Width > 0.1f && r.Width < 3f && r.Height > 0.1f && r.Height < 3f
                        && Math.Abs(r.Width - r.Height) < 0.3f) // přibližně čtvercové
            .ToList();

        if (smallSquares.Count >= 20)
        {
            // Seskupit blízké čtverečky (DBSCAN-like: čtverce vzdálené < 5mm)
            var clusters = ClusterRectangles(smallSquares, 5f);
            foreach (var cluster in clusters.Where(c => c.Count >= 15))
            {
                float minX = cluster.Min(r => r.X);
                float minY = cluster.Min(r => r.Y);
                float maxX = cluster.Max(r => r.X + r.Width);
                float maxY = cluster.Max(r => r.Y + r.Height);

                float w = maxX - minX;
                float h = maxY - minY;

                // QR kód je přibližně čtvercový
                if (w > 3f && h > 3f && Math.Abs(w - h) < w * 0.3f)
                {
                    areas.Add(new GraphicArea(
                        $"Potenciální QR kód ({cluster.Count} čtverečků)",
                        minX / pageW, minY / pageH, maxX / pageW, maxY / pageH));
                }
            }
        }

        // 2. Logo/grafika: oblast s mnoha obdélníky bez textu
        //    Rozdělit stránku na 4×4 grid a najít buňky s vysokou hustotou obdélníků a nízkou hustotou textu
        const int gridSize = 4;
        float cellW = pageW / gridSize;
        float cellH = pageH / gridSize;

        for (int gx = 0; gx < gridSize; gx++)
        for (int gy = 0; gy < gridSize; gy++)
        {
            float cx = gx * cellW;
            float cy = gy * cellH;

            int rectCount = page.Rectangles.Count(r =>
                r.X + r.Width > cx && r.X < cx + cellW
                && r.Y + r.Height > cy && r.Y < cy + cellH);

            int wordCount = page.Words.Count(w =>
                w.X + w.Width > cx && w.X < cx + cellW
                && w.Y + w.Height > cy && w.Y < cy + cellH);

            // Vysoká hustota obdélníků (>= 10) a nízká hustota textu (<= 2 slov)
            if (rectCount >= 10 && wordCount <= 2)
            {
                // Zpřesnit boundary na skutečné obdélníky
                var cellRects = page.Rectangles.Where(r =>
                    r.X + r.Width > cx && r.X < cx + cellW
                    && r.Y + r.Height > cy && r.Y < cy + cellH).ToList();

                float minX = cellRects.Min(r => r.X);
                float minY = cellRects.Min(r => r.Y);
                float maxX = cellRects.Max(r => r.X + r.Width);
                float maxY = cellRects.Max(r => r.Y + r.Height);

                // Vyloučit pokud se překrývá s již detekovaným QR kódem
                bool overlaps = areas.Any(a =>
                    minX / pageW < a.RelRight && maxX / pageW > a.RelLeft
                    && minY / pageH < a.RelBottom && maxY / pageH > a.RelTop);

                if (!overlaps)
                {
                    areas.Add(new GraphicArea(
                        $"Potenciální logo/grafika ({rectCount} obdélníků, {wordCount} slov)",
                        minX / pageW, minY / pageH, maxX / pageW, maxY / pageH));
                }
            }
        }

        return areas;
    }

    /// <summary>Jednoduchý clustering obdélníků dle vzdálenosti středů.</summary>
    private static List<List<ExtractedRectangle>> ClusterRectangles(List<ExtractedRectangle> rects, float maxDist)
    {
        var clusters = new List<List<ExtractedRectangle>>();
        var assigned = new bool[rects.Count];

        for (int i = 0; i < rects.Count; i++)
        {
            if (assigned[i]) continue;

            var cluster = new List<ExtractedRectangle> { rects[i] };
            assigned[i] = true;
            var queue = new Queue<int>();
            queue.Enqueue(i);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                float cx = rects[current].X + rects[current].Width / 2;
                float cy = rects[current].Y + rects[current].Height / 2;

                for (int j = 0; j < rects.Count; j++)
                {
                    if (assigned[j]) continue;
                    float jx = rects[j].X + rects[j].Width / 2;
                    float jy = rects[j].Y + rects[j].Height / 2;

                    if (Math.Abs(cx - jx) < maxDist && Math.Abs(cy - jy) < maxDist)
                    {
                        cluster.Add(rects[j]);
                        assigned[j] = true;
                        queue.Enqueue(j);
                    }
                }
            }

            clusters.Add(cluster);
        }

        return clusters;
    }

    // ── Response parsing ─────────────────────────────────────────────────

    private static AiVisionResult? ParseResponse(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new AiVisionResult
        {
            ScheduleType = root.GetStringOrDefault("scheduleType", "Unknown"),
            Confidence = root.TryGetProperty("confidence", out var c) ? c.GetSingle() : 0f,
            Elements = ParseElements(root),
            TextMappings = ParseTextMappings(root),
            ImageMappings = ParseImageMappings(root),
            TemplateParameters = ParseStringDictionary(root, "templateParameters"),
            ElementSettings = ParseElementSettings(root),
            Notes = root.GetStringOrNull("notes")
        };
    }

    /// <summary>„elementSettings": { "StopList": { "Skupina · Popisek": "hodnota" } }.</summary>
    private static Dictionary<string, Dictionary<string, string>> ParseElementSettings(JsonElement root)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("elementSettings", out var obj) || obj.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var elementType in obj.EnumerateObject())
        {
            if (elementType.Value.ValueKind != JsonValueKind.Object) continue;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in elementType.Value.EnumerateObject())
                if (prop.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number
                    or JsonValueKind.True or JsonValueKind.False)
                    values[prop.Name] = prop.Value.ToString();
            if (values.Count > 0) result[elementType.Name] = values;
        }
        return result;
    }

    private static List<AiVisionElement> ParseElements(JsonElement root)
    {
        var elements = new List<AiVisionElement>();
        if (!root.TryGetProperty("elements", out var arr)) return elements;

        foreach (var item in arr.EnumerateArray())
        {
            // Souřadnice mají být relativní 0.0–1.0, ale model občas mírně přeteče (bottom 1.015)
            // → bez clampu vznikne element vyšší než stránka a import rovnou svítí přetečením.
            var el = new AiVisionElement
            {
                Type = item.GetStringOrDefault("type"),
                Left = ClampRel(GetFloat(item, "left")),
                Top = ClampRel(GetFloat(item, "top")),
                Right = ClampRel(GetFloat(item, "right")),
                Bottom = ClampRel(GetFloat(item, "bottom")),
                HasHeader = item.TryGetProperty("hasHeader", out var hh) && hh.GetBoolean(),
            };

            if (item.TryGetProperty("columns", out var cols))
            {
                foreach (var col in cols.EnumerateArray())
                    if (col.GetString() is { } s) el.Columns.Add(s);
            }

            if (item.TryGetProperty("dayGroups", out var dgs))
            {
                foreach (var dg in dgs.EnumerateArray())
                {
                    el.DayGroups.Add(new AiVisionDayGroup
                    {
                        Header = dg.GetStringOrDefault("header"),
                        RelativeLeft = GetFloat(dg, "relativeLeft")
                    });
                }
            }

            if (item.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in props.EnumerateObject())
                    el.Properties[prop.Name] = prop.Value.ToString();
            }

            elements.Add(el);
        }

        return elements;
    }

    private static List<AiVisionTextMapping> ParseTextMappings(JsonElement root)
    {
        var mappings = new List<AiVisionTextMapping>();
        if (!root.TryGetProperty("textMappings", out var arr)) return mappings;

        foreach (var item in arr.EnumerateArray())
        {
            mappings.Add(new AiVisionTextMapping
            {
                Text = item.GetStringOrDefault("text"),
                Role = item.GetStringOrDefault("role", "unknown"),
                DbField = item.GetStringOrNull("dbField"),
                PlaceholderText = item.GetStringOrNull("placeholderText"),
                X = GetFloat(item, "x"),
                Y = GetFloat(item, "y"),
                W = GetFloat(item, "w"),
                H = GetFloat(item, "h"),
                ApproxFontSize = GetFloat(item, "approxFontSize"),
                Confidence = item.TryGetProperty("confidence", out var cf) ? cf.GetSingle() : 0f
            });
        }

        return mappings;
    }

    private static List<AiVisionImageMapping> ParseImageMappings(JsonElement root)
    {
        var mappings = new List<AiVisionImageMapping>();
        if (!root.TryGetProperty("imageMappings", out var arr)) return mappings;

        foreach (var item in arr.EnumerateArray())
        {
            mappings.Add(new AiVisionImageMapping
            {
                Role = item.GetStringOrDefault("role", "decoration"),
                Description = item.GetStringOrDefault("description"),
                X = GetFloat(item, "x"),
                Y = GetFloat(item, "y"),
                W = GetFloat(item, "w"),
                H = GetFloat(item, "h"),
                DbField = item.GetStringOrNull("dbField"),
            });
        }

        return mappings;
    }

    private static Dictionary<string, string> ParseStringDictionary(JsonElement root, string propName)
    {
        var dict = new Dictionary<string, string>();
        if (!root.TryGetProperty(propName, out var obj) || obj.ValueKind != JsonValueKind.Object)
            return dict;
        foreach (var prop in obj.EnumerateObject())
            dict[prop.Name] = prop.Value.ToString();
        return dict;
    }

    private static float GetFloat(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.TryGetSingle(out float f) ? f : 0f;

    /// <summary>Clamp relativní souřadnice do 0.0–1.0 (model občas vrátí mírný přesah).</summary>
    private static float ClampRel(float v) => Math.Clamp(v, 0f, 1f);
}

// ── Výsledkové třídy (relativní souřadnice 0.0–1.0) ──────────────────

/// <summary>Výsledek AI vision analýzy. Všechny souřadnice relativní (0.0–1.0).</summary>
public class AiVisionResult
{
    public string ScheduleType { get; init; } = "Unknown";
    public float Confidence { get; init; }
    public List<AiVisionElement> Elements { get; init; } = [];
    public List<AiVisionTextMapping> TextMappings { get; init; } = [];
    public List<AiVisionImageMapping> ImageMappings { get; init; } = [];
    /// <summary>Detekované vizuální vlastnosti → hodnoty parametrů šablony.</summary>
    public Dictionary<string, string> TemplateParameters { get; init; } = [];

    /// <summary>Vzhled prvku poznaný z předlohy: typ prvku → („Skupina · Popisek" → hodnota).</summary>
    public Dictionary<string, Dictionary<string, string>> ElementSettings { get; init; } = [];
    public string? Notes { get; init; }
}

/// <summary>Element detekovaný AI. Souřadnice relativní (0.0–1.0).</summary>
public class AiVisionElement
{
    public string Type { get; init; } = "";
    public float Left { get; init; }
    public float Top { get; init; }
    public float Right { get; init; }
    public float Bottom { get; init; }
    public List<string> Columns { get; init; } = [];
    public bool HasHeader { get; init; }
    public List<AiVisionDayGroup> DayGroups { get; init; } = [];
    /// <summary>Vizuální vlastnosti elementu detekované AI (klíč → hodnota).</summary>
    public Dictionary<string, string> Properties { get; init; } = [];
}

/// <summary>Skupina dnů detekovaná AI.</summary>
public class AiVisionDayGroup
{
    public string Header { get; init; } = "";
    public float RelativeLeft { get; init; }
}

/// <summary>Text mimo JŘ oblast. Souřadnice relativní (0.0–1.0).</summary>
public class AiVisionTextMapping
{
    public string Text { get; init; } = "";
    public string Role { get; init; } = "unknown";
    public string? DbField { get; init; }
    public string? PlaceholderText { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float W { get; init; }
    public float H { get; init; }
    public float ApproxFontSize { get; init; }
    public float Confidence { get; init; }
}

/// <summary>Obrázek (logo, QR kód). Souřadnice relativní (0.0–1.0).</summary>
public class AiVisionImageMapping
{
    public string Role { get; init; } = "decoration";
    public string Description { get; init; } = "";
    public float X { get; init; }
    public float Y { get; init; }
    public float W { get; init; }
    public float H { get; init; }
    public string? DbField { get; init; }
}
