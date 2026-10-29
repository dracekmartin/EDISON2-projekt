using System;

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
    private bool _forced;

    /// <summary>
    /// Běží runner serverové aktualizace (<see cref="ManualUpdate"/>), který session shellu odhlásí o chvíli dřív,
    /// než dá signál k zavření. <see cref="ServerSessionEnd"/> proto s hláškou o konci session chvíli počká.
    /// </summary>
    public bool SessionEndExpected { get; set; }

    /// <summary>Napojí suppress hook shellu. Před jeho vznikem není co potlačovat (dialog neexistuje).</summary>
    public void AttachShell(Action suppressCloseConfirm)
    {
        _suppressCloseConfirm = suppressCloseConfirm;
        // Shell vzniklý až po Force (force logout během stavby) WPF při ukončení zavírá taky.
        if (_forced) suppressCloseConfirm();
    }

    public void Force()
    {
        _forced = true;
        _suppressCloseConfirm?.Invoke();
        shutdown();
    }
}
