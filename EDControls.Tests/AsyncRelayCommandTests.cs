using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

// Sdílí serializovanou kolekci s ostatními třídami sahajícími na EDServices.Current
// (Initialize/Reset v *_FallsBackToEDServicesCurrent + fallback v Execute catch) - viz SharedStaticStateCollection.
[Collection(SharedStaticStateCollection.Name)]
public class AsyncRelayCommandTests
{
    private static EDServices ServicesWith(IPopupDisplayer popup) =>
        new(mlRest: null,
            windowNavigator: Substitute.For<IWindowNavigator>(),
            popupDisplayer: popup);

    [Fact]
    public async Task Execute_AwaitsTask()
    {
        var invoked = false;
        var done = new TaskCompletionSource();
        var cmd = new AsyncRelayCommand(async () =>
        {
            await Task.Yield();
            invoked = true;
            done.SetResult();
        });

        cmd.Execute(null);
        await done.Task;   // deterministické: počkáme přímo na dokončení handleru

        Assert.True(invoked);
    }

    [Fact]
    public void CanExecute_NullPredicate_TrueByDefault()
    {
        var cmd = new AsyncRelayCommand(() => Task.CompletedTask);

        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public void CanExecute_RespectsPredicate()
    {
        var allowed = false;
        var cmd = new AsyncRelayCommand(() => Task.CompletedTask, () => allowed);

        Assert.False(cmd.CanExecute(null));
        allowed = true;
        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public async Task CanExecute_FalseWhileRunning_PreventsDoubleClick()
    {
        // _isRunning flag blokuje druhé spuštění během běžícího tasku.
        var gate = new TaskCompletionSource();
        var cmd = new AsyncRelayCommand(() => gate.Task);

        cmd.Execute(null);
        // Po startu, ale před dokončením - running, CanExecute=false.
        Assert.False(cmd.CanExecute(null));

        gate.SetResult();
        await Task.Delay(50);  // dej dispatcheru čas dokončit finally blok

        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public async Task SecondExecute_WhileFirstRunning_IsIgnored()
    {
        var gate = new TaskCompletionSource();
        int invocations = 0;
        var cmd = new AsyncRelayCommand(async () =>
        {
            invocations++;
            await gate.Task;
        });

        cmd.Execute(null);
        cmd.Execute(null);  // by mělo být ignorováno

        Assert.Equal(1, invocations);  // invocations se inkrementuje synchronně na startu handleru

        gate.SetResult();
        await gate.Task;               // úklid: necháme první task dokončit
    }

    [Fact]
    public void Execute_Exception_RoutedToInjectedPopup()
    {
        // Handler hodí synchronně → catch/finally proběhne synchronně, žádný await nepotřeba.
        var popup = Substitute.For<IPopupDisplayer>();
        var boom = new InvalidOperationException("boom");
        var cmd = new AsyncRelayCommand(() => throw boom, canExecute: null, popupDisplayer: popup);

        cmd.Execute(null);

        popup.Received(1).Error(boom);
    }

    [Fact]
    public void Execute_Exception_NullPopup_FallsBackToEDServicesCurrent()
    {
        var popup = Substitute.For<IPopupDisplayer>();
        EDServices.Initialize(ServicesWith(popup));
        try
        {
            var boom = new InvalidOperationException("boom");
            var cmd = new AsyncRelayCommand(() => throw boom);

            cmd.Execute(null);

            popup.Received(1).Error(boom);
        }
        finally
        {
            EDServices.Reset();
        }
    }

    [Fact]
    public void Execute_ExceptionDoesNotLeave_IsRunningTrue()
    {
        // Při výjimce musí finally blok obnovit _isRunning=false, jinak by command zůstal
        // permanentně zablokovaný a uživatel by ho po prvním fail nemohl spustit znovu.
        // Handler hazí synchronně → oba běhy proběhnou synchronně, bez await.
        var popup = Substitute.For<IPopupDisplayer>();
        int invocations = 0;
        var cmd = new AsyncRelayCommand(() =>
        {
            invocations++;
            throw new InvalidOperationException();
        }, canExecute: null, popupDisplayer: popup);

        cmd.Execute(null);
        cmd.Execute(null);

        Assert.Equal(2, invocations);
    }
}

// Generický AsyncRelayCommand<T> je samostatná (duplikovaná) implementace - vlastní pokrytí.
// Ve stejné serializované kolekci, protože jeho Execute catch také fallbackuje na EDServices.Current.
[Collection(SharedStaticStateCollection.Name)]
public class AsyncRelayCommandGenericTests
{
    [Fact]
    public async Task Execute_PassesParameter()
    {
        string? received = null;
        var done = new TaskCompletionSource();
        var cmd = new AsyncRelayCommand<string>(async s =>
        {
            await Task.Yield();
            received = s;
            done.SetResult();
        });

        cmd.Execute("hello");
        await done.Task;   // deterministické místo Task.Delay

        Assert.Equal("hello", received);
    }

    [Fact]
    public void CanExecute_RespectsPredicateWithParameter()
    {
        var cmd = new AsyncRelayCommand<string>(_ => Task.CompletedTask, s => s == "ok");

        Assert.False(cmd.CanExecute("no"));
        Assert.True(cmd.CanExecute("ok"));
    }

    [Fact]
    public void CanExecute_WrongParameterType_False()
    {
        // parameter is T t je false pro 42 (T=string) → predikát se nevolá, CanExecute=false.
        var cmd = new AsyncRelayCommand<string>(_ => Task.CompletedTask, _ => true);

        Assert.False(cmd.CanExecute(42));
    }

    [Fact]
    public async Task CanExecute_FalseWhileRunning()
    {
        // _isRunning blokuje CanExecute po dobu běhu tasku; gate držíme pod kontrolou testu.
        var gate = new TaskCompletionSource();
        var cmd = new AsyncRelayCommand<string>(_ => gate.Task);

        cmd.Execute("x");
        Assert.False(cmd.CanExecute("x"));   // běží → false

        gate.SetResult();
        await Task.Delay(50);                // počkat na finally blok (obnova _isRunning)

        Assert.True(cmd.CanExecute("x"));
    }

    [Fact]
    public void Execute_WrongParameterType_HandlerNotInvoked()
    {
        // parameter is not T t → return před spuštěním handleru (synchronně, bez await).
        var invoked = false;
        var popup = Substitute.For<IPopupDisplayer>();
        var cmd = new AsyncRelayCommand<string>(_ => { invoked = true; return Task.CompletedTask; },
            canExecute: null, popupDisplayer: popup);

        cmd.Execute(42);
        cmd.Execute(null);

        Assert.False(invoked);
        popup.DidNotReceive().Error(Arg.Any<Exception>());
    }

    [Fact]
    public async Task SecondExecute_WhileFirstRunning_IsIgnored()
    {
        var gate = new TaskCompletionSource();
        int invocations = 0;
        var cmd = new AsyncRelayCommand<string>(async _ => { invocations++; await gate.Task; });

        cmd.Execute("a");
        cmd.Execute("b");   // ignorováno - první ještě běží

        Assert.Equal(1, invocations);  // invocations se inkrementuje synchronně na startu handleru

        gate.SetResult();
        await gate.Task;
    }

    [Fact]
    public void Execute_Exception_RoutedToInjectedPopup()
    {
        // Handler hazí synchronně → catch proběhne synchronně, bez await.
        var popup = Substitute.For<IPopupDisplayer>();
        var boom = new InvalidOperationException("boom");
        var cmd = new AsyncRelayCommand<string>(_ => throw boom, canExecute: null, popupDisplayer: popup);

        cmd.Execute("x");

        popup.Received(1).Error(boom);
    }

    [Fact]
    public void Execute_ExceptionResetsIsRunning_AndRoutesToPopup()
    {
        // Zrcadlí non-generický test: po výjimce finally obnoví _isRunning=false (CanExecute zas true)
        // a výjimka jde do IPopupDisplayer. Handler hazí synchronně → oba běhy synchronně.
        var popup = Substitute.For<IPopupDisplayer>();
        var boom = new InvalidOperationException("boom");
        int invocations = 0;
        var cmd = new AsyncRelayCommand<string>(_ =>
        {
            invocations++;
            throw boom;
        }, canExecute: null, popupDisplayer: popup);

        cmd.Execute("x");
        Assert.True(cmd.CanExecute("x"));   // _isRunning zpět na false
        cmd.Execute("x");

        Assert.Equal(2, invocations);
        popup.Received(2).Error(boom);
    }
}
