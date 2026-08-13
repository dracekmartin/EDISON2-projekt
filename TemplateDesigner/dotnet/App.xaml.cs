using System.Windows;
using System.Windows.Threading;
using EDControls;
using EDControls.EDViewModel;
using EDControls.ErrorReporting;
using EDControls.UserFonts;
using TemplateDesigner.Config;

namespace TemplateDesigner;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnUnhandledException;

        // Chyby mimo dispatcher by jinak zmizely beze stopy — do telemetrie, bez dialogu.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) ErrorReports.ReportFatal(ex);
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ErrorReports.ReportBackground(args.Exception);
            args.SetObserved();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Globální default icon pro všechna Window v aplikaci. Implicit <Style TargetType="Window">
        // v App.xaml se v WPF neaplikuje na derived Window classes (známý quirk) - registrace
        // class handleru je spolehlivá pro všechna okna i dialogy.
        EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent,
            new RoutedEventHandler(OnAnyWindowLoaded));

        // FormIdPrefix odděluje okna TD od shellu ve sdílené usage telemetrii a v persistenci stavu oken.
        EDServices.Initialize(new EDServices(null, new WindowNavigator(), new PopupDisplayer())
        {
            FormIdPrefix = "TD"
        });

        // Sdílený vzhled (font + světlý/tmavý režim) - jedna registrace, sdílené storage s EDISONMAIN2
        // (%APPDATA%\m-line\common\). Apply před MainWindow.Show eliminuje flicker; FileSystemWatcher
        // reaguje na změnu ze shellu (přepnutí dark v EDISONMAIN2 se projeví i tady za běhu).
        EDControls.EDAppearance.Initialize(this);

        var settings = AppSettings.Load();

        // Spuštění ze shellu (starý EDISON / EDISONMAIN2): session přijde v argumentech
        // EDISONMAIN2 kontraktu a přebije appsettings. Bez argů běží standalone režim.
        var launch = EdisonLaunchArgs.TryParse(e.Args);
        var mlRest = launch is not null
            ? CacheSchemaWiring.CreateMlRest(launch)
            : CacheSchemaWiring.TryCreateMlRest(settings);

        if (launch is null)
        {
            if (EdisonLaunchArgs.IsEdisonLaunch(e.Args))
                MessageBox.Show(
                    "Aplikace byla spuštěna s nevalidními argumenty EDISONMAIN2 kontraktu - " +
                    "session shellu se nepřevzala. Pokračuji ve standalone režimu (appsettings).",
                    "Caché - neplatné argumenty spuštění",
                    MessageBoxButton.OK, MessageBoxImage.Warning);

            WarnIfCacheMisconfigured(settings, mlRest);
        }

        if (mlRest is not null)
            EDServices.Current.UserPrefs = new EDControls.UserPrefs.CacheUserPrefsService(new MLRestAdapter(mlRest));

        // Per-user state (zoom, grid, splittery, recent files, instance defaults).
        // Krátký sync wait (200ms) pro warm-cache happy path; pokud propadne (cold-start
        // Caché HTTP typicky 2-3s), MainWindow startuje s defaults a po dokončení LoadAsync
        // se hodnoty aplikují přes event TdPreferencesService.Loaded (MainWindow subscribe).
        // Detail v EDControls/DOCS/05-userdata-scopes.md.
        var prefs = new TemplateDesigner.Services.TdPreferencesService(EDServices.Current.UserPrefs);
        prefs.LoadAsync().Wait(TimeSpan.FromMilliseconds(200));

        // Color picker recent colors integrace - static reference, žádný explicit DI.
        TemplateDesigner.Controls.ColorPickerHelper.Preferences = prefs;

        // EDInputForm Color field - wire fancy color picker (s recent colors) místo žádného fallback.
        EDControls.EDInputForm.EDInputForm.PickColorImpl =
            (owner, hex) => TemplateDesigner.Controls.ColorPickerHelper.PickColor(owner, hex);

        // EDInputForm Image field - file dialog + downscale → Base64. EDControls nemá obrázkové zpracování.
        EDControls.EDInputForm.EDInputForm.PickImageImpl = owner =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Vybrat obrázek",
                Filter = "Obrázky (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Všechny soubory (*.*)|*.*"
            };
            if (dlg.ShowDialog(owner) != true) return null;
            try
            {
                var bytes = System.IO.File.ReadAllBytes(dlg.FileName);
                return TemplateDesigner.Models.Elements.ImageElement.EncodeAndResize(bytes);
            }
            catch (Exception ex)
            {
                // R2: dřív tichý null - uživatel nevěděl, proč se vybraný obrázek nenačetl
                EDServices.Current.PopupDisplayer.Error(ex);
                return null;
            }
        };

        // Registr agend - Caché auto-discovery z BaseAgenda subclasses.
        // Sync wait do 3s - pokud propadne, MainWindow startuje jen s GENERAL agendou
        // (fallback v CacheAgendaRegistry). Cold-start Caché HTTP je dominantní, warm calls 50ms.
        IReadOnlyList<TemplateDesigner.Serialization.AgendaRegistryEntry> agendas = [];
        if (mlRest is not null)
        {
            var agendaRegistry = new TemplateDesigner.Serialization.CacheAgendaRegistry(new MLRestAdapter(mlRest));
            var listTask = agendaRegistry.ListAsync();
            if (listTask.Wait(TimeSpan.FromSeconds(3)))
                agendas = listTask.Result;
        }
        else
        {
            // Standalone bez Caché - pouze GENERAL (synthetic injection v registru, ale registr není volaný)
            agendas = [new TemplateDesigner.Serialization.AgendaRegistryEntry("GENERAL", "Obecná (bez databáze)", "")];
        }

        // Seed systémových (M-line) šablon z embedded JSON → MLINE větev (governance krok 4).
        // Fire-and-forget: neblokuje start; picker je ukáže v sekci „M-line" po dokončení seedu.
        if (mlRest is not null)
            _ = TemplateDesigner.Services.SystemTemplateSeeder.SeedAsync(
                new TemplateDesigner.Services.CacheTemplateService(mlRest));

        // inputParam kontraktu "Agenda=<kód>" → startovní agenda šablony. Uzel stromu legacy
        // shellu nese kód v atributu input (JŘ vs MHD místo), MHD2 tlačítko ho posílá natvrdo.
        string? startupAgendaCode = null;
        if (launch?.InputParam.StartsWith("Agenda=", StringComparison.OrdinalIgnoreCase) == true)
            startupAgendaCode = launch.InputParam["Agenda=".Length..].Trim();

        new MainWindow(mlRest, prefs, agendas, startupAgendaCode).Show();
    }

    /// <summary>
    /// Pokud nelze Caché vytvořit, zobrazí diagnostické varování.
    /// Embedded config by měl vždy uspět - pokud nebyl, něco je rozbité při buildu.
    /// </summary>
    private static void WarnIfCacheMisconfigured(AppSettings settings, ALWA.SYS.MLRest? mlRest)
    {
        if (mlRest is not null) return;

        var (title, message) = settings.Source switch
        {
            ConfigSource.None =>
                ("Caché - chybějící embedded config",
                 "Embedded baseline config se nenačetl. Pravděpodobně chybí EmbeddedResource " +
                 "položka v csproj nebo selhalo parsování JSON. Aplikace pojede bez Caché."),
            _ =>
                ("Caché - neplatná konfigurace",
                 $"Sekce 'cache' v configu (zdroj: {settings.Source}) chybí nebo je neplatná " +
                 $"(prázdný Server / nevalidní Port). Aplikace pojede bez Caché. " +
                 $"Pro override vlož '{AppSettings.ConfigFileName}' vedle exe.")
        };

        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static void OnAnyWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window) return;
        if (window.Icon is not null) return;   // explicit Icon (např. MainWindow) ponechán
        if (Application.Current.TryFindResource("EDIcons.Molecule") is System.Windows.Media.ImageSource icon)
            window.Icon = icon;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Sdílený popup místo MessageBoxu: nese stack trace, referenční číslo a hlášení
        // do telemetrie. Dřív šel detail jen do Debug.WriteLine, takže v Release mizel.
        try
        {
            // Handler visí od konstruktoru App, tedy dřív než EDServices.Initialize v OnStartup —
            // pád při startu by na EDServices.Current hodil druhou výjimku.
            EDServices.Current.PopupDisplayer.Error(e.Exception);
        }
        catch
        {
            MessageBox.Show($"Došlo k neočekávané chybě:\n\n{e.Exception}",
                "Chyba aplikace", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        e.Handled = true;
    }
}

