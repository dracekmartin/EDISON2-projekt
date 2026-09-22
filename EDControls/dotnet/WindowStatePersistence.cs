using System.Windows;

namespace EDControls;

/// <summary>
/// Persistence stavu WPF okna (pozice, velikost, Maximized). Kam se ukládá, drží
/// <see cref="IWindowStateStore"/> - default <see cref="CacheWindowStateStore"/> (per uživatel ×
/// IDFa na serveru), shell a přihlašovací okno sdílejí jeden záznam v local.json.
/// </summary>
public static class WindowStatePersistence
{
    // Kolik z titulku musí zůstat na ploše, aby šlo okno uchopit myší (DIP).
    private const double MinVisibleCaptionWidth = 120;
    private const double CaptionStripHeight = 32;

    /// <summary>Persistence do Caché úložiště pod daným formID.</summary>
    public static void Attach(Window window, string formId) =>
        Attach(window, new CacheWindowStateStore(formId));

    public static void Attach(Window window, IWindowStateStore store)
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

            window.SourceInitialized += (_, _) => TryRestore(window, store, xamlState);
        }
        else
            TryRestore(window, store, window.WindowState);

        // Guard na e.Cancel: WPF pustí i zbylé Closing handlery, když jiný zavření zrušil
        // (potvrzovací dialog shellu) - bez guardu by každé odmítnuté zavření platilo Save.
        window.Closing += (_, e) => { if (!e.Cancel) Save(window, store); };
    }

    /// <summary>
    /// Uloží aktuální geometrii okna. <see cref="Attach"/> ji volá sám při zavírání; ručně
    /// volej, když okno musí geometrii předat dřív - login okno se zavírá až po vykreslení
    /// shellu, který si ji ale čte v témž běhu.
    /// </summary>
    public static void Save(Window window, IWindowStateStore store)
    {
        try
        {
            // RestoreBounds drží "normal" rozměry i když je okno aktuálně Maximized.
            var bounds = window.WindowState == WindowState.Normal
                ? new Rect(window.Left, window.Top, window.Width, window.Height)
                : window.RestoreBounds;

            store.Save(bounds, window.WindowState == WindowState.Maximized);
        }
        catch
        {
            // Best-effort save - neblokujeme zavírání okna.
        }
    }

    private static void TryRestore(Window window, IWindowStateStore store, WindowState xamlState)
    {
        var geometry = WindowGeometry.None;
        try { geometry = store.Load(); }
        catch
        {
            // Best-effort restore - chyba neblokuje start okna, použijí se XAML defaults.
        }

        // I při selhání se musí projít ApplyToWindow: vrací stav sražený v Attach.
        try { ApplyToWindow(window, geometry, xamlState); }
        catch { }
    }

    internal static void ApplyToWindow(Window window, WindowGeometry geometry, WindowState xamlState)
    {
        // Nic uloženého - vrátíme stav, který chtěl XAML (Attach ho kvůli HWND srazil na Normal).
        if (geometry.IsEmpty)
        {
            window.WindowState = xamlState;
            return;
        }

        // Pojistka pro okno, které už je zobrazené (Attach volaný po Show): maximalizované okno
        // Win32 zvětšit nenechá. U okna před Show() je to no-op, tam stav srazil už Attach.
        if (window.WindowState != WindowState.Normal) window.WindowState = WindowState.Normal;

        // Pořadí: rozměry → pozice → stav. Maximize as last, jinak by se velikost nezachovala
        // pro un-maximize krok.
        if (geometry.Width is > 0) window.Width = geometry.Width.Value;
        if (geometry.Height is > 0) window.Height = geometry.Height.Value;

        if (geometry.Left is { } x && geometry.Top is { } y && IsLocationReachable(x, y, VirtualScreen()))
        {
            window.Left = x;
            window.Top = y;
        }

        // Chybějící uložený stav nesmí okno nechat odmaximalizované - vrátíme stav z XAML.
        window.WindowState = geometry.Maximized switch
        {
            true => WindowState.Maximized,
            false => WindowState.Normal,
            null => xamlState,
        };
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
        // NaN projde parsováním jako platné číslo (NumberStyles.Float zná symbol NaN).
        if (!double.IsFinite(left) || !double.IsFinite(top)) return false;

        return virtualScreen.Contains(new Rect(left, top, MinVisibleCaptionWidth, CaptionStripHeight));
    }
}
