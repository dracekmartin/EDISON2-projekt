using System.Windows.Input;

namespace EDControls.EDViewModel;

/// <summary>
/// Async equivalent of <see cref="RelayCommand"/>. Wires an <c>async Task</c> handler
/// to an <see cref="ICommand"/>, catching exceptions through <see cref="IPopupDisplayer"/>
/// instead of leaking them to the UI thread as fire-and-forget <c>async void</c>.
/// <see cref="CanExecute"/> returns false while the task is running — prevents double-click.
/// </summary>
public class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null, IPopupDisplayer? popupDisplayer = null) : ICommand
{
    private bool _isRunning;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    // canExecute must not throw — WPF calls it on every UI interaction and exceptions
    // propagate to DispatcherUnhandledException rather than being caught here.
    public bool CanExecute(object? parameter) => !_isRunning && (canExecute == null || canExecute());

    // async void is the one supported case: ICommand.Execute is void. Exceptions are
    // guaranteed caught via try/catch and routed to IPopupDisplayer.
    public async void Execute(object? parameter)
    {
        if (_isRunning) return;
        _isRunning = true;
        CommandManager.InvalidateRequerySuggested();
        try { await execute(); }
        catch (Exception ex) { (popupDisplayer ?? new PopupDisplayer()).Error(ex); }
        finally
        {
            _isRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
