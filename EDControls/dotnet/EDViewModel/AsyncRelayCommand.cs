using System.Windows.Input;

namespace EDControls.EDViewModel;

/// <summary>
/// Async varianta <see cref="RelayCommand"/>. Napojí <c>async Task</c> handler na
/// <see cref="ICommand"/> a chytá výjimky přes <see cref="IPopupDisplayer"/> místo aby
/// propadly na UI thread jako <c>async void</c>. Během běhu tasku <see cref="CanExecute"/>
/// vrací false — brání double-clicku.
/// </summary>
public class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null, IPopupDisplayer? popupDisplayer = null) : ICommand
{
    private bool _isRunning;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    // canExecute nesmí házet — WPF ho volá při každé UI interakci a výjimka by propadla
    // na DispatcherUnhandledException, ne do našeho catch-u.
    public bool CanExecute(object? parameter) => !_isRunning && (canExecute == null || canExecute());

    // async void je výjimečně OK: ICommand.Execute je void; výjimky chytá try/catch níže.
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
