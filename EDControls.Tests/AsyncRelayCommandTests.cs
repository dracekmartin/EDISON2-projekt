using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

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
        var cmd = new AsyncRelayCommand(async () =>
        {
            await Task.Delay(10);
            invoked = true;
        });

        cmd.Execute(null);
        await Task.Delay(50);

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
        // Po startu, ale před dokončením — running, CanExecute=false.
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

        gate.SetResult();
        await Task.Delay(50);

        Assert.Equal(1, invocations);
    }

    [Fact]
    public async Task Execute_Exception_RoutedToInjectedPopup()
    {
        var popup = Substitute.For<IPopupDisplayer>();
        var boom = new InvalidOperationException("boom");
        var cmd = new AsyncRelayCommand(() => throw boom, canExecute: null, popupDisplayer: popup);

        cmd.Execute(null);
        await Task.Delay(50);

        popup.Received(1).Error(boom);
    }

    [Fact]
    public async Task Execute_Exception_NullPopup_FallsBackToEDServicesCurrent()
    {
        var popup = Substitute.For<IPopupDisplayer>();
        EDServices.Initialize(ServicesWith(popup));
        try
        {
            var boom = new InvalidOperationException("boom");
            var cmd = new AsyncRelayCommand(() => throw boom);

            cmd.Execute(null);
            await Task.Delay(50);

            popup.Received(1).Error(boom);
        }
        finally
        {
            EDServices.Reset();
        }
    }

    [Fact]
    public async Task Execute_ExceptionDoesNotLeave_IsRunningTrue()
    {
        // Při výjimce musí finally blok obnovit _isRunning=false, jinak by command zůstal
        // permanentně zablokovaný a uživatel by ho po prvním fail nemohl spustit znovu.
        var popup = Substitute.For<IPopupDisplayer>();
        int invocations = 0;
        var cmd = new AsyncRelayCommand(() =>
        {
            invocations++;
            throw new InvalidOperationException();
        }, canExecute: null, popupDisplayer: popup);

        cmd.Execute(null);
        await Task.Delay(50);
        cmd.Execute(null);
        await Task.Delay(50);

        Assert.Equal(2, invocations);
    }
}
