using System.Text;
using System.Text.Json;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.PdfImport;

namespace TemplateDesigner.Services;

/// <summary>
/// AI obohacení heuristické detekce JŘ z PDF.
/// Validuje detekci, mapuje nezařazené texty na DB pole, koriguje záhlaví skupin dnů.
/// Kontextový system prompt per typ JŘ - každý typ má specifické informace o elementech.
/// </summary>
public class AiImportEnricher
{
    private const float AutoApplyThreshold = 0.7f;

    /// <summary>Debug log z posledního volání EnrichAsync. Obsahuje prompty, response a výsledek parsingu.</summary>
    public string? DebugLog { get; private set; }

    /// <summary>
    /// Obohatí výsledek heuristické detekce pomocí AI.
    /// Vrací null pokud AI nevrátí použitelnou odpověď.
    /// Debug log je vždy k dispozici v property DebugLog.
    /// </summary>
    public async Task<EnrichedImportResult?> EnrichAsync(
        ScheduleDetectionResult detection,
        ExtractedPage page,
        DataSchema? schema,
        ScheduleImportType scheduleType,
        string apiKey)
    {
        var log = new StringBuilder();
        var systemPrompt = GetSystemPrompt(scheduleType);
        var userPrompt = BuildUserPrompt(detection, page, schema);

        log.AppendLine("╔══════════════════════════════════════════════════════════════╗");
        log.AppendLine("║              AI IMPORT ENRICHMENT - DEBUG LOG               ║");
        log.AppendLine("╚══════════════════════════════════════════════════════════════╝");
        log.AppendLine();
        log.AppendLine($"Model: claude-haiku-4-5");
        log.AppendLine($"ScheduleImportType: {scheduleType}");
        log.AppendLine($"Schema: {schema?.AgendaCode ?? "(none)"}");
        log.AppendLine($"Detection confidence: {detection.Confidence:0.##}");
        log.AppendLine($"UnmappedTexts: {detection.UnmappedTexts.Count}");
        log.AppendLine($"DayGroups: {detection.DayGroups.Count}");
        log.AppendLine();

        log.AppendLine("┌──────────────────────────────────────────────────────────────┐");
        log.AppendLine("│ SYSTEM PROMPT                                                │");
        log.AppendLine("└──────────────────────────────────────────────────────────────┘");
        log.AppendLine(systemPrompt);
        log.AppendLine();

        log.AppendLine("┌──────────────────────────────────────────────────────────────┐");
        log.AppendLine("│ USER PROMPT                                                  │");
        log.AppendLine("└──────────────────────────────────────────────────────────────┘");
        log.AppendLine(userPrompt);
        log.AppendLine();

        string? responseText;
        try
        {
            responseText = await AnthropicApiClient.CallApiAsync(
                apiKey, systemPrompt, userPrompt,
                model: "claude-haiku-4-5", maxTokens: 4096, temperature: 0.2);
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

        EnrichedImportResult? result;
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

        // Logovat výsledek parsingu
        log.AppendLine("┌──────────────────────────────────────────────────────────────┐");
        log.AppendLine("│ PARSED RESULT                                                │");
        log.AppendLine("└──────────────────────────────────────────────────────────────┘");

        if (result is not null)
        {
            log.AppendLine($"TextMappings: {result.TextMappings.Count}");
            foreach (var m in result.TextMappings)
            {
                string autoApply = m.Confidence >= AutoApplyThreshold && m.PlaceholderText is not null ? " ✅ AUTO-APPLY" : "";
                log.AppendLine($"  \"{Truncate(m.OriginalText, 50)}\"");
                log.AppendLine($"    → role={m.Role}, dbField={m.DbField ?? "null"}, confidence={m.Confidence:0.##}{autoApply}");
                if (m.PlaceholderText is not null)
                    log.AppendLine($"    → placeholder=\"{m.PlaceholderText}\"");
            }

            log.AppendLine();
            log.AppendLine($"DayGroupValidations: {result.DayGroupValidations.Count}");
            foreach (var v in result.DayGroupValidations)
            {
                string change = v.CorrectedHeader is not null ? $" → \"{v.CorrectedHeader}\"" : " (beze změny)";
                log.AppendLine($"  [{v.Index}] \"{v.OriginalHeader}\"{change} valid={v.IsValid}");
            }

            if (result.Validation is { } val)
            {
                log.AppendLine();
                log.AppendLine($"DetectionValidation:");
                log.AppendLine($"  StopListValid={val.StopListValid}, DepartureGridValid={val.DepartureGridValid}, NotesValid={val.NotesValid}");
                if (val.ScheduleTypeSuggestion is not null) log.AppendLine($"  ScheduleTypeSuggestion={val.ScheduleTypeSuggestion}");
                if (val.Notes is not null) log.AppendLine($"  Notes: {val.Notes}");
            }
        }
        else
        {
            log.AppendLine("→ VÝSLEDEK: null (parsing vrátil null)");
        }

        DebugLog = log.ToString();
        return result;
    }

    /// <summary>Minimální confidence pro automatické aplikování mapování.</summary>
    public static float GetAutoApplyThreshold() => AutoApplyThreshold;

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "...";

    // ── System prompts per typ JŘ ────────────────────────────────────────

    private static string GetSystemPrompt(ScheduleImportType type) => type switch
    {
        ScheduleImportType.ZastavkovyJrMhd => SystemPromptZastavkovyJrMhd,
        _ => SystemPromptGeneric
    };

    private const string SystemPromptZastavkovyJrMhd = @"Jsi expert na rozpoznávání zastávkových jízdních řádů MHD (městské hromadné dopravy).

KONTEXT: Heuristický detektor analyzoval PDF stránku a identifikoval geometrii zastávkového JŘ.
Detekované elementy:
- StopListElement - tabulka zastávek (vlevo): sloupce Jízdní doba, Tarifní číslo, Název zastávky, Zóna
- DepartureGridElement - minutová mřížka (vpravo): hodiny × skupiny dnů, buňky = minuty odjezdů s anotacemi
- NotesLegendElement - legenda poznámek (pod mřížkou): symboly a jejich vysvětlení

Tvůj úkol je doplnit SÉMANTIKU - validovat detekci a namapovat texty na databázová pole.

PRAVIDLA:
1. VALIDACE oblastí:
   - Ověř, že detekce odpovídá zastávkovému JŘ MHD (minutová mřížka, NE celé časy spojů).
   - Pokud oblast neodpovídá, nastav xxxValid=false v detectionValidation.

2. SKUPINY DNŮ:
   - Zkontroluj a oprav záhlaví skupin dnů.
   - Typické zkratky: ""Prac."" → ""Pracovní den"", ""So"" → ""Sobota"", ""Ne"" → ""Neděle"", ""Ne+sv"" → ""Neděle a svátky"".
   - Rozepiš zkratky na plné názvy. Pokud záhlaví je už plné, nechej beze změny (correctedHeader=null).

3. MAPOVÁNÍ TEXTŮ mimo JŘ oblasti:
   - Typické texty na zastávkovém JŘ MHD: název dopravce, číslo linky, název linky/trasy, platnost od/do, web, QR kód text, označení směru.
   - Pro každý text urči sémantickou roli: header, title, validity, company, route, decoration, footnote, unknown.
   - Pokud text odpovídá DB poli z dodaného DataSchema → nastav dbField a VŽDY nastav placeholderText:
     a) Celý text je hodnota pole: ""11"" → dbField: ""CISLO_LINKY"", placeholderText: ""{CISLO_LINKY}""
     b) Text má konstantní + variabilní část: ""Linka č. 3"" → placeholderText: ""Linka č. {CISLO_LINKY}""
     c) Text obsahuje víc DB polí: ""Dopravce: DPMCB, tel. 123"" → placeholderText: ""{POZNAMKA_POD_DOPRAVCE}""
   - PRAVIDLO: Pokud nastavíš dbField, MUSÍŠ nastavit i placeholderText. Nikdy nenechávej dbField neprázdné a placeholderText null.
   - Pokud text je čistě statický (dekorace, URL, copyright): dbField=null, placeholderText=null.
   - Pokud si NEJSI JISTÁ → dbField: null, placeholderText: null. Raději nechat prázdné než špatně mapovat.
   - Confidence: 0.9+ = jistá shoda s DB polem, 0.7–0.9 = pravděpodobná, pod 0.7 → nastav dbField i placeholderText na null.
   - Texty které jsou evidentně součástí JŘ mřížky (hodiny, minuty, jízdní doby) nebo patří do záhlaví skupin dnů PŘESKOČ - nastav role=""decoration"" nebo ""unknown"", dbField=null.

