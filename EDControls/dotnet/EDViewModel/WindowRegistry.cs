using System.Windows;

namespace EDControls.EDViewModel;

/// <summary>
/// Central runtime registr mapující string klíč → factory delegát pro WPF okno.
/// Naplňuje composition root (App.xaml.cs) při startupu; agendy si registrují vlastní okna v rámci svého startup hooku.
/// Použito <see cref="WindowNavigator"/> pro <c>Show</c> / <c>ShowDialog</c> (in-process WPF okna).
/// Cross-process spawn (<c>ShowLegacy*</c>) registr nepoužívá — má vlastní mechanismus přes <c>FormToRunGetter</c>.
/// </summary>
public class WindowRegistry
{
    private readonly Dictionary<string, Func<EDWindowInput?, Window>> _factories = new();

    /// <summary>
    /// Registrace okna. Klíč je libovolný string, konvence: jméno window třídy
    /// (typicky <c>nameof(MyWindow)</c>) pro shell okna; pro cross-agenda navigaci
    /// preferovat namespacovaný klíč (např. <c>"MZDY.ZamestnanecEdit"</c>) aby
    /// nedošlo ke kolizi mezi agendami.
    /// </summary>
    public void Register(string key, Func<EDWindowInput?, Window> factory)
    {
        if (_factories.ContainsKey(key))
            throw new InvalidOperationException($"Window key '{key}' je už registrován.");
        _factories[key] = factory;
    }

    internal Window Create(string key, EDWindowInput? input)
    {
        if (!_factories.TryGetValue(key, out var factory))
            throw new InvalidOperationException(
                $"Window key '{key}' není registrován. Zaregistruj přes WindowNavigator.Registry.Register(...) při startupu.");
        return factory(input);
    }
}
