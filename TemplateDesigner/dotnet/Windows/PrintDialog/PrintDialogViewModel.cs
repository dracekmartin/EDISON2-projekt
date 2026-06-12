using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using ALWA.SYS;
using EDControls.EDViewModel;
using Microsoft.Win32;
using TemplateDesigner.Models;
using TemplateDesigner.Services;

namespace TemplateDesigner;

public class PrintDialogViewModel : EDViewModel
{
    private readonly Template _template;
    private readonly Dictionary<string, object?> _paramValues;
    private readonly IReadOnlyList<TemplateParameter> _parameters;
    private readonly string? _agendaClass;
    private readonly Action? _onParamsChanged;
    private readonly Func<Dictionary<string, object?>, DataContext?> _buildMockData;
    private readonly Func<Dictionary<string, object?>, List<DataContext>?>? _buildRealData;
    private readonly TdPreferencesService? _preferences;

    private string _statusText = "";
    private bool _hasParameters;

    /// <summary>FQN Caché agenda třídy pro EDInputForm AgendaParams (null = bez parametrů / GENERAL).</summary>
    public string? AgendaClass => _agendaClass;

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
        Action? onParamsChanged = null,
        string? agendaClass = null)
    {
        _template = template;
        _paramValues = sharedParamValues;
        _parameters = parameters;
        _agendaClass = agendaClass;
        _onParamsChanged = onParamsChanged;
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

        // Parametry renderuje EDInputForm (CacheClass=AgendaParams) v code-behindu; VM jen drží
        // definice + sdílený dict a poskytuje konverzní/visibility helpery (viz níže).
        HasParameters = parameters.Count > 0;
    }

    // ── EDInputForm integrace ─────────────────────────────────────────────────

    /// <summary>IN params pro AgendaParams.Init — agendaClass + JSON aktuálních hodnot pro prefill.</summary>
    public CVar BuildInitParams()
    {
        var init = new CVar();
        init["agendaClass"] = new CVar(_agendaClass ?? "");

        var vals = new Dictionary<string, string>();
        foreach (var p in _parameters)
        {
            var v = _paramValues.TryGetValue(p.Name, out var o) ? o : p.GetDefaultTyped();
            vals[p.Name] = ToFieldString(v, p.Type);
        }
        init["values"] = new CVar(JsonSerializer.Serialize(vals));
        return init;
    }

    /// <summary>Zapíše hodnotu pole (string z EDInputFormu) do sdíleného dict s typovou konverzí
    /// a spustí živý náhled (<c>onParamsChanged</c>). Typování zrcadlí <see cref="TemplateParameter.GetDefaultTyped"/>.</summary>
    public void SetParamValue(string name, string fieldValue)
    {
        var type = _parameters.FirstOrDefault(p => p.Name == name)?.Type ?? "string";
        _paramValues[name] = type switch
        {
            "bool"   => fieldValue == "1" || string.Equals(fieldValue, "true", StringComparison.OrdinalIgnoreCase),
            "number" => float.TryParse(fieldValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f,
            _        => (object)fieldValue
        };
        _onParamsChanged?.Invoke();
    }

    /// <summary>Vyhodnotí <c>visibleWhen</c> parametru proti aktuálním hodnotám (sdílený dict).
    /// Logika v <see cref="TemplateParameter.IsVisible"/> — jeden zdroj pro preview lištu i tisk.</summary>
    public bool IsParamVisible(string name) =>
        _parameters.FirstOrDefault(x => x.Name == name)?.IsVisible(_paramValues) ?? true;

    private static string ToFieldString(object? v, string type) => type switch
    {
        "bool"   => v is true ? "true" : "false",
        "number" => v is float f ? f.ToString(CultureInfo.InvariantCulture) : (v?.ToString() ?? ""),
        _        => v?.ToString() ?? ""
    };

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
