using System.Windows;
using System.Windows.Threading;
using EDControls;
using EDControls.EDViewModel;
using TemplateDesigner.Config;

namespace TemplateDesigner;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Globální default icon pro všechna Window v aplikaci. Implicit <Style TargetType="Window">
        // v App.xaml se v WPF neaplikuje na derived Window classes (známý quirk) — registrace
        // class handleru je spolehlivá pro všechna okna i dialogy.
        EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent,
            new RoutedEventHandler(OnAnyWindowLoaded));

#if DEBUG
        // Jednorázový test detektoru JŘ — zakomentuj po otestování
        if (e.Args.Length > 0 && e.Args[0] == "--test-detector")
        {
            var pdf1 = @"c:\Users\miesb\Downloads\kv_zastavkovy.PDF";
            var pdf2 = @"c:\Users\miesb\Downloads\kv_zastavkovy_2.PDF";
            var output = new System.Text.StringBuilder();
            if (System.IO.File.Exists(pdf1))
                output.AppendLine(PdfImport.ScheduleDetectorTest.Run(pdf1));
            if (System.IO.File.Exists(pdf2))
                output.AppendLine(PdfImport.ScheduleDetectorTest.Run(pdf2));
            var outPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(pdf1)!, "schedule-detect-results.txt");
            System.IO.File.WriteAllText(outPath, output.ToString());
            MessageBox.Show($"Výsledky uloženy: {outPath}", "Test detektoru");
            Shutdown();
            return;
        }
#endif

        EDServices.Initialize(new EDServices(null, new WindowNavigator(), new PopupDisplayer()));
        var settings = AppSettings.Load();
        var mlRest   = CacheSchemaWiring.TryCreateMlRest(settings);

        WarnIfCacheMisconfigured(settings, mlRest);

        if (mlRest is not null)
            EDServices.Current.UserPrefs = new EDControls.UserPrefs.CacheUserPrefsService(new MLRestAdapter(mlRest));

        // Per-user state (zoom, grid, splittery, recent files, instance defaults).
        // Krátký sync wait (200ms) pro warm-cache happy path; pokud propadne (cold-start
        // Caché HTTP typicky 2-3s), MainWindow startuje s defaults a po dokončení LoadAsync
        // se hodnoty aplikují přes event TdPreferencesService.Loaded (MainWindow subscribe).
        // Detail v EDControls/DOCS/05-userdata-scopes.md.
        var prefs = new TemplateDesigner.Services.TdPreferencesService(EDServices.Current.UserPrefs);
        prefs.LoadAsync().Wait(TimeSpan.FromMilliseconds(200));

        // Color picker recent colors integrace — static reference, žádný explicit DI.
        TemplateDesigner.Controls.ColorPickerHelper.Preferences = prefs;

        // Wave 2 (W2-1): registr agend — Caché auto-discovery z BaseAgenda subclasses.
        // Sync wait do 3s — pokud propadne, MainWindow startuje jen s GENERAL agendou
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
            // Standalone bez Caché — pouze GENERAL (synthetic injection v registru, ale registr není volaný)
            agendas = [new TemplateDesigner.Serialization.AgendaRegistryEntry("GENERAL", "Obecná (bez databáze)", "")];
        }

        new MainWindow(mlRest, prefs, agendas).Show();
    }

    /// <summary>
    /// Pokud nelze Caché vytvořit, zobrazí diagnostické varování.
    /// Embedded config by měl vždy uspět — pokud nebyl, něco je rozbité při buildu.
    /// </summary>
    private static void WarnIfCacheMisconfigured(AppSettings settings, ALWA.SYS.MLRest? mlRest)
    {
        if (mlRest is not null) return;

        var (title, message) = settings.Source switch
        {
            ConfigSource.None =>
                ("Caché — chybějící embedded config",
                 "Embedded baseline config se nenačetl. Pravděpodobně chybí EmbeddedResource " +
                 "položka v csproj nebo selhalo parsování JSON. Aplikace pojede bez Caché."),
            _ =>
                ("Caché — neplatná konfigurace",
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
        MessageBox.Show(
            $"Neošetřená výjimka:\n\n{e.Exception}",
            "Chyba aplikace",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}

