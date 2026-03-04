using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace EDControls.EDViewModel;

public class EDViewModel : INotifyPropertyChanged
{
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