4. Použij POUZE pole z dodaného DataSchema. NIKDY nevymýšlej vlastní názvy polí.

VÝSTUP: Vrať POUZE validní JSON objekt (bez markdown, bez komentářů) s touto strukturou:
{
  ""textMappings"": [
    {
      ""originalText"": ""původní text"",
      ""role"": ""title"",
      ""dbField"": ""CISLO_LINKY"",
      ""placeholderText"": ""Linka č. {CISLO_LINKY}"",
      ""confidence"": 0.95
    }
  ],
  ""dayGroupValidations"": [
    {
      ""index"": 0,
      ""originalHeader"": ""Prac."",
      ""correctedHeader"": ""Pracovní den"",
      ""isValid"": true
    }
  ],
  ""detectionValidation"": {
    ""stopListValid"": true,
    ""departureGridValid"": true,
    ""notesValid"": true,
    ""scheduleTypeSuggestion"": ""ZastavkovyJrMhd"",
    ""notes"": ""volitelné poznámky""
  }
}";

    private const string SystemPromptGeneric = @"Jsi expert na rozpoznávání tiskových sestav jízdních řádů veřejné dopravy.

KONTEXT: Heuristický detektor analyzoval PDF stránku a identifikoval geometrii jízdního řádu.
Tvůj úkol je doplnit sémantiku - namapovat nezařazené texty na databázová pole.

