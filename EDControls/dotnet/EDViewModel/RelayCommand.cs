using System.Windows.Input;

namespace EDControls.EDViewModel;

public class RelayCommand(Action execute, Func<bool>? canExecute = null, IPopupDisplayer? popupDisplayer = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    // canExecute nesmí házet — WPF ho volá při každé UI interakci a výjimka by propadla
    // na DispatcherUnhandledException, ne do našeho catch-u.
    public bool CanExecute(object? parameter) => canExecute == null || canExecute();
    public void Execute(object? parameter) { try { execute(); } catch (Exception ex) { (popupDisplayer ?? new PopupDisplayer()).Error(ex); } }
}

public class RelayCommand<T>(Action<T> execute, Func<T, bool>? canExecute = null, IPopupDisplayer? popupDisplayer = null) : ICommand
{
    private readonly Action<T> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    private readonly Func<T, bool>? _canExecute = canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    // canExecute nesmí házet — WPF ho volá při každé UI interakci a výjimka by propadla
    // na DispatcherUnhandledException, ne do našeho catch-u.
    public bool CanExecute(object? parameter) => _canExecute == null || (parameter is T t && _canExecute(t));
    public void Execute(object? parameter) { try { if (parameter is T t) _execute(t); } catch (Exception ex) { (popupDisplayer ?? new PopupDisplayer()).Error(ex); } }
}
