using System.Windows;
using System.Windows.Threading;
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

        EDServices.Initialize(new EDServices(null, new ReflectionWindowNavigator(), new PopupDisplayer()));
        var settings = AppSettings.Load();
        var mlRest   = CacheSchemaWiring.TryCreateMlRest(settings);
        new MainWindow(mlRest).Show();
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

