using System.Windows;
using EDControls;

namespace EDISONMAIN2;

/// <summary>
/// Geometrie hlavního okna v <c>local.json</c>. Jeden záznam schválně sdílí shell
/// i přihlašovací okno: před loginem není session, do které by šlo ukládat (Caché úložiště
/// je per uživatel × IDFa), a obě okna mají naskočit na témž monitoru. Scope je Windows
/// uživatel (%APPDATA%), takže na roamingovém profilu jde záznam i na jinou stanici.
/// </summary>
public class ShellWindowStateStore(ILocalPrefs prefs) : IWindowStateStore
{
    public WindowGeometry Load() =>
        prefs.ShellWindowBounds is { } b
            ? new WindowGeometry(b.Left, b.Top, b.Width, b.Height, b.Maximized)
            : WindowGeometry.None;

    public void Save(Rect bounds, bool maximized)
    {
        prefs.ShellWindowBounds = new WindowBoundsPrefs
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = maximized,
        };
        prefs.Save();
    }
}