PRAVIDLA:
1. Pro každý nezařazený text urči sémantickou roli: header, title, validity, company, route, decoration, footnote, unknown.
2. Pokud text odpovídá DB poli z dodaného DataSchema → nastav dbField a placeholderText.
3. Pokud si NEJSI JISTÁ → dbField: null. Raději nechat prázdné.
4. Použij POUZE pole z dodaného DataSchema. NIKDY nevymýšlej vlastní.

VÝSTUP: Vrať POUZE validní JSON objekt (bez markdown) se strukturou:
{
  ""textMappings"": [ { ""originalText"": ""..."", ""role"": ""..."", ""dbField"": null, ""placeholderText"": null, ""confidence"": 0.5 } ],
  ""dayGroupValidations"": [],
  ""detectionValidation"": { ""stopListValid"": true, ""departureGridValid"": true, ""notesValid"": true, ""scheduleTypeSuggestion"": null, ""notes"": null }
}";

    // ── User prompt building ─────────────────────────────────────────────

    private static string BuildUserPrompt(
        ScheduleDetectionResult detection,
        ExtractedPage page,
        DataSchema? schema)
    {
        var sb = new StringBuilder();

        // Detekované oblasti
        sb.AppendLine("DETEKOVANÉ OBLASTI:");
        sb.AppendLine($"- StopList: {FormatRegion(detection.StopListRegion)}");
        sb.AppendLine($"- DepartureGrid: {FormatRegion(detection.DepartureGridRegion)}");
        sb.AppendLine($"- Notes: {FormatRegion(detection.NotesRegion)}");
        sb.AppendLine($"- Confidence heuristik: {detection.Confidence:0.##}");
        sb.AppendLine($"- Stránka: {page.WidthMm:0.#} × {page.HeightMm:0.#} mm");
        sb.AppendLine();

        // Skupiny dnů
        if (detection.DayGroups.Count > 0)
        {
            sb.AppendLine("SKUPINY DNŮ (záhlaví detekovaná z PDF):");
            for (int i = 0; i < detection.DayGroups.Count; i++)
            {
                var g = detection.DayGroups[i];
                sb.AppendLine($"  {i + 1}. \"{g.HeaderText}\" [x={g.X:0.#}, šířka={g.Width:0.#}mm]");
            }
            sb.AppendLine();
        }

        // Nezařazené texty
        if (detection.UnmappedTexts.Count > 0)
        {
            sb.AppendLine("NEZAŘAZENÉ TEXTY (mimo JŘ oblasti - potřebují mapování):");
            for (int i = 0; i < detection.UnmappedTexts.Count; i++)
            {
                var t = detection.UnmappedTexts[i];
                var fontDesc = $"{t.FontSizePt:0.#}pt";
                if (t.IsBold) fontDesc += " Bold";
                if (t.IsItalic) fontDesc += " Italic";
                sb.AppendLine($"  {i + 1}. [{t.X:0.#},{t.Y:0.#} {fontDesc}] \"{t.Text}\"");
            }
            sb.AppendLine();
        }

        AiPromptHelpers.AppendDataSchemaSection(sb, schema);

        return sb.ToString();
    }

    // ── Response parsing ─────────────────────────────────────────────────

    private static EnrichedImportResult? ParseResponse(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var result = new EnrichedImportResult
        {
            TextMappings = ParseTextMappings(root),
            DayGroupValidations = ParseDayGroupValidations(root),
            Validation = ParseDetectionValidation(root)
        };

        return result;
    }

    private static List<TextMapping> ParseTextMappings(JsonElement root)
    {
        var mappings = new List<TextMapping>();
        if (!root.TryGetProperty("textMappings", out var arr)) return mappings;

        foreach (var item in arr.EnumerateArray())
        {
            mappings.Add(new TextMapping
            {
                OriginalText = item.GetStringOrDefault("originalText"),
                Role = item.GetStringOrDefault("role", "unknown"),
                DbField = item.GetStringOrNull("dbField"),
                PlaceholderText = item.GetStringOrNull("placeholderText"),
                Confidence = item.TryGetProperty("confidence", out var c) ? c.GetSingle() : 0f
            });
        }

        return mappings;
    }

    private static List<DayGroupValidation> ParseDayGroupValidations(JsonElement root)
    {
        var validations = new List<DayGroupValidation>();
        if (!root.TryGetProperty("dayGroupValidations", out var arr)) return validations;

        foreach (var item in arr.EnumerateArray())
        {
            validations.Add(new DayGroupValidation
            {
                Index = item.TryGetProperty("index", out var idx) ? idx.GetInt32() : 0,
                OriginalHeader = item.GetStringOrDefault("originalHeader"),
                CorrectedHeader = item.GetStringOrNull("correctedHeader"),
                IsValid = !item.TryGetProperty("isValid", out var v) || v.GetBoolean()
            });
        }

        return validations;
    }

    private static DetectionValidation? ParseDetectionValidation(JsonElement root)
    {
        if (!root.TryGetProperty("detectionValidation", out var val)) return null;

        return new DetectionValidation
        {
            StopListValid = !val.TryGetProperty("stopListValid", out var sl) || sl.GetBoolean(),
            DepartureGridValid = !val.TryGetProperty("departureGridValid", out var dg) || dg.GetBoolean(),
            NotesValid = !val.TryGetProperty("notesValid", out var n) || n.GetBoolean(),
            ScheduleTypeSuggestion = val.GetStringOrNull("scheduleTypeSuggestion"),
            Notes = val.GetStringOrNull("notes")
        };
    }

    private static string FormatRegion(DetectedRegion? r) =>
        r is null ? "(nedetekováno)" : $"[{r.X:0.#},{r.Y:0.#} {r.Width:0.#}×{r.Height:0.#}mm]";
}

// ── JsonElement extension helpers ────────────────────────────────────────

internal static class JsonElementExtensions
{
    public static string GetStringOrDefault(this JsonElement el, string prop, string defaultValue = "")
    {
        if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString() ?? defaultValue;
        return defaultValue;
    }

    public static string? GetStringOrNull(this JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString();
        return null;
    }
}
