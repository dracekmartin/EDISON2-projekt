using System;
using System.Threading;
using System.Threading.Tasks;
using EDControls.EDViewModel;

namespace EDISONMAIN2;

/// <summary>
/// Nástroje → Zkontrolovat aktualizace. Serverová instalace: legacy runner <see cref="RunnerForm"/> v EDISONMAIN.exe
/// hostuje nezměněnou <c>frmNoMDI.CheckForUpdate</c> (práva, licence, dialogy, EDTRANSFER2/EDISONINS jako starý shell).
/// Stanice: tatáž kontrola jako po přihlášení (<see cref="StationUpdate"/>).
/// </summary>
public class ManualUpdate(EDServices services, ShellShutdown shutdown)
{
    internal const string RunnerExe = "EDISONMAIN.exe";
    internal const string RunnerForm = "ALVA.EDISONMAIN.formEDISONMAINServerUpdate";
    internal const string NoNewVersionMessage = "Nebyly nalezeny žádné nové aktualizace.";

    /// <param name="progress">Overlay „Akce probíhá" do konce zjišťování na stanici, resp. do okna runneru.</param>
    /// <returns>true = shell se má zavřít.</returns>
    public virtual async Task<bool> RunAsync(bool testVersion, Func<IDisposable> progress)
    {
        var updater = CreateUpdater();
        var overlay = progress();
        var closing = false;
        try
        {
            // Klon s Locks=0 jako SessionOpener: po FULL vydání by version gate request odmítl.
            var rest = services.RequireMLRest.With(cacheLocks: 0);
            var settings = await Task.Run(() => updater.LoadSettings(rest));
            if (!settings.ServerInstallation)
            {
                var result = await Task.Run(() => updater.Check(settings));
                overlay.Dispose();
                if (StationUpdate.Apply(updater, settings, result, services.PopupDisplayer)) return true;
                services.PopupDisplayer.InfoSystem(NoNewVersionMessage);
                return false;
            }

            var eventName = $"EDISONMAIN2.ServerUpdate.{Guid.NewGuid():N}";
            using var closeRequested = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
            var run = services.WindowNavigator.StartLegacyDetachedDialog(RunnerExe, RunnerForm,
                RunnerInput(testVersion, Environment.ProcessId, eventName));
            if (run is null) return false;
            shutdown.SessionEndExpected = true;
            await run.WindowShown;
            overlay.Dispose();

            var signaled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var wait = ThreadPool.RegisterWaitForSingleObject(closeRequested, (_, _) => signaled.TrySetResult(),
                null, Timeout.Infinite, executeOnlyOnce: true);
            try
            {
                await Task.WhenAny(run.Exited, signaled.Task);
            }
            finally
            {
                wait.Unregister(null);
            }

            // Signál a konec runneru můžou dorazit současně (bez PID shellu runner nečeká), rozhoduje stav události.
            if (closeRequested.WaitOne(0))
            {
                // Session odhlásil runner, zbylé requesty zavírání jdou bez kontroly zámku (jako ServerSessionEnd).
                services.RequireMLRest.CacheLocks = 0;
                return closing = true;
            }
            return false;
        }
        finally
        {
            if (!closing) shutdown.SessionEndExpected = false;
            overlay.Dispose();
        }
    }

    /// <summary>EDParamIn runneru, čte ho <c>formEDISONMAINServerUpdate.InputValue</c> v repu EDISON.</summary>
    internal static string RunnerInput(bool testVersion, int shellPid, string closeEvent) =>
        $"Test={(testVersion ? 1 : 0)}|ShellPid={shellPid}|CloseEvent={closeEvent}";

    protected virtual Updater CreateUpdater() => new();
}
