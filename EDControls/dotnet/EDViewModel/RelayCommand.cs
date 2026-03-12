using System.Windows.Input;

namespace EDControls.EDViewModel;

public class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    // canExecute must not throw — WPF calls it on every UI interaction and exceptions
    // propagate to DispatcherUnhandledException rather than being caught here.
    public bool CanExecute(object? parameter) => canExecute == null || canExecute();
    public void Execute(object? parameter) { try { execute(); } catch (Exception ex) { new PopupDisplayer().Error(ex); } }
}

public class RelayCommand<T>(Action<T> execute, Func<T, bool>? canExecute = null) : ICommand
{
    private readonly Action<T> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    private readonly Func<T, bool>? _canExecute = canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    // canExecute must not throw — WPF calls it on every UI interaction and exceptions
    // propagate to DispatcherUnhandledException rather than being caught here.
    public bool CanExecute(object? parameter) => _canExecute == null || (parameter is T t && _canExecute(t));
    public void Execute(object? parameter) { try { if (parameter is T t) _execute(t); } catch (Exception ex) { new PopupDisplayer().Error(ex); } }
}
