using System.IO;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Services;

namespace TemplateDesigner.PdfImport;

/// <summary>
/// Orchestruje AI Vision import: extrakce PDF → rasterizace → AI vision → engine → Template.
/// Extrahováno z PdfImportDialogViewModel — VM volá tuto třídu, neobsahuje business logiku.
/// </summary>
public class AiVisionImportPipeline
{
    private readonly string _apiKey;
    private readonly IDataSchemaProvider? _schemaProvider;
    private readonly string? _agendaCode;
    private readonly IReadOnlyList<TemplateParameter>? _knownParameters;

    public string? AiDebugLog { get; private set; }

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

        DataSchema? schema = null;
        try
        {
            schema = !string.IsNullOrEmpty(_agendaCode)
                ? _schemaProvider?.GetSchema(_agendaCode)
                : null;

            visionResult = await visionService.AnalyzeAsync(
                base64Png, firstPage, schema, scheduleType, _apiKey);
        }
        catch (Exception ex)
        {
            setStatus($"AI Vision selhala ({ex.Message}), přepínám na heuristiku...");
        }
        finally
        {
            AiDebugLog = visionService.DebugLog;
        }

        // 4. Sestavit šablonu
        setStatus("Vytvářím elementy...");

        Template template;
        if (visionResult is not null && visionResult.Confidence >= 0.5f && visionResult.Elements.Count > 0)
        {
            template = await Task.Run(() => BuildTemplateFromVision(
                visionResult, firstPage, pageWidthMm, pageHeightMm,
                pdfPath, schema, visionService.DebugLog));

            int elCount = template.Sections.Sum(s => s.Elements.Count);
            int mappedFields = visionResult.TextMappings.Count(m => m.DbField is not null && m.Confidence >= 0.7f);
            setStatus($"AI Vision: {visionResult.Elements.Count} JŘ elementů, {mappedFields} polí namapováno, celkem {elCount} elementů");
        }
        else
        {
            // Fallback na heuristiku
            setStatus("AI Vision nedostatečná, přepínám na heuristiku...");
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

        if (AiGuidedScheduleBuilder.LastDebugInfo is not null)
            AiDebugLog = (visionDebugLog ?? "") + "\n\n── AiGuidedScheduleBuilder ──\n" + AiGuidedScheduleBuilder.LastDebugInfo;

        var section = new Section
        {
            Id = Guid.NewGuid(),
            Name = "JŘ — AI Vision",
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

        var templateParams = BuildTemplateParameters(visionResult);

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
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

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
                // Agenda má definici — převzít typ, label, options
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
