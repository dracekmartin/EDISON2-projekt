using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using EDControls.EDViewModel;
using Microsoft.Win32;
using TemplateDesigner.Models;
using TemplateDesigner.Services;

namespace TemplateDesigner;

public class PrintDialogViewModel : EDViewModel
{
    private readonly Template _template;
    private readonly Dictionary<string, object?> _paramValues;
    private readonly Func<Dictionary<string, object?>, DataContext?> _buildMockData;
    private readonly Func<Dictionary<string, object?>, List<DataContext>?>? _buildRealData;
    private readonly TdPreferencesService? _preferences;

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

    /// <summary>
    /// PrintDialog operuje nad **sdíleným** dictionary parametrů (typicky <c>MainWindowViewModel.PreviewParamValues</c>).
    /// Tím je zaručeno že preview lišta a PrintDialog ukazují stejné hodnoty — když user změní
    /// param v PrintDialogu, preview se live aktualizuje přes <paramref name="onParamsChanged"/>
    /// callback (typicky <c>MainWindowViewModel.OnExternalParamsChanged</c>).
    /// </summary>
    public PrintDialogViewModel(
        Template template,
        IReadOnlyList<TemplateParameter> parameters,
        Dictionary<string, object?> sharedParamValues,
        Func<Dictionary<string, object?>, DataContext?> buildMockData,
        Func<Dictionary<string, object?>, List<DataContext>?>? buildRealData = null,
        TdPreferencesService? preferences = null,
        Action? onParamsChanged = null)
    {
        _template = template;
        _paramValues = sharedParamValues;
        _buildMockData = buildMockData;
        _buildRealData = buildRealData;
        _preferences = preferences;

        ExportPdfCommand = Command(ExportPdf);
        ExportRealDataPdfCommand = Command(ExportRealDataPdf, () => _buildRealData is not null);

        // _paramValues přichází už naplněný (preview lišta defaults + případně saved PrintParams
        // aplikované při openu šablony). Pro chybějící klíče (nový parametr přidaný po předchozím
        // savu) doplníme defaults — nepřepisuje existing values.
        foreach (var p in parameters)
            _paramValues.TryAdd(p.Name, p.GetDefaultTyped());

        // ParameterControlVM.Init helper předá onChanged do každé VM — při změně value se notifikace
        // propaguje do MainWindow (preview lišta + canvas refresh).
        ParameterControlVM.Init(Parameters, parameters, _paramValues, onChanged: onParamsChanged);

        HasParameters = Parameters.Count > 0;
    }

    /// <summary>
    /// Uloží aktuální _paramValues (= shared dict s preview lištou) do prefs (fire-and-forget).
    /// Volá se před export — perzistuje uživatelovy hodnoty pro příští session.
    /// MainWindow.OnWindowClosing taky uloží přes <c>SavePrintParamsAsync</c> jako safety.
    /// </summary>
    private void PersistParams()
    {
        if (_preferences is null) return;
        if (string.IsNullOrEmpty(_template.AgendaCode)) return;
        _ = _preferences.SavePrintParamsAsync(_template.AgendaCode, _paramValues);
    }

    private void ExportPdf()
    {
        var filePath = ShowSaveDialog("Exportovat PDF (mock data)");
        if (filePath is null) return;

        try
        {
            PersistParams();
            var data = _buildMockData(_paramValues);
            RequestRenderPdf?.Invoke(filePath, data);
            OpenPdf(filePath);
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
            PersistParams();
            StatusText = "Načítání dat z Caché...";
            var dataPages = _buildRealData(_paramValues);

            if (dataPages is null or { Count: 0 })
            {
                StatusText = "Caché nevrátila žádná data.";
                return;
            }

            RequestRenderMultiPdf?.Invoke(filePath, dataPages);
            OpenPdf(filePath);
            StatusText = $"PDF uloženo ({dataPages.Count} stránek dat): {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Chyba: {ex.Message}";
        }
    }

    private static void OpenPdf(string filePath)
    {
        try { Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }); }
        catch { /* PDF viewer nemusí být nainstalován */ }
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
