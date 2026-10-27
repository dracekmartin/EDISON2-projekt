using System.IO;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
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
        ScheduleImportType = ScheduleImportTypes.ForAgenda(SelectedAgendaCode);

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
    public string ImportModeInfo => ScheduleImportType switch
    {
        ScheduleImportType.ZastavkovyJrMhd =>
            $"Zastávkový jízdní řád - AI rozpozná strukturu (zastávky, odjezdy, poznámky) a namapuje pole agendy {SelectedAgendaName}.",
        ScheduleImportType.LinkovyJr =>
            $"Linkový jízdní řád - AI rozpozná tabulku spojů, poznámky a texty v hlavičce a namapuje pole agendy {SelectedAgendaName}.",
        _ => "Obecný layout - texty a grafika se převezmou 1:1 jako prvky šablony.",
    };

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
                // JŘ se rozpoznává z jedné stránky: u zastávkového je to jedna vývěska, u linkového
                // rozvržení stránky, které sestava zopakuje pro každý směr. U vícestránkového PDF
                // volí uživatel kterou; AI dostane jen tu - bez zahlcení.
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
                        ScheduleImportType = scheduleType
                    };
                    return converter.Convert(pdfPath, pagesArr);
                });
            }

            // Šablona zdědí agendu, ve které import běžel - i u obecného importu. Bez toho z něj
            // vypadla šablona bez agendy: prázdný seznam polí a nutnost ji ručně přiřadit.
            if (!string.IsNullOrEmpty(SelectedAgendaCode))
                template.AgendaCode = SelectedAgendaCode;

            NormalizeImportedPage(template, lineSchedule: scheduleType == ScheduleImportType.LinkovyJr);
            ApplyDefaultImportBorders(template);
            ImportedTemplate = template;

            int elementCount = template.Sections.Sum(s => s.Elements.Count);
            if (elementCount == 0)
            {
                StatusText = "PDF neobsahuje textovou vrstvu ani grafické prvky.";
                PopupDisplayer.Info(
                    "PDF neobsahuje rozpoznatelný textový obsah.\n" +
                    "Možná se jedná o skenovaný dokument bez OCR vrstvy.\n\n" +
                    "Šablona bude vytvořena se správnými rozměry stránky, ale bez prvků.");
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
                StatusText += $" | Import: {template.Sections.Count} sekcí, {elementCount} prvků";
            }
            else
            {
                StatusText = $"Import dokončen: {template.Sections.Count} sekcí, {elementCount} prvků";
            }

            SaveProtocolFile(pdfPath, enableDebug);
            if (useVision)
                SaveLastImportDiagnostics(pdfPath, aiDebugLog, template);
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
    /// Uloží podrobný protokol vedle PDF - <b>jen se zaškrtnutým Debugem</b>, tedy pro vývoj.
    /// Je to nástroj na porovnání dvou importů, ne informace pro uživatele: import je rychlý start,
    /// ne hotová šablona, takže výpis „co všechno se nastavilo" neumí říct, co ještě chybí - jen
    /// vysype čtyřicet položek, ze kterých nejde nic vyvodit (rozhodnutí Jan 19. 8.).
    /// Nezdaří-li se zápis (jen pro čtení, síťová cesta), import to nesmí shodit.
    /// </summary>
    private void SaveProtocolFile(string pdfPath, bool enableDebug)
    {
        if (!enableDebug) return;
        if (ImportLog.Count == 0 && DetailedLog.Count == 0) return;
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
        }
        catch (Exception) { /* protokol je pomůcka, ne brána */ }
    }

    private string? _lastPageImage;

    /// <summary>Složka s výpisem posledního AI importu (přepisuje se).</summary>
    internal static string LastImportDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "m-line", "TemplateDesigner", "posledni-import");

    /// <summary>
    /// Výpis posledního AI importu - vždy, do složky aplikace, ne vedle PDF: uživatele neobtěžuje
    /// a vývojář se podívá, co model vrátil. Bez něj nešlo zpětně zjistit, proč import dopadl
    /// hůř: odpovědi modelu se mezi běhy liší a volba Debug je jen s lokálním klíčem (nález 2. 10.).
    /// Nezdaří-li se zápis, import to nesmí shodit.
    /// </summary>
    private void SaveLastImportDiagnostics(string pdfPath, string? aiDebugLog, Template template)
    {
        try
        {
            Directory.CreateDirectory(LastImportDir);
            File.WriteAllText(Path.Combine(LastImportDir, "ai-debug.txt"),
                $"PDF: {pdfPath}{Environment.NewLine}Čas: {DateTime.Now:d. M. yyyy H:mm:ss}{Environment.NewLine}{Environment.NewLine}"
                + aiDebugLog);
            File.WriteAllLines(Path.Combine(LastImportDir, "protokol.txt"), [.. ImportLog, "", .. DetailedLog]);
            File.WriteAllText(Path.Combine(LastImportDir, "sablona.json"),
                Serialization.TemplateSerializer.Serialize(template));
            if (_lastPageImage is { Length: > 0 } png)
                File.WriteAllBytes(Path.Combine(LastImportDir, "stranka.png"), Convert.FromBase64String(png));
        }
        catch (Exception) { /* výpis je pomůcka, ne brána */ }
    }

    /// <summary>Normalizuje stránku importované šablony, ať sekce po importu rozumně vyplní stránku
    /// a nevypadá „posunutě": (1) rozměr stránky přichytí na nejbližší standardní formát
    /// (A5/A4/A3 × orientace) v toleranci, jinak zaokrouhlí; (2) nastaví malé jednotné výchozí okraje
    /// (1 mm, clampnuté ať neořežou obsah); (3) Body sekci dopočítá na tiskovou plochu.
    /// Page-level - prvky se nehýbou vůči sobě (posun se sekcí je uniformní).
    /// Linkový JŘ vynechá kroky (2) i (3): okraje zůstanou podle předlohy, protože jejich zmenšení
    /// posune celý obsah doleva nahoru (u vzoru 940 o 9-12 mm, změřeno 2. 10.), a sekce pod sebou
    /// (tabulka, poznámky) by každá na celou plochu rozházelo po stránkách.</summary>
    internal static void NormalizeImportedPage(Template template, bool lineSchedule = false)
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
        if (lineSchedule)
        {
            ps.MarginLeft = MmMath.Round(ps.MarginLeft); ps.MarginRight = MmMath.Round(ps.MarginRight);
            ps.MarginTop = MmMath.Round(ps.MarginTop); ps.MarginBottom = MmMath.Round(ps.MarginBottom);
            return;
        }

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
    internal static void ApplyDefaultImportBorders(Template template)
    {
        foreach (var section in template.Sections)
            foreach (var el in section.Elements)
            {
                if (el.BorderWidth > 0f) continue;   // detekovaný / už nastavený rámeček nechat
                if (el is RectangleElement) continue; // obdélník má vlastní okraj - černý navíc by orámoval i barevnou plochu hlavičky
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
        _lastPageImage = pipeline.PageImageBase64;
        ImportLog.AddRange(pipeline.ImportLog);
        DetailedLog.AddRange(pipeline.DetailedLog);
        return template;
    }
}
