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

    private string _statusText = "";
    private bool _hasParameters;

    public ObservableCollection<ParameterControlVM> Parameters { get; } = [];

    public string TemplateName => string.IsNullOrWhiteSpace(_template.Name)
        ? "Bez názvu" : _template.Name;

    public bool HasParameters { get => _hasParameters; private set => Set(ref _hasParameters, value); }
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

    public RelayCommand ExportPdfCommand { get; }

    /// <summary>Code-behind napojí na PreviewCanvas.RenderToPdf.</summary>
    public Action<string, DataContext?>? RequestRenderPdf { get; set; }

    public PrintDialogViewModel(
        Template template,
        IReadOnlyList<TemplateParameter> parameters,
        Func<Dictionary<string, object?>, DataContext?> buildMockData)
    {
        _template = template;
        _buildMockData = buildMockData;

        ExportPdfCommand = new RelayCommand(ExportPdf);

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
        var dlg = new SaveFileDialog
        {
            Title = "Exportovat PDF",
            Filter = "PDF soubory (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            FileName = (string.IsNullOrWhiteSpace(_template.Name)
                ? "sablona" : _template.Name) + ".pdf"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var data = _buildMockData(_paramValues);
            RequestRenderPdf?.Invoke(dlg.FileName, data);
            StatusText = $"PDF uloženo: {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Chyba: {ex.Message}";
        }
    }
}
