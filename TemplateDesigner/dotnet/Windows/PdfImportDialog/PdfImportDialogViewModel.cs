using System.IO;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.PdfImport;
using TemplateDesigner.Services;
using UglyToad.PdfPig;

namespace TemplateDesigner;

public class PdfImportDialogViewModel : EDViewModel
{
    private readonly IDataSchemaProvider? _schemaProvider;
    private readonly string? _apiKey;

    public PdfImportDialogViewModel(IDataSchemaProvider? schemaProvider = null, string? apiKey = null)
    {
        _schemaProvider = schemaProvider;
        _apiKey = apiKey;

        // Načíst dostupné agendy pro dropdown
        if (schemaProvider is not null)
        {
            try { AvailableAgendas = schemaProvider.GetAvailableAgendas().ToList(); }
            catch { /* Caché nedostupná */ }
        }

        // Předvybrat první agendu
        if (AvailableAgendas.Count > 0)
            SelectedAgendaCode = AvailableAgendas[0].Code;

        // AI enrichment je dostupný pokud máme API klíč
        HasApiKey = !string.IsNullOrEmpty(apiKey);
    }

    private string _pdfFilePath = "";
    public string PdfFilePath
    {
        get => _pdfFilePath;
        set { Set(ref _pdfFilePath, value); OnPropertyChanged(nameof(CanImport)); }
    }

