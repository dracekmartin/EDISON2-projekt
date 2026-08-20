using System.Windows;

namespace EDControls.MiniApps;

/// <summary>
/// Lightweight mini-app pro čistě obsahové panely (nástěnka, statický přehled…) - bez vlastní
/// VM/View/DataTemplate třídy. Vystaví <see cref="Title"/> a továrnu na obsah; renderuje se
/// sdílenou <see cref="SimpleMiniAppView"/>.
/// </summary>
public sealed class SimpleMiniAppViewModel : EDMiniAppViewModel
{
    /// <param name="contentFactory">
    /// Vyrobí obsah panelu. Drží se jako továrna (ne živý element), aby šla mini-app otevřít
    /// vícekrát a panel dockovat/odtrhávat bez re-parentingu jedné instance.
    /// </param>
    public SimpleMiniAppViewModel(string title, Func<FrameworkElement> contentFactory)
    {
        Title = title;
        ContentFactory = contentFactory;
    }

    /// <summary>DI/test overload - předá vlastní <c>EDServices</c> místo <c>EDServices.Current</c>.</summary>
    public SimpleMiniAppViewModel(EDViewModel.EDServices services, string title, Func<FrameworkElement> contentFactory)
        : base(services)
    {
        Title = title;
        ContentFactory = contentFactory;
    }

    public override string Title { get; }

    public Func<FrameworkElement> ContentFactory { get; }

    private IDisposable? _disposableContent;

    /// <summary>
    /// View předá vytvořený obsah, ať ho při zavření panelu můžeme uklidit (je-li IDisposable -
    /// např. WebView2 drží browser proces). Nelze vázat na <c>Unloaded</c>, to padá i při
    /// dock/float re-parentingu (AvalonDock přesouvá tutéž instanci).
    /// </summary>
    internal void OnContentCreated(FrameworkElement content) => _disposableContent = content as IDisposable;

    public override void OnClosed() => _disposableContent?.Dispose();
}
