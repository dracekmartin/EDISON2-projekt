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

    /// <summary>Důvod nedostupnosti AI (z EdisonAiService) - null = AI dostupné.</summary>
    private readonly string? _aiUnavailableReason;

    public PdfImportDialogViewModel(
        IDataSchemaProvider? schemaProvider = null, string? apiKey = null,
        string? preferredAgendaCode = null, string? aiUnavailableReason = null)
    {
        _schemaProvider = schemaProvider;
        _apiKey = apiKey;
        _aiUnavailableReason = aiUnavailableReason;

        if (schemaProvider is not null)
        {
            try { AvailableAgendas = schemaProvider.GetAvailableAgendas().ToList(); }
            catch { /* Caché nedostupná */ }
        }

        // Režim importu se odvozuje z aktuálně otevřené agendy - uživatel nevolí typ, agendu
        // ani AI (JŘ import = vždy AI Vision; jiná agenda = obecný import textů a grafiky).
        SelectedAgendaCode = preferredAgendaCode is { Length: > 0 } pref
            && AvailableAgendas.Any(a => a.Code.Equals(pref, StringComparison.OrdinalIgnoreCase))
                ? pref : "";
        ScheduleImportType = SelectedAgendaCode.StartsWith("ZASTAVKOVY_JR", StringComparison.OrdinalIgnoreCase)
            ? ScheduleImportType.ZastavkovyJrMhd
            : ScheduleImportType.None;

        // AI klíč: lokální dev, nebo vyzvednutý z mlweb/EDISONAI
        HasApiKey = !string.IsNullOrEmpty(apiKey);
        UseAiVision = HasApiKey && IsScheduleImport;
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
    public bool CanImport => !IsBusy && !string.IsNullOrEmpty(PdfFilePath) && !ImportBlocked;

    /// <summary>Debug log jen pro vývojáře (lokální dev klíč v appsettings) - zákazník ho nevidí.</summary>
    public bool IsDevMode => !string.IsNullOrEmpty(Config.AppSettings.Current?.AiAssistant.AnthropicApiKey);

    private bool _enableDebug;
    public bool EnableDebug { get => _enableDebug; set => Set(ref _enableDebug, value); }

    // ── Režim importu - odvozen z aktuální agendy, uživatel nevolí ─────────

    public ScheduleImportType ScheduleImportType { get; }

    public bool IsScheduleImport => ScheduleImportType != ScheduleImportType.None;

    /// <summary>JŘ import bez AI nedává smysl - bez dostupného klíče je import zablokovaný.</summary>
    public bool ImportBlocked => IsScheduleImport && !HasApiKey;

    /// <summary>Text blokace s konkrétním důvodem z mlweb (limit, chybí konfigurace…).</summary>
    public string ImportBlockedText => _aiUnavailableReason
        ?? "AI import není dostupný - kontaktujte podporu M-line.";

    /// <summary>Popis režimu importu pro info řádek v dialogu.</summary>
    public string ImportModeInfo => IsScheduleImport
        ? $"Zastávkový jízdní řád - AI rozpozná strukturu (zastávky, odjezdy, poznámky) a namapuje pole agendy {SelectedAgendaName}."
        : "Obecný layout - texty a grafika se převezmou 1:1 jako elementy šablony.";

    private string SelectedAgendaName =>
        AvailableAgendas.FirstOrDefault(a => a.Code.Equals(SelectedAgendaCode, StringComparison.OrdinalIgnoreCase))?.Name
        ?? SelectedAgendaCode;

    /// <summary>AI Vision zapnuto vždy, když je to JŘ import a klíč je dostupný (bez UI volby).</summary>
    public bool UseAiVision { get; }

    /// <summary>Máme API klíč (lokální dev, nebo z mlweb/EDISONAI)?</summary>
    public bool HasApiKey { get; }

    /// <summary>Dostupné agendy (pro překlad kódu na název).</summary>
    public List<AgendaInfo> AvailableAgendas { get; } = [];

    public string SelectedAgendaCode { get; }

    /// <summary>
    /// Grafika u obecného importu: každá čára, obdélník i obrázek jako vlastní prvek (bez UI volby).
    /// Slévání čar do jednoho obrázku na pozadí (dosavadní výchozí stav) dávalo zamčený rastr, který
    /// nešel upravit a v tisku byl rozmazaný proti zbytku šablony - obrázek má v šabloně vzniknout
    /// jen z toho, co obrázek opravdu je (logo, QR). Čáry se vkládají seskupené; početnost čar má
    /// řešit rozpoznání tabulky z jejich mřížky, ne rastr.
    /// </summary>
    public GraphicsMergeMode MergeMode { get; } = GraphicsMergeMode.None;

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

    /// <summary>Protokol posledního importu - co se z předlohy nastavilo a co se nerozpoznalo.</summary>
    public List<string> ImportLog { get; } = [];

    /// <summary>Úplný soupis vzniklých prvků a jejich nastavení - ukládá se vedle PDF.</summary>
    public List<string> DetailedLog { get; } = [];

    // Callbacky - View je napojí v konstruktoru
    public Func<string?>? RequestOpenFile { get; set; }

    private RelayCommand? _browsePdf;
    public RelayCommand BrowsePdfCommand => _browsePdf ??= Command(BrowsePdf);

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
            StatusText = $"⚠ Chyba: {ex.Message}";
            PopupDisplayer.Error(ex);
        }
    }

    private async Task ImportPdfAsync()
    {
        if (string.IsNullOrEmpty(PdfFilePath)) return;

        IsBusy = true;
        StatusText = "Extrahuji obsah PDF…";

        try
        {
            int[]? pages = null;
            if (IsScheduleImport)
            {
                // Zastávkový JŘ = vždy právě jedna stránka (jedna vývěska). U vícestránkového
                // PDF (celá linka) volí uživatel kterou; AI dostane jen tu - bez zahlcení.
                pages = [Math.Clamp(PageFrom, 1, Math.Max(TotalPages, 1))];
            }
            else if (!ImportAllPages)
            {
                int from = Math.Clamp(PageFrom, 1, TotalPages);
                int to = Math.Clamp(PageTo, from, TotalPages);
                pages = Enumerable.Range(from, to - from + 1).ToArray();
            }

            // Konverze na background threadu - UI zůstane responzivní
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
                // ── Heuristický pipeline (obecný import bez AI) ─────────────
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
            }

            // Šablona zdědí agendu, ve které import běžel - i u obecného importu. Bez toho z něj
            // vypadla šablona bez agendy: prázdný seznam polí a nutnost ji ručně přiřadit.
            if (!string.IsNullOrEmpty(SelectedAgendaCode))
                template.AgendaCode = SelectedAgendaCode;

            NormalizeImportedPage(template);
            ApplyDefaultImportBorders(template);
            ImportedTemplate = template;

            int elementCount = template.Sections.Sum(s => s.Elements.Count);
            if (elementCount == 0)
            {
                StatusText = "PDF neobsahuje textovou vrstvu ani grafické prvky.";
                PopupDisplayer.Info(
                    "PDF neobsahuje rozpoznatelný textový obsah.\n" +
                    "Možná se jedná o skenovaný dokument bez OCR vrstvy.\n\n" +
                    "Šablona bude vytvořena se správnými rozměry stránky, ale bez elementů.");
            }

            // Debug logy vedle PDF - jen se zaškrtnutým Debug (vývojář); bez popupu,
            // ať import nekončí hláškou navíc. Vývojář ví, kde soubory hledat.
            if (enableDebug)
            {
                if (converter?.DebugLog is { } debugLog)
                    await File.WriteAllTextAsync(Path.ChangeExtension(pdfPath, ".import-debug.txt"), debugLog);
                if (aiDebugLog is not null)
                    await File.WriteAllTextAsync(Path.ChangeExtension(pdfPath, ".ai-debug.txt"), aiDebugLog);
            }

            if (StatusText.StartsWith("AI"))
            {
                // Zachovat AI status a doplnit celkový výsledek
                StatusText += $" | Import: {template.Sections.Count} sekcí, {elementCount} elementů";
            }
            else
            {
                StatusText = $"Import dokončen: {template.Sections.Count} sekcí, {elementCount} elementů";
            }

            string? protokol = SaveProtocolFile(pdfPath);
            ReportImportLog(protokol);
            CloseDialog(true);
        }
        catch (Exception ex)
        {
            StatusText = $"⚠ Chyba při importu: {ex.Message}";
            PopupDisplayer.Error(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Ukáže, co import z předlohy převzal a co nerozpoznal. Bez toho se hlášky ztratily se
    /// zavřením dialogu a uživatel neměl jak zjistit, co si má dodělat ručně.
    /// </summary>
    private void ReportImportLog(string? protocolPath)
    {
        if (ImportLog.Count == 0) return;
        string text = "Protokol importu:" + Environment.NewLine + Environment.NewLine
            + string.Join(Environment.NewLine, ImportLog);
        if (protocolPath is not null)
            text += Environment.NewLine + Environment.NewLine
                  + "Podrobný soupis všech prvků a jejich nastavení:" + Environment.NewLine + protocolPath;
        PopupDisplayer.Info(text);
    }

    /// <summary>
    /// Uloží podrobný protokol vedle PDF. Hláška se dá jen přečíst a zavřít; na kontrolu proti
    /// předloze a na porovnání dvou importů mezi sebou je potřeba soubor, který zůstane.
    /// Nezdaří-li se zápis (jen pro čtení, síťová cesta), import to nesmí shodit.
    /// </summary>
    private string? SaveProtocolFile(string pdfPath)
    {
        if (ImportLog.Count == 0 && DetailedLog.Count == 0) return null;
        try
        {
            string path = Path.ChangeExtension(pdfPath, ".import-protokol.txt");
            var text = new List<string>
            {
                $"Protokol importu z předlohy - {Path.GetFileName(pdfPath)}",
                new('=', 60),
                ""
            };
            text.AddRange(ImportLog);
            if (DetailedLog.Count > 0)
            {
                text.Add("");
                text.Add("Prvky a jejich nastavení (oproti čerstvě vloženému prvku)");
                text.Add(new string('-', 60));
                text.AddRange(DetailedLog);
            }
            File.WriteAllLines(path, text);
            return path;
        }
        catch (Exception) { return null; }
    }

    /// <summary>Normalizuje stránku importované šablony, ať sekce po importu rozumně vyplní stránku
    /// a nevypadá „posunutě": (1) rozměr stránky přichytí na nejbližší standardní formát
    /// (A5/A4/A3 × orientace) v toleranci, jinak zaokrouhlí; (2) nastaví malé jednotné výchozí okraje
    /// (1 mm, clampnuté ať neořežou obsah); (3) Body sekci dopočítá na tiskovou plochu.
    /// Page-level - prvky se nehýbou vůči sobě (posun se sekcí je uniformní).</summary>
    private static void NormalizeImportedPage(Template template)
    {
        var ps = template.PageSize;

        // (1) snap na standardní formát - portrait rozměry, zkoušíme obě orientace
        const float tol = 5f;
        (float W, float H)[] formats = [(148f, 210f), (210f, 297f), (297f, 420f)];   // A5, A4, A3
        bool snapped = false;
        foreach (var (fw, fh) in formats)
        {
            foreach (var (cw, ch) in new[] { (fw, fh), (fh, fw) })
                if (MathF.Abs(ps.Width - cw) <= tol && MathF.Abs(ps.Height - ch) <= tol)
                { ps.Width = cw; ps.Height = ch; snapped = true; break; }
            if (snapped) break;
        }
        if (!snapped) { ps.Width = MmMath.Round(ps.Width); ps.Height = MmMath.Round(ps.Height); }

        // (2) výchozí okraje - malé jednotné (1 mm), ale nikdy tak velké, aby ořízly obsah
        float maxRight = 0f, maxBottom = 0f;
        foreach (var section in template.Sections)
            foreach (var el in section.Elements)
            {
                maxRight  = MathF.Max(maxRight,  el.X + el.Width);
                maxBottom = MathF.Max(maxBottom, el.Y + (el.Height ?? el.EffectiveDesignerHeight));
            }
        const float defMargin = 1f;
        float mh = MathF.Min(defMargin, MathF.Max(0f, (ps.Width  - maxRight)  / 2f));
        float mv = MathF.Min(defMargin, MathF.Max(0f, (ps.Height - maxBottom) / 2f));
        ps.MarginLeft = ps.MarginRight = MmMath.Round(mh);
        ps.MarginTop  = ps.MarginBottom = MmMath.Round(mv);

        // (3) Body sekce vyplní tiskovou plochu
        foreach (var section in template.Sections)
            if (section.Role == SectionRole.Body)
                section.DesignerHeight = ps.PrintableHeight;
    }

    /// <summary>Dorovná importovaným elementům konzistentní výchozí rámeček (černý 0,1mm) tam, kde
    /// žádný nemají - shodně s novými elementy z palety (<c>CreateElementByType</c>). Import ho někdy
    /// nenastaví; rámeček detekovaný z PDF (šířka > 0) se nepřepisuje.</summary>
    private static void ApplyDefaultImportBorders(Template template)
    {
        foreach (var section in template.Sections)
            foreach (var el in section.Elements)
            {
                if (el.BorderWidth > 0f) continue;   // detekovaný / už nastavený rámeček nechat
                el.BorderColor = "#FF000000";
                el.BorderWidth = 0.1f;
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
        ImportLog.AddRange(pipeline.ImportLog);
        DetailedLog.AddRange(pipeline.DetailedLog);
        return template;
    }
}