    private int _totalPages;
    public int TotalPages { get => _totalPages; set => Set(ref _totalPages, value); }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { Set(ref _isBusy, value); OnPropertyChanged(nameof(IsNotBusy)); OnPropertyChanged(nameof(CanImport)); }
    }

    public bool IsNotBusy => !IsBusy;
    public bool CanImport => !IsBusy && !string.IsNullOrEmpty(PdfFilePath);

    private bool _enableDebug;
    public bool EnableDebug { get => _enableDebug; set => Set(ref _enableDebug, value); }

    // ── Typ importu ───────────────────────────────────────────────────────
    private ScheduleImportType _scheduleImportType = ScheduleImportType.None;
    public ScheduleImportType ScheduleImportType
    {
        get => _scheduleImportType;
        set
        {
            Set(ref _scheduleImportType, value);
            OnPropertyChanged(nameof(IsImportGeneral));
            OnPropertyChanged(nameof(IsImportZastavkovyJr));
            OnPropertyChanged(nameof(IsGraphicsModeVisible));
            OnPropertyChanged(nameof(IsAiSectionVisible));
        }
    }

    public bool IsImportGeneral { get => ScheduleImportType == ScheduleImportType.None; set { if (value) ScheduleImportType = ScheduleImportType.None; } }
    public bool IsImportZastavkovyJr { get => ScheduleImportType == ScheduleImportType.ZastavkovyJrMhd; set { if (value) ScheduleImportType = ScheduleImportType.ZastavkovyJrMhd; } }

    /// <summary>Režim grafiky je relevantní jen pro obecný import — JŘ import grafiku ignoruje.</summary>
    public bool IsGraphicsModeVisible => ScheduleImportType == ScheduleImportType.None;

    // ── AI obohacení ──────────────────────────────────────────────────────

    private bool _useAiEnrichment;
    public bool UseAiEnrichment { get => _useAiEnrichment; set => Set(ref _useAiEnrichment, value); }

    private bool _useAiVision;
    public bool UseAiVision
    {
        get => _useAiVision;
        set { Set(ref _useAiVision, value); OnPropertyChanged(nameof(IsAiVisionAgendaVisible)); }
    }

    /// <summary>Máme API klíč z appsettings.json?</summary>
    public bool HasApiKey { get; }

    /// <summary>Sekce AI obohacení je viditelná jen při JŘ importu.</summary>
    public bool IsAiSectionVisible => ScheduleImportType != ScheduleImportType.None;

    /// <summary>Agenda dropdown pro AI Vision je viditelný když je AI Vision zapnutý.</summary>
    public bool IsAiVisionAgendaVisible => UseAiVision;

    /// <summary>Dostupné agendy pro dropdown.</summary>
    public List<AgendaInfo> AvailableAgendas { get; } = [];

    private string _selectedAgendaCode = "";
    public string SelectedAgendaCode { get => _selectedAgendaCode; set => Set(ref _selectedAgendaCode, value); }

    // ── Režim grafiky ───────────────────────────────────────────────────────
    private GraphicsMergeMode _mergeMode = GraphicsMergeMode.LinesOnly;
    public GraphicsMergeMode MergeMode
    {
        get => _mergeMode;
        set
        {
            Set(ref _mergeMode, value);
            OnPropertyChanged(nameof(IsMergeModeNone));
            OnPropertyChanged(nameof(IsMergeModeLinesOnly));
            OnPropertyChanged(nameof(IsMergeModeAll));
        }
    }

    public bool IsMergeModeNone { get => MergeMode == GraphicsMergeMode.None; set { if (value) MergeMode = GraphicsMergeMode.None; } }
    public bool IsMergeModeLinesOnly { get => MergeMode == GraphicsMergeMode.LinesOnly; set { if (value) MergeMode = GraphicsMergeMode.LinesOnly; } }
    public bool IsMergeModeAll { get => MergeMode == GraphicsMergeMode.All; set { if (value) MergeMode = GraphicsMergeMode.All; } }

    private bool _importAllPages = true;
    public bool ImportAllPages { get => _importAllPages; set { Set(ref _importAllPages, value); OnPropertyChanged(nameof(ImportPageRange)); } }

    public bool ImportPageRange
    {
        get => !_importAllPages;
        set { ImportAllPages = !value; }
    }

    private int _pageFrom = 1;
    public int PageFrom { get => _pageFrom; set => Set(ref _pageFrom, value); }

    private int _pageTo = 1;
    public int PageTo { get => _pageTo; set => Set(ref _pageTo, value); }

    public bool HasMultiplePages => TotalPages > 1;

    public string PageRangeInfo => $"(z {TotalPages})";

    private string _statusText = "";
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

    public Template? ImportedTemplate { get; private set; }

    // Callbacky — View je napojí v konstruktoru
    public Func<string?>? RequestOpenFile { get; set; }
    public Action<string, string>? RequestShowError { get; set; }
    public Action<string, string>? RequestShowWarning { get; set; }
    public Action<string, string>? RequestShowInfo { get; set; }

    private RelayCommand? _browsePdf;
    public RelayCommand BrowsePdfCommand => _browsePdf ??= new(BrowsePdf);

    protected override void OnOk()
    {
        _ = ImportPdfAsync();
    }

    private void BrowsePdf()
    {
        var path = RequestOpenFile?.Invoke();
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            using var doc = PdfDocument.Open(path);
            TotalPages = doc.NumberOfPages;
            PageFrom = 1;
            PageTo = TotalPages;
            PdfFilePath = path;
            StatusText = $"Vybrán soubor: {Path.GetFileName(path)} ({TotalPages} stran)";
            OnPropertyChanged(nameof(CanImport));
            OnPropertyChanged(nameof(HasMultiplePages));
            OnPropertyChanged(nameof(PageRangeInfo));
        }
        catch (Exception ex)
        {
            StatusText = $"Chyba: {ex.Message}";
            RequestShowError?.Invoke($"Nelze otevřít PDF soubor:\n{ex.Message}", "Chyba");
        }
    }

    private async Task ImportPdfAsync()
    {
        if (string.IsNullOrEmpty(PdfFilePath)) return;

        IsBusy = true;
        StatusText = "Extrahuji obsah PDF...";

        try
        {
            int[]? pages = null;
            if (!ImportAllPages)
            {
                int from = Math.Clamp(PageFrom, 1, TotalPages);
                int to = Math.Clamp(PageTo, from, TotalPages);
                pages = Enumerable.Range(from, to - from + 1).ToArray();
            }

            // Konverze na background threadu — UI zůstane responzivní
            string pdfPath = PdfFilePath;
            bool enableDebug = EnableDebug;
            var mergeMode = MergeMode;
            var scheduleType = ScheduleImportType;
            var pagesArr = pages;
            bool useVision = UseAiVision && !string.IsNullOrEmpty(_apiKey)
                             && scheduleType != ScheduleImportType.None;

            PdfToTemplateConverter? converter = null;
            Template template;
            string? aiDebugLog = null;

            if (useVision)
            {
                // ── AI Vision pipeline ──────────────────────────────────────
                template = await ImportWithAiVisionAsync(
                    pdfPath, pagesArr, scheduleType, enableDebug,
                    s => StatusText = s,
                    log => aiDebugLog = log);
            }
            else
            {
                // ── Heuristický pipeline (stávající) ────────────────────────
                template = await Task.Run(() =>
                {
                    converter = new PdfToTemplateConverter
                    {
                        EnableDebug = enableDebug,
                        MergeMode = mergeMode,
                        ScheduleImportType = scheduleType
                    };
                    return converter.Convert(pdfPath, pagesArr);
                });

                // AI enrichment — pokud zapnutý a máme API klíč a detekce proběhla
                if (UseAiEnrichment && !string.IsNullOrEmpty(_apiKey)
                    && scheduleType != ScheduleImportType.None
                    && converter is not null
                    && converter.LastDetectionResult is { } detection
                    && detection.Confidence >= 0.5f
                    && converter.LastExtractedPages.Count > 0)
                {
                    StatusText = "AI analyzuje strukturu...";
                    var enricher = new AiImportEnricher();
                    try
                    {
                        var schema = !string.IsNullOrEmpty(SelectedAgendaCode)
                            ? _schemaProvider?.GetSchema(SelectedAgendaCode)
                            : null;

                        var enrichment = await enricher.EnrichAsync(
                            detection, converter.LastExtractedPages[0], schema, scheduleType, _apiKey);

                        if (enrichment is not null)
                        {
                            ApplyEnrichment(template, enrichment);
                            int mappedCount = enrichment.TextMappings.Count(m => m.DbField is not null && m.Confidence >= 0.7f);
                            StatusText = $"AI: {mappedCount} polí namapováno";
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusText = $"AI nedostupná ({ex.Message}), pokračuji bez obohacení";
                    }
                    finally
                    {
                        aiDebugLog = enricher.DebugLog;
                    }
                }
            }

            // Nastavit agendu na šabloně pokud importujeme JŘ
            if (scheduleType != ScheduleImportType.None && !string.IsNullOrEmpty(SelectedAgendaCode))
                template.AgendaCode = SelectedAgendaCode;

            ImportedTemplate = template;

            int elementCount = template.Sections.Sum(s => s.Elements.Count);
            if (elementCount == 0)
            {
                StatusText = "PDF neobsahuje textovou vrstvu ani grafické prvky.";
                RequestShowWarning?.Invoke(
                    "PDF neobsahuje rozpoznatelný textový obsah.\n" +
                    "Možná se jedná o skenovaný dokument bez OCR vrstvy.\n\n" +
                    "Šablona bude vytvořena se správnými rozměry stránky, ale bez elementů.",
                    "Upozornění");
            }

            // Uložit debug logy vedle PDF
            var debugLog = converter?.DebugLog;
            var savedLogPaths = new List<string>();

            if (enableDebug && debugLog is not null)
            {
                var logPath = Path.ChangeExtension(pdfPath, ".import-debug.txt");
                await File.WriteAllTextAsync(logPath, debugLog);
                savedLogPaths.Add(logPath);
            }

            // AI debug log se ukládá VŽDY když AI enrichment proběhl (i bez debug checkboxu)
            if (aiDebugLog is not null)
            {
                var aiLogPath = Path.ChangeExtension(pdfPath, ".ai-debug.txt");
                await File.WriteAllTextAsync(aiLogPath, aiDebugLog);
                savedLogPaths.Add(aiLogPath);
            }

            if (savedLogPaths.Count > 0)
            {
                var fileNames = string.Join(", ", savedLogPaths.Select(Path.GetFileName));
                StatusText = $"Import dokončen: {elementCount} elementů. Debug: {fileNames}";
                RequestShowInfo?.Invoke($"Debug logy uloženy:\n{string.Join("\n", savedLogPaths)}", "Debug");
            }
            else if (StatusText.StartsWith("AI"))
            {
                // Zachovat AI status a doplnit celkový výsledek
                StatusText += $" | Import: {template.Sections.Count} sekcí, {elementCount} elementů";
            }
            else
            {
                StatusText = $"Import dokončen: {template.Sections.Count} sekcí, {elementCount} elementů";
            }

            CloseDialog(true);
        }
        catch (Exception ex)
        {
            StatusText = $"Chyba při importu: {ex.Message}";
            RequestShowError?.Invoke($"Chyba při importu PDF:\n{ex.Message}", "Chyba importu");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// AI Vision import pipeline: deleguje na AiVisionImportPipeline.
    /// </summary>
    private async Task<Template> ImportWithAiVisionAsync(
        string pdfPath, int[]? pages, ScheduleImportType scheduleType, bool enableDebug,
        Action<string> setStatus, Action<string?> setAiLog)
    {
        // Získat known parametry z Caché agendy (pokud dostupné)
        IReadOnlyList<TemplateParameter>? knownParams = null;
        if (_schemaProvider is Serialization.CacheAgendaProvider cacheProvider
            && !string.IsNullOrEmpty(SelectedAgendaCode))
        {
            try { knownParams = cacheProvider.GetParameters(SelectedAgendaCode); }
            catch { /* Caché nedostupná */ }
        }

        var pipeline = new AiVisionImportPipeline(
            _apiKey!, _schemaProvider, SelectedAgendaCode, knownParams);

        var template = await pipeline.ImportAsync(
            pdfPath, pages, scheduleType, enableDebug, setStatus);

        setAiLog(pipeline.AiDebugLog);
        return template;
    }

    /// <summary>
    /// Aplikuje AI enrichment na importovanou šablonu.
    /// Nahradí statické texty za placeholdery kde AI navrhla mapování s dostatečnou confidence.
    /// </summary>
    private static void ApplyEnrichment(Template template, EnrichedImportResult enrichment)
    {
        foreach (var section in template.Sections)
        {
            // Odebrat dekorativní texty (pomlčky, oddělovače) označené AI
            section.Elements.RemoveAll(el =>
            {
                if (el is not Models.Elements.TextElement txt) return false;
                var mapping = enrichment.TextMappings.FirstOrDefault(m =>
                    string.Equals(m.OriginalText, txt.Text, StringComparison.OrdinalIgnoreCase));
                return mapping is not null
                    && string.Equals(mapping.Role, "decoration", StringComparison.OrdinalIgnoreCase)
                    && mapping.DbField is null;
            });

            // Nahradit statické texty za placeholdery
            foreach (var element in section.Elements)
            {
                if (element is not Models.Elements.TextElement textEl) continue;

                var mapping = enrichment.TextMappings.FirstOrDefault(m =>
                    string.Equals(m.OriginalText, textEl.Text, StringComparison.OrdinalIgnoreCase));

                if (mapping?.PlaceholderText is not null && mapping.Confidence >= 0.7f)
                    textEl.Text = mapping.PlaceholderText;
            }
        }
    }
}
