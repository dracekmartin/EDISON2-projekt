using System.IO;
using EDControls;
using EDControls.EDViewModel;
using EDControls.ErrorReporting;
using TemplateDesigner.Config;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>
/// PDF ze spuštění bez hlavního okna (<c>Vystup=PDF</c>): ukazuje jen průběh, PDF otevře ve výchozím prohlížeči
/// a zavře se. Rozdílový JŘ z porovnání linek bere šablonu jen z .exe (<see cref="EmbeddedTemplates.LaunchFolder"/>),
/// v návrháři se nenabízí. Ostatní agendy (hromadný tisk z okna JŘ, `B190`) berou šablonu z databáze podle pravidla
/// na serveru (<see cref="TemplateDesignerViewModel.ResolvePrintTemplateAsync"/>).
/// </summary>
public class LaunchPdfWindowViewModel : EDViewModel
{
    private readonly LaunchInput _input;
    private readonly Task<AgendaRegistryLoad> _agendas;
    private readonly Func<IReadOnlyList<AgendaRegistryEntry>, TemplateDesignerViewModel> _createDesigner;

    private string _statusText = "Připravuji PDF…";
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    /// <summary>Kam se PDF uloží, když spuštění složku neurčí (<see cref="LaunchInput.FolderKey"/>); testy podstrčí vlastní.</summary>
    public string Folder { get; init; } = Path.Combine(Path.GetTempPath(), "EDISON", "TemplateDesigner");

    /// <summary>Otevření hotového PDF; testy ho nahradí, aby se nespouštěl prohlížeč.</summary>
    public Action<string> OpenPdf { get; init; } = DefaultBrowser.Open;

    /// <summary>Výpadek spojení do telemetrie; testy ho nahradí, aby nic neodcházelo.</summary>
    public Action<Exception> ReportFailure { get; init; } = ErrorReports.ReportBackground;

    /// <param name="agendas">Načítání registru agend spuštěné při startu - okno na něj počká.</param>
    /// <param name="createDesigner">Návrhář nad načtenými agendami.</param>
    public LaunchPdfWindowViewModel(LaunchInput input, Task<AgendaRegistryLoad> agendas,
                                    Func<IReadOnlyList<AgendaRegistryEntry>, TemplateDesignerViewModel> createDesigner)
    {
        _input = input;
        _agendas = agendas;
        _createDesigner = createDesigner;
    }

    public async Task RunAsync()
    {
        try
        {
            var agenda = _input.AgendaCode ?? "";
            var sablonaZeSpusteni = _input.Get(LaunchInput.TemplateKey);
            var porovnani = sablonaZeSpusteni is null
                ? await Task.Run(() => EmbeddedTemplates.ForAgenda(EmbeddedTemplates.LaunchFolder, agenda))
                : null;

            var folder = OutputFolder(_input, Folder);
            if (!await Task.Run(() => CanWriteTo(folder)))
            {
                PopupDisplayer.Info($"Do složky {folder} nejde zapisovat, PDF nevzniklo. " + (porovnani is not null
                    ? "Zvolte v okně Porovnání linek jinou složku."
                    : "Zvolte ve formuláři tisku jinou složku."));
                return;
            }

            var registry = await _agendas;
            if (registry.Failure is { } failure)
            {
                if (!MLRestFailure.IsTransport(failure))
                {
                    PopupDisplayer.Error(failure);
                    return;
                }
                ReportFailure(failure);
                PopupDisplayer.Info(UnreachableServerMessage);
                return;
            }
            if (!registry.Agendas.Any(a => a.Code.Equals(agenda, StringComparison.OrdinalIgnoreCase)))
            {
                PopupDisplayer.Info($"Návrhář na serveru nenašel agendu {agenda}, PDF nevzniklo.");
                return;
            }

            var designer = _createDesigner(registry.Agendas);
            var (template, bezSablony) = porovnani is not null
                ? (porovnani, null)
                : await designer.ResolvePrintTemplateAsync(agenda, sablonaZeSpusteni);
            if (template is null)
            {
                PopupDisplayer.Info(bezSablony ?? $"Pro agendu {agenda} návrhář PDF nevytvoří - nemá k ní šablonu.");
                return;
            }

            StatusText = $"Připravuji PDF: {template.Name}…";
            var vysledek = await designer.ExportLaunchPdfAsync(_input, template, folder,
                new Progress<string>(text => StatusText = text));
            if (vysledek.Path is null)
            {
                PopupDisplayer.Info(vysledek.Duvod ?? "PDF se nepodařilo vytvořit.");
                return;
            }
            // Víc souborů se neotevírá ani se neotevírá složka (Jan 4. 11.): uživatel dostane, kolik jich je a kde.
            if (vysledek.Souboru > 1)
                PopupDisplayer.Info(UlozenoText(vysledek.Souboru, vysledek.Path)
                    + (vysledek.Upozorneni is { } chybi ? "\n\n" + chybi : ""));
            else
            {
                OpenPdf(vysledek.Path);
                if (vysledek.Upozorneni is { } upozorneni) PopupDisplayer.Info(upozorneni);
            }
        }
        catch (Serialization.CacheAgendaDataException ex)
        {
            // Chyba v datech linky (`B194a` C11) - uživatel ji opraví v jízdních řádech, není to chyba programu.
            PopupDisplayer.Info(ex.Linka is null ? ex.Message : $"Linka {ex.Linka} nejde vytisknout: {ex.Message}");
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

    /// <summary>Potvrzení tisku do souboru pro každou linku.</summary>
    internal static string UlozenoText(int souboru, string slozka) =>
        $"Uloženo {souboru} {(souboru is >= 2 and <= 4 ? "soubory" : "souborů")} PDF do složky {slozka}.";

    /// <summary>Složka ze spuštění s rozbalenými proměnnými prostředí, bez ní <paramref name="fallback"/>.</summary>
    internal static string OutputFolder(LaunchInput input, string fallback) =>
        input.Get(LaunchInput.FolderKey)?.Trim() is { Length: > 0 } folder
            ? Environment.ExpandEnvironmentVariables(folder)
            : fallback;

    // Právo zápisu se dá zjistit jen zápisem. Zkouší se před načtením dat, ať uživatel nečeká na PDF,
    // které nepůjde uložit; relativní cesta by mířila do pracovní složky procesu.
    internal static bool CanWriteTo(string folder)
    {
        if (!Path.IsPathFullyQualified(folder)) return false;
        try
        {
            Directory.CreateDirectory(folder);
            using (File.Create(Path.Combine(folder, $".td-{Guid.NewGuid():N}.tmp"), 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }

    // Do PDF se návrhář spouští z běžící session EDISONu, server tedy z tohoto počítače dostupný byl.
    // Hlášku čte provoz: bez adresy serveru a textu od systému, ty nese hlášení do telemetrie.
    internal const string UnreachableServerMessage =
        "Návrhář sestav se nespojil se serverem EDISON, PDF nevzniklo.\n\n" +
        "Pokud vám ostatní části EDISONu fungují, spojení návrháři nejspíš blokuje firewall nebo antivirus " +
        "na tomto počítači. Předejte správci sítě, že programu TemplateDesigner.exe je potřeba povolit připojení k serveru EDISON.";
}
