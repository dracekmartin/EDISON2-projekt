using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using EDControls.EDViewModel;

namespace EDControls;

/// <summary>
/// Persistence stavu WPF okna (pozice, velikost, Maximized) přes existující Caché endpoint
/// <c>ALVA.LIBRARY.Form.SaveParams</c> / <c>LoadParams</c>. Storage je per-MLP × IDFa
/// (server čte IDFa ze sys kontextu MLRest). Wire je <c>object[]</c>, ne CVar - legacy endpoint
/// posílá IN i OUT jako číslované řádky <c>"klíč{del}hodnota"</c>.
/// </summary>
/// <remarks>
/// FormID se default odvozuje z názvu VM (viz <see cref="EDViewModel.PersistedFormId"/>) a
/// dostává prefix <c>"EDISON2."</c> - odděluje od starých numerických IDs z <c>EDISFormEnum</c>
/// (tam VB moduly ukládají vlastní state).
/// </remarks>
public static class WindowStatePersistence
{
    // Kolik z titulku musí zůstat na ploše, aby šlo okno uchopit myší (DIP).
    private const double MinVisibleCaptionWidth = 120;
    private const double CaptionStripHeight = 32;

    public static void Attach(Window window, string formId)
    {
        // Před Show(): PresentationSource je null, hookne se SourceInitialized event,
        // který se synchronně vyvolá uvnitř Show() ještě před prvním paintem - restore
        // dorazí včas a nenastane flicker z XAML defaults na uložený stav.
        if (PresentationSource.FromVisual(window) is null)
        {
            // Okno s `WindowState="Maximized"` v XAML dostane HWND rovnou se stylem WS_MAXIMIZE
            // a takové okno Win32 odmítá zvětšit či zmenšit - uloženou velikost si zapíše jen
            // do restore bounds a WPF si pak vlastní Width/Height přepíše zpátky podle HWND.
            // (Změřeno: srazit stav na Normal až v SourceInitialized nestačí, styl tam
            // nezmizí.) Proto se stav sráží tady, dokud HWND ještě neexistuje; zpátky ho
            // nasadí restore, až po nastavení pozice, takže maximalizace míří na správný monitor.
            var xamlState = window.WindowState;
            if (xamlState != WindowState.Normal) window.WindowState = WindowState.Normal;

            window.SourceInitialized += (_, _) => TryRestore(window, formId, xamlState);
        }
        else
            TryRestore(window, formId, window.WindowState);

        // Guard na e.Cancel: WPF pustí i zbylé Closing handlery, když jiný zavření zrušil
        // (potvrzovací dialog shellu) - bez guardu by každé odmítnuté zavření platilo SaveParams.
        window.Closing += (_, e) => { if (!e.Cancel) TrySave(window, formId); };
    }

    private static void TryRestore(Window window, string formId, WindowState xamlState)
    {
        var prefs = new Dictionary<string, string>();
        try
        {
            var mlRest = EDServices.Current.MLRest;
            if (mlRest is not null)
            {
                var del = mlRest.Delimiter;

                var input = new object[]
                {
                    $"formID{del}{formId}",
                    $"ciselnik{del}state",
                };
                var output = mlRest.QueryOrThrow("ALVA.LIBRARY.Form", "LoadParams", input);
                prefs = ParseOutput(output, del);
            }
        }
        catch
        {
            // Best-effort restore - chyba neblokuje start okna, použijí se XAML defaults.
        }

        // I při selhání se musí projít ApplyToWindow: vrací stav sražený v Attach.
        try { ApplyToWindow(window, prefs, xamlState); }
        catch { }
    }

    private static void TrySave(Window window, string formId)
    {
        try
        {
            var mlRest = EDServices.Current.MLRest;
            if (mlRest is null) return;
            var del = mlRest.Delimiter;

            // RestoreBounds drží "normal" rozměry i když je okno aktuálně Maximized.
            var bounds = window.WindowState == WindowState.Normal
                ? new Rect(window.Left, window.Top, window.Width, window.Height)
                : window.RestoreBounds;
            var stateInt = window.WindowState == WindowState.Maximized ? 1 : 0;

            mlRest.QueryOrThrow("ALVA.LIBRARY.Form", "SaveParams", BuildSaveParams(formId, bounds, stateInt, del));
        }
        catch
        {
            // Best-effort save - neblokujeme zavírání okna.
        }
    }

