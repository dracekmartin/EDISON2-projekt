using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ALWA.SYS;

namespace EDControls.EDViewModel;

public class EDViewModel : INotifyPropertyChanged
{
    private static MLRest? _mlRest;
    public MLRest? MLRest { get => _mlRest; protected set => _mlRest = value; }

    /// <summary>
    /// Navigator for opening other windows. Use WindowNavigator.Show(...) or ShowDialog(...)
    /// from ViewModel subclasses.
    /// </summary>
    private static readonly IWindowNavigator _windowNavigator = new ReflectionWindowNavigator();
    protected static IWindowNavigator WindowNavigator => _windowNavigator;

    /// <summary>
    /// Service for showing popups. Always targets the currently active window as owner.
    /// Use PopupDisplayer.Confirm(...) or PopupDisplayer.Info(...) from ViewModel logic.
    /// </summary>
    private static readonly IPopupDisplayer _popupDisplayer = new PopupDisplayer();
    public IPopupDisplayer PopupDisplayer => _popupDisplayer;

    /// <summary>
    /// When true, EDFooter shows a confirmation dialog before closing on Storno/Zavřít.
    /// Set to true in the ViewModel constructor for windows with unsaved-data risk.
    /// </summary>
    public bool ConfirmOnCancel { get; protected set; }

    private bool _isActionInProgress;
    public bool IsActionInProgress
    {
        get => _isActionInProgress;
        private set => Set(ref _isActionInProgress, value);
    }

    /// <summary>
    /// Sets IsActionInProgress = true and returns a scope that resets it on disposal.
    /// Usage: using var _ = ActionInProgress();
    /// </summary>
    public IDisposable ActionInProgress()
    {
        IsActionInProgress = true;
        return new ActionScope(this);
    }

    private sealed class ActionScope(EDViewModel vm) : IDisposable
    {
        public void Dispose() => vm.IsActionInProgress = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Fired by the ViewModel to request the window to close.
    /// true = OK/confirmed, false = cancelled, null = just close.
    /// EDWindow subscribes to this automatically when it detects EDViewModel as DataContext.
    /// </summary>
    public event Action<bool?>? RequestClose;

    /// <summary>
    /// Bound to the Ok button in EDWindow (FooterMode = OkCancel).
    /// Override OnOk() to add validation or custom logic before closing.
    /// </summary>
    public ICommand OkCommand { get; }

    public EDViewModel()
    {
        OkCommand = new RelayCommand(OnOk);
    }

    /// <summary>
    /// Called when the Ok button is clicked. Override to add validation.
    /// Call base.OnOk() (or CloseDialog(true)) to confirm and close.
    /// </summary>
    protected virtual void OnOk() => CloseDialog(true);

    /// <summary>
    /// Requests the window to close with the given dialog result.
    /// </summary>
    protected void CloseDialog(bool? result = null) => RequestClose?.Invoke(result);

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(property);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
