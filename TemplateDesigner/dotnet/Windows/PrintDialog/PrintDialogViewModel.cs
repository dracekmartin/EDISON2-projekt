using System.Collections.ObjectModel;
using System.IO;
using EDControls.EDViewModel;
using Microsoft.Win32;
using TemplateDesigner.Models;

namespace TemplateDesigner;

public class PrintDialogViewModel : EDViewModel
{
    private readonly Template _template;
    private readonly Dictionary<string, object?> _paramValues = new();
    private readonly Func<Dictionary<string, object?>, DataContext?> _buildMockData;
    private readonly Func<Dictionary<string, object?>, List<DataContext>?>? _buildRealData;

    private string _statusText = "";
    private bool _hasParameters;

    public ObservableCollection<ParameterControlVM> Parameters { get; } = [];

    public string TemplateName => string.IsNullOrWhiteSpace(_template.Name)
        ? "Bez názvu" : _template.Name;

    public bool HasParameters { get => _hasParameters; private set => Set(ref _hasParameters, value); }
    public bool CanExportRealData => _buildRealData is not null;
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

    public RelayCommand ExportPdfCommand { get; }
    public RelayCommand ExportRealDataPdfCommand { get; }

    /// <summary>Code-behind napojí na PreviewCanvas.RenderToPdf.</summary>
    public Action<string, DataContext?>? RequestRenderPdf { get; set; }

    /// <summary>Code-behind napojí na PreviewCanvas.RenderMultiDataToPdf.</summary>
    public Action<string, IReadOnlyList<DataContext>>? RequestRenderMultiPdf { get; set; }

    public PrintDialogViewModel(
        Template template,
        IReadOnlyList<TemplateParameter> parameters,
        Func<Dictionary<string, object?>, DataContext?> buildMockData,
        Func<Dictionary<string, object?>, List<DataContext>?>? buildRealData = null)
    {
        _template = template;
        _buildMockData = buildMockData;
        _buildRealData = buildRealData;

        ExportPdfCommand = new RelayCommand(ExportPdf);
        ExportRealDataPdfCommand = new RelayCommand(ExportRealDataPdf, () => _buildRealData is not null);

        // Inicializace hodnot parametrů z defaults
        foreach (var p in parameters)
            _paramValues[p.Name] = p.GetDefaultTyped();

        // Vytvoření UI kontrolek pro parametry
        foreach (var p in parameters)
            Parameters.Add(new ParameterControlVM(p, _paramValues, null));

        HasParameters = Parameters.Count > 0;
    }

    private void ExportPdf()
    {
        var filePath = ShowSaveDialog("Exportovat PDF (mock data)");
        if (filePath is null) return;

        try
        {
            var data = _buildMockData(_paramValues);
            RequestRenderPdf?.Invoke(filePath, data);
            StatusText = $"PDF uloženo: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Chyba: {ex.Message}";
        }
    }

    private void ExportRealDataPdf()
    {
        if (_buildRealData is null) return;

        var filePath = ShowSaveDialog("Exportovat PDF (reálná data z Caché)");
        if (filePath is null) return;

        try
        {
            StatusText = "Načítání dat z Caché...";
            var dataPages = _buildRealData(_paramValues);

            if (dataPages is null or { Count: 0 })
            {
                StatusText = "Caché nevrátila žádná data.";
                return;
            }

            RequestRenderMultiPdf?.Invoke(filePath, dataPages);
            StatusText = $"PDF uloženo ({dataPages.Count} stránek dat): {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Chyba: {ex.Message}";
        }
    }

    private string? ShowSaveDialog(string title)
    {
        var dlg = new SaveFileDialog
        {
            Title = title,
            Filter = "PDF soubory (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            FileName = (string.IsNullOrWhiteSpace(_template.Name)
                ? "sablona" : _template.Name) + ".pdf"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