    internal static object[] BuildSaveParams(string formId, Rect bounds, int stateInt, char del) =>
    [
        $"formID{del}{formId}",
        $"clear{del}1",
        $"param{del}location{del}{bounds.Left:0};{bounds.Top:0}",
        $"param{del}size{del}{bounds.Width:0};{bounds.Height:0}",
        $"param{del}state{del}{stateInt}",
    ];

    internal static Dictionary<string, string> ParseOutput(object[] output, char del)
    {
        var dict = new Dictionary<string, string>();
        for (int i = 1; i < output.Length; i++)
        {
            var s = output[i]?.ToString() ?? "";
            var parts = s.Split(del);
            if (parts.Length >= 3 && parts[0] == "param")
                dict[parts[1]] = parts[2];
        }
        return dict;
    }

    internal static void ApplyToWindow(Window window, Dictionary<string, string> prefs, WindowState xamlState)
    {
        // Nic uloženého - vrátíme stav, který chtěl XAML (Attach ho kvůli HWND srazil na Normal).
        if (prefs.Count == 0)
        {
            window.WindowState = xamlState;
            return;
        }

        // Pojistka pro okno, které už je zobrazené (Attach volaný po Show): maximalizované okno
        // Win32 zvětšit nenechá. U okna před Show() je to no-op, tam stav srazil už Attach.
        if (window.WindowState != WindowState.Normal) window.WindowState = WindowState.Normal;

        // Pořadí: rozměry → pozice → stav. Maximize as last, jinak by se velikost nezachovala
        // pro un-maximize krok.
        if (prefs.TryGetValue("size", out var size) && TryParsePair(size, out var w, out var h))
        {
            if (w > 0) window.Width = w;
            if (h > 0) window.Height = h;
        }
        if (prefs.TryGetValue("location", out var loc) && TryParsePair(loc, out var x, out var y)
            && IsLocationReachable(x, y, VirtualScreen()))
        {
            window.Left = x;
            window.Top = y;
        }

        // Chybějící klíč `state` nesmí okno nechat odmaximalizované - vrátíme stav z XAML.
        window.WindowState = prefs.TryGetValue("state", out var state)
            ? (state == "1" ? WindowState.Maximized : WindowState.Normal)
            : xamlState;
    }

    private static Rect VirtualScreen() => new(
        SystemParameters.VirtualScreenLeft,
        SystemParameters.VirtualScreenTop,
        SystemParameters.VirtualScreenWidth,
        SystemParameters.VirtualScreenHeight);

    /// <summary>
    /// Leží uložený levý horní roh na dnešní ploše? Bez téhle kontroly vyjede okno uložené
    /// na později odpojeném monitoru mimo obrazovku a uživatel se k němu nedostane.
    /// </summary>
    /// <remarks>
    /// Testuje se pruh titulku od levého horního rohu, ne celé okno - velikost tu ještě nemusí
    /// být známá (<c>Width</c>/<c>Height</c> je NaN, dokud ji někdo nenastaví). Plocha je jen
    /// obálka všech monitorů, takže mrtvý kout mezi monitory různé výšky projde; na odpojený
    /// monitor to nicméně stačí.
    /// </remarks>
    internal static bool IsLocationReachable(double left, double top, Rect virtualScreen)
    {
        // NaN projde TryParsePair jako platné číslo (NumberStyles.Float zná symbol NaN).
        if (!double.IsFinite(left) || !double.IsFinite(top)) return false;

        return virtualScreen.Contains(new Rect(left, top, MinVisibleCaptionWidth, CaptionStripHeight));
    }

    internal static bool TryParsePair(string s, out double a, out double b)
    {
        a = b = 0;
        var parts = s.Split(';');
        return parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out a)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out b);
    }
}
