using System.IO;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.PdfImport;
using UglyToad.PdfPig;

namespace TemplateDesigner;

public class PdfImportDialogViewModel : EDViewModel
{
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
            var pagesArr = pages;

            var (template, debugLog) = await Task.Run(() =>
            {
                var converter = new PdfToTemplateConverter
                {
                    EnableDebug = enableDebug,
                    MergeMode = mergeMode
                };
                var result = converter.Convert(pdfPath, pagesArr);
                return (result, converter.DebugLog);
            });

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

            // Uložit debug log vedle PDF
            if (enableDebug && debugLog is not null)
            {
                var logPath = Path.ChangeExtension(pdfPath, ".import-debug.txt");
                await File.WriteAllTextAsync(logPath, debugLog);
                StatusText = $"Import dokončen: {elementCount} elementů. Debug log: {Path.GetFileName(logPath)}";
                RequestShowInfo?.Invoke($"Debug log uložen do:\n{logPath}", "Debug");
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
}
