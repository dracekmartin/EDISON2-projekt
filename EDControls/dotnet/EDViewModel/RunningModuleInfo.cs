namespace EDControls.EDViewModel;

/// <summary>
/// Jeden záznam pro menu "Spuštěné moduly" - sjednocuje legacy child procesy a in-process
/// WPF okna. Activate je delegát: pro WPF okno volá <c>Window.Activate()</c>, pro legacy
/// proces volá Win32 <c>SetForegroundWindow</c> na <c>MainWindowHandle</c>.
/// <see cref="IsEnabled"/> = false značí placeholder položku (např. "Žádné moduly nejsou
/// spuštěné") která se nedá kliknout.
/// </summary>
public record RunningModuleInfo(string DisplayName, Action Activate, bool IsEnabled = true)
{
    /// <summary>Název EXE, jak ho dostal launch (u WPF oken prázdné). Spolu s <see cref="FormFqn"/>
    /// a <see cref="InputParam"/> tvoří identitu modulu - shell si přes ni dohledá cestu agendou,
    /// protože <see cref="DisplayName"/> legacy agendy si formátují samy.</summary>
    public string ExeName { get; init; } = "";

    public string FormFqn { get; init; } = "";

    public string InputParam { get; init; } = "";
}
