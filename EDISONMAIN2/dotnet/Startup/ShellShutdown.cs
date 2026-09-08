using System;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro WinForms color picker).
using Application = System.Windows.Application;

namespace EDISONMAIN2;

/// <summary>
/// Vynucené ukončení aplikace, které obejde potvrzovací dialog shellu
/// (<c>EdisonMainWindow.OnShellClosing</c>) - force logout ani version gate uživatel odmítnout nesmí.
/// Vlastní objekt proto, že ukončení se spouští po startu (activity handler, odpočet odhlášení),
/// kdežto <see cref="AppBootstrapper"/> tou dobou už nemá žít.
/// </summary>
public class ShellShutdown(Action shutdown)
{
    private Action? _suppressCloseConfirm;

    /// <summary>Produkční cesta. Lazy lambda - test-only ctor AppBootstrapperu předává null Application.</summary>
    public ShellShutdown(Application application) : this(() => application.Shutdown()) { }

    /// <summary>Napojí suppress hook shellu. Před jeho vznikem není co potlačovat (dialog neexistuje).</summary>
    public void AttachShell(Action suppressCloseConfirm) => _suppressCloseConfirm = suppressCloseConfirm;

    public void Force()
    {
        _suppressCloseConfirm?.Invoke();
        shutdown();
    }
}
