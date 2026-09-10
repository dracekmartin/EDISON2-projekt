using System.Diagnostics;
using System.IO;
using System.Windows;

namespace EDUPDATE2;

public partial class UpdateWindow : Window
{
    private readonly UpdaterArgs _args;
    private readonly UpdateLog _log;
    private readonly CancellationTokenSource _cancelSource = new();
    private UpdateEngine? _engine;
    private bool _finished;
    private bool _closeRequested;

    public UpdateWindow(UpdaterArgs args, UpdateLog log)
    {
        _args = args;
        _log = log;
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    internal enum CloseDecision { Allow, Block, CancelAndBlock }

    /// <summary>Během swap/rollbacku nesmí proces zaniknout - cíl by zůstal nekonzistentní.</summary>
    internal bool InCriticalPhase =>
        DecideClose(_finished, _engine?.CurrentPhase, closeRequested: false) == CloseDecision.Block;

    internal static bool ShouldRunExe(int exitCode, string runExe) =>
        exitCode == UpdateEngine.ExitOk && runExe != "";

    internal static CloseDecision DecideClose(bool finished, UpdatePhase? enginePhase, bool closeRequested)
    {
        if (finished) return CloseDecision.Allow;
        if (enginePhase is UpdatePhase.Swap or UpdatePhase.Rollback) return CloseDecision.Block;
        return closeRequested ? CloseDecision.Block : CloseDecision.CancelAndBlock;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        var progress = new Progress<UpdateProgress>(OnProgress);
        _engine = new UpdateEngine(_args, _log, progress, _cancelSource.Token);
        UpdateResult result;
        try
        {
            var engine = _engine;
            result = await Task.Run(engine.Run);
        }
        catch (Exception ex)
        {
            _log.Write("Výjimka", ex.ToString());
            result = new(UpdateEngine.ExitBadParamsOrAborted, $"Neočekávaná chyba aktualizace: {ex.Message}");
        }
        _finished = true;
        Environment.ExitCode = result.ExitCode;
        if (result.ExitCode == UpdateEngine.ExitOk)
        {
            if (ShouldRunExe(result.ExitCode, _args.RunExe)) RunExeIfRequested();
            Close();
            return;
        }
        if (_closeRequested)
        {
            Close();
            return;
        }
        PhaseText.Text = "Aktualizace se nezdařila.";
        DetailText.Text = $"{result.Message}{Environment.NewLine}Podrobnosti: {_log.FilePath}";
        Progress.IsIndeterminate = false;
        CloseButton.IsEnabled = true;
    }

    private void RunExeIfRequested()
    {
        try
        {
            StartProcess(new ProcessStartInfo
            {
                FileName = Path.Combine(_args.TargetPath, _args.RunExe),
                WorkingDirectory = _args.TargetPath,
                UseShellExecute = true,
            });
        }
        catch (Exception e)
        {
            _log.Write("Chyba", $"Spuštění {_args.RunExe} selhalo: {e.Message}");
            MessageBox.Show($"Aktualizace proběhla, ale {_args.RunExe} se nepodařilo spustit. Spusťte EDISON ručně.",
                Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnProgress(UpdateProgress progress)
    {
        PhaseText.Text = PhaseLabel(progress.Phase);
        DetailText.Text = progress.Detail;
        if (progress.Percent >= 0)
        {
            Progress.IsIndeterminate = false;
            Progress.Value = progress.Percent;
        }
    }

    private static string PhaseLabel(UpdatePhase phase) => phase switch
    {
        UpdatePhase.Validation => "Kontroluji parametry...",
        UpdatePhase.WaitingForProcess => "Čekám na zavření aplikace...",
        UpdatePhase.Selection => "Zjišťuji soubory k aktualizaci...",
        UpdatePhase.Staging => "Stahuji soubory...",
        UpdatePhase.Swap => "Instaluji soubory...",
        UpdatePhase.Rollback => "Obnovuji původní soubory...",
        UpdatePhase.Done => "Hotovo.",
        _ => "",
    };

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Fáze se čte z enginu, ne z Progress eventů - ty chodí přes Dispatcher se zpožděním.
        switch (DecideClose(_finished, _engine?.CurrentPhase, _closeRequested))
        {
            case CloseDecision.Allow:
                return;
            case CloseDecision.Block:
                e.Cancel = true;
                return;
            case CloseDecision.CancelAndBlock:
                // Mimo kritickou fázi se zavření odloží: engine dojede na checkpoint, uklidí staging
                // a vrátí exit 1 (cíl beze změny) - proces nesmí umřít uprostřed práce s diskem.
                e.Cancel = true;
                _closeRequested = true;
                _log.Write("UPDATER", "Zavření vyžádáno uživatelem, přerušuji.");
                _cancelSource.Cancel();
                return;
        }
    }

    protected virtual void StartProcess(ProcessStartInfo startInfo) => Process.Start(startInfo);

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
