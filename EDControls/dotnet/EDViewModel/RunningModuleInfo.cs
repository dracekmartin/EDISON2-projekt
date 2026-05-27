namespace EDControls.EDViewModel;

/// <summary>
/// Jeden záznam pro menu "Spuštěné moduly" — sjednocuje legacy child procesy a in-process
/// WPF okna. Activate je delegát: pro WPF okno volá <c>Window.Activate()</c>, pro legacy
/// proces volá Win32 <c>SetForegroundWindow</c> na <c>MainWindowHandle</c>.
/// <see cref="IsEnabled"/> = false značí placeholder položku (např. "Žádné moduly nejsou
/// spuštěné") která se nedá kliknout.
/// </summary>
public record RunningModuleInfo(string DisplayName, Action Activate, bool IsEnabled = true);
