using System.Windows.Media;

namespace EDControls.MiniApps;

/// <summary>
/// Báze pro mini-app ViewModely. Mini-app je hostovaná v docku (AvalonDock), ne ve vlastním
/// okně — proto <see cref="PersistedFormId"/> vrací null: geometrii drží layout docku, ne
/// <c>WindowStatePersistence</c>. Lifecycle panelu řídí <c>MiniAppManager</c> přes
/// <see cref="OnOpened"/> / <see cref="OnClosed"/>.
/// </summary>
public abstract class EDMiniAppViewModel : EDViewModel.EDViewModel
{
    protected EDMiniAppViewModel() { }

    protected EDMiniAppViewModel(EDViewModel.EDServices services) : base(services) { }

    protected override string? PersistedFormId => null;

    /// <summary>Titulek panelu v docku (tab header).</summary>
    public abstract string Title { get; }

    /// <summary>Ikona panelu. Volitelné.</summary>
    public virtual ImageSource? Icon => null;

    /// <summary>
    /// Mini-app žádá o své zavření v docku. <c>MiniAppManager</c> se přihlásí na base
    /// <c>RequestClose</c> event.
    /// </summary>
    protected void CloseSelf() => CloseDialog();

    /// <summary>Volá <c>MiniAppManager</c> po namountování panelu do docku.</summary>
    public virtual void OnOpened() { }

    /// <summary>Volá <c>MiniAppManager</c> při zavření panelu, přepnutí firmy nebo zavření shellu.</summary>
    public virtual void OnClosed() { }
}
