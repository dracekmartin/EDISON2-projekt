using System.Windows;

namespace AvalonDock.Controls;

/// <summary>
/// Dvojník okna docku (overlay / plovoucí okno) pro testy vlastnictví oken. Namespace je tady
/// podstatný, ne typ — predikát <c>FrameworkWindows</c> rozhoduje podle namespace, protože
/// EDControls na AvalonDock nedosáhne. Proto je i jméno namespace shodné se skutečným.
/// </summary>
internal sealed class FakeDockWindow : Window;
