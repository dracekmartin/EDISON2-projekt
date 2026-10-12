using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using EDTesting;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Zápisy z UI threadu jdou na pozadí (zavření okna nečeká na síť) a ukončení aplikace na ně počká.
/// Sériově: <see cref="BackgroundWrites"/>, buffer telemetrie i <c>EDServices.Current</c> jsou stav procesu.
/// </summary>
[Collection(SharedStaticStateCollection.Name)]
public class BackgroundWritesTests
{
    public BackgroundWritesTests() => EDUsageTelemetry.ResetForTests();

    private static EDServices Services(IMLRest mlRest) =>
        new(mlRest, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

    [WindowFact]
    public void CloseWindow_ServerHangs_ReturnsAtOnceAndBothWritesReachClone()
    {
        // Při výpadku VPN zavření agendového okna nesmí zmrazit shell na uložení geometrie a telemetrii.
        var shared = new GatedMLRest("SaveParams", "LogUsageBatch");
        var services = Services(shared);
        EDServices.Initialize(services);
        try
        {
            var closing = TimeSpan.Zero;
            Sta.RunOnAppThread(() =>
            {
                var vm = new EDViewModel.EDViewModel(services);
                var window = new Window { ShowInTaskbar = false, ShowActivated = false, DataContext = vm };
                vm.AttachToWindow(window);
                EDUsageTelemetryHook.Hook(window, vm.UsageFormId!);
                window.Show();

                var watch = Stopwatch.StartNew();
                window.Close();
                closing = watch.Elapsed;
            });

            Assert.True(closing < TimeSpan.FromSeconds(2), $"Zavření okna čekalo na server {closing}.");
            shared.Gate.Set();
            Assert.True(BackgroundWrites.WaitPending(TimeSpan.FromSeconds(10)));
            Assert.True(shared.Received("SaveParams", onClone: true));
            Assert.True(shared.Received("LogUsageBatch", onClone: true));
        }
        finally
        {
            shared.Gate.Set();
            EDServices.Reset();
        }
    }

    [Fact]
    public void Flush_WriteInFlight_ReturnsOnlyAfterItLands()
    {
        // Shell volá Flush při ukončení těsně před odhlášením session, po něm by server zápis odmítl.
        var shared = new GatedMLRest("SaveParams");
        try
        {
            new CacheWindowStateStore("EDISON2.TestWindow", () => shared).Save(new Rect(0, 0, 100, 100), maximized: false);
            _ = Task.Delay(200).ContinueWith(_ => shared.Gate.Set());

            EDUsageTelemetry.Flush(shared);

            Assert.True(shared.Received("SaveParams", onClone: true));
        }
        finally
        {
            shared.Gate.Set();
        }
    }

    [Fact]
    public async Task PrefsSave_InFlight_IsWaitedOnAtExit()
    {
        var shared = new GatedMLRest("Save");
        var prefs = new CacheUserPrefsService(() => shared, _ => { });
        try
        {
            var save = prefs.SaveAsync("scope", "key", JsonNode.Parse("{}")!);
            _ = Task.Delay(200).ContinueWith(_ => shared.Gate.Set());

            Assert.True(BackgroundWrites.WaitPending(TimeSpan.FromSeconds(10)));
            Assert.True(shared.Received("Save", onClone: true));
            await save;
        }
        finally
        {
            shared.Gate.Set();
        }
    }

    [Fact]
    public void WaitPending_WriteHangsPastLimit_GivesUpAtLimit()
    {
        // Proces po zavření oken nesmí při výpadku spojení viset do TCP timeoutu.
        var shared = new GatedMLRest("SaveParams");
        try
        {
            new CacheWindowStateStore("EDISON2.TestWindow", () => shared).Save(new Rect(0, 0, 100, 100), maximized: false);
            var watch = Stopwatch.StartNew();

            Assert.False(BackgroundWrites.WaitPending(TimeSpan.FromMilliseconds(200)));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"Čekání trvalo {watch.Elapsed}.");
        }
        finally
        {
            shared.Gate.Set();
            BackgroundWrites.WaitPending(TimeSpan.FromSeconds(10));
        }
    }
}
