using System.IO;
using EDControls.EDViewModel;
using TemplateDesigner.Config;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>
/// PDF ze spuštění bez hlavního okna (<c>Vystup=PDF</c>, rozdílový JŘ z porovnání linek v modulu JŘ):
/// ukazuje jen průběh, PDF otevře ve výchozím prohlížeči a zavře se. Šablonu bere jen z .exe
/// (<see cref="EmbeddedTemplates.LaunchFolder"/>), ne z databáze - v návrháři se nenabízí.
/// </summary>
public class LaunchPdfWindowViewModel : EDViewModel
{
    private readonly LaunchInput _input;
    private readonly Func<TemplateDesignerViewModel> _createDesigner;

    private string _statusText = "Připravuji PDF…";
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    /// <summary>Kam se PDF uloží; testy podstrčí vlastní složku.</summary>
    public string Folder { get; init; } = Path.Combine(Path.GetTempPath(), "EDISON", "TemplateDesigner");

    /// <summary>Otevření hotového PDF; testy ho nahradí, aby se nespouštěl prohlížeč.</summary>
    public Action<string> OpenPdf { get; init; } = DefaultBrowser.Open;

    public LaunchPdfWindowViewModel(LaunchInput input, Func<TemplateDesignerViewModel> createDesigner)
    {
        _input = input;
        _createDesigner = createDesigner;
    }

    public async Task RunAsync()
    {
        try
        {
            var agenda = _input.AgendaCode ?? "";
            var template = await Task.Run(() => EmbeddedTemplates.ForAgenda(EmbeddedTemplates.LaunchFolder, agenda));
            if (template is null)
            {
                PopupDisplayer.Info($"Pro agendu {agenda} návrhář PDF nevytvoří - nemá k ní šablonu.");
                return;
            }

            StatusText = $"Připravuji PDF: {template.Name}…";
            var (path, duvod) = await _createDesigner().ExportLaunchPdfAsync(_input, template, Folder);
            if (path is null)
            {
                PopupDisplayer.Info(duvod ?? "PDF se nepodařilo vytvořit.");
                return;
            }
            OpenPdf(path);
        }
        catch (Exception ex)
        {
            PopupDisplayer.Error(ex);
        }
        finally
        {
            CloseDialog(true);
        }
    }
}
