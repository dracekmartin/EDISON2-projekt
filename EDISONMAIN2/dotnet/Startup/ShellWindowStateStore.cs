using System.Windows;
using EDControls;

namespace EDISONMAIN2;

/// <summary>
/// Geometrie hlavního okna v <c>local.json</c>, per server. Záznam serveru schválně sdílí shell
/// i přihlašovací okno: před loginem není session, do které by šlo ukládat (Caché úložiště
/// je per uživatel × IDFa), a obě okna mají naskočit na témž monitoru. Scope je Windows
/// uživatel (%APPDATA%), takže na roamingovém profilu jde záznam i na jinou stanici.
/// </summary>
public class ShellWindowStateStore(ILocalPrefs prefs, string server, int port) : IWindowStateStore
{
#pragma warning disable CS0618 // geometrie starší verze: okno serveru bez vlastního záznamu naskočí tam, kde bylo před aktualizací
    public WindowGeometry Load() =>
        (prefs.ShellWindowBoundsFor(server, port) ?? prefs.ShellWindowBounds) is { } b
            ? new WindowGeometry(b.Left, b.Top, b.Width, b.Height, b.Maximized)
            : WindowGeometry.None;
#pragma warning restore CS0618

    public void Save(Rect bounds, bool maximized)
    {
        prefs.SetShellWindowBounds(server, port, new WindowBoundsPrefs
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = maximized,
        });
        prefs.Save();
    }
}
