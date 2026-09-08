using System.Windows;
using System.Windows.Threading;
using ALWA.SYS;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;
using List = EDControls.EDList.EDList;

namespace EDControls.Tests;

/// <summary>
/// Dokumentovaná invarianta LoadListAsync (EDList/CLAUDE.md): dotazy se serializují
/// a odpověď staršího dotazu už do seznamu nedosedne - rychlé přepnutí filtru nezamíchá řádky.
/// </summary>
public class EDListLoadTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void LoadListAsync_StaleResponse_DoesNotLandAfterNewerRequest()
    {
        RunSta(() =>
        {
            var mock = MLRestMocks.Create();
            var firstQueryStarted = new ManualResetEventSlim();
            var releaseFirst = new ManualResetEventSlim();
            int calls = 0;
            mock.QueryOrThrow("ALVA.MockList", "LoadList", Arg.Any<CVar>()).Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    firstQueryStarted.Set();
                    releaseFirst.Wait(TimeSpan.FromSeconds(10));
                    return RowsOutput("stara");
                }
                return RowsOutput("nova");
            });
            var list = new List
            {
                CacheClass = "ALVA.MockList",
                ServicesOverride = new EDServices(mock,
                    Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()),
            };

            var first = list.LoadListAsync();
            Assert.True(firstQueryStarted.Wait(TimeSpan.FromSeconds(10)));
            var second = list.LoadListAsync();   // novější požadavek - starší je ještě v letu
            releaseFirst.Set();
            PumpUntilCompleted(first, second);

            Assert.True(first.IsCompletedSuccessfully);
            Assert.True(second.IsCompletedSuccessfully);
            var row = Assert.Single(list.Rows);
            Assert.Equal("nova", row.Id);   // stará odpověď se zahodila
        });
    }

    private static CVar RowsOutput(string id)
    {
        var output = new CVar();
        output["1"] = new CVar($$"""{"id":"{{id}}","name":"radek {{id}}"}""");
        return output;
    }

    // Awaity LoadListAsync pokračují přes DispatcherSynchronizationContext - bez pumpování
    // by uvázly, dokud drží test vlákno uvnitř Invoke. Timeout mění případný deadlock
    // na viditelné selhání assertu.
    private static void PumpUntilCompleted(params Task[] tasks)
    {
        var frame = new DispatcherFrame();
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        timeout.Tick += (_, _) => { timeout.Stop(); frame.Continue = false; };
        timeout.Start();
        Task.WhenAll(tasks).ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        timeout.Stop();
    }

    // EDList.xaml čte StaticResource EDIcons/EDStrings → Application se slovníky appky;
    // trvalé app vlákno viz EDComponentToolbarFlatTests.RunSta.
    private static void EnsureApplication()
    {
        if (Application.Current is not null) return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
    }

    private static void RunSta(Action action) =>
        Sta.RunOnAppThread(() => { EnsureApplication(); action(); });
}
