using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using EDControls.EDViewModel;

namespace EDControls;

/// <summary>
/// Persistence stavu WPF okna (pozice, velikost, Maximized) přes legacy
/// <c>ALVA.LIBRARY.Form.SaveParams</c> / <c>LoadParams</c>. Storage je per-MLP × IDFa
/// (server čte IDFa ze sys kontextu MLRest).
/// </summary>
/// <remarks>
/// Aktivace: <see cref="EDViewModel.PersistedFormId"/> override v subclass VM. Konvence
/// formID: <c>"EDISON2.&lt;WindowClassName&gt;"</c> — prefix odděluje od legacy numeric IDs
/// z <c>EDISFormEnum</c>.
/// </remarks>
public static class WindowStatePersistence
{
    public static void Attach(Window window, string formId)
    {
        // Volat před Show(): PresentationSource je null, hookne SourceInitialized event.
        // SourceInitialized fire-uje synchronně uvnitř Show() ještě před prvním paintem,
        // takže restore se aplikuje včas a nenastane flicker.
        if (PresentationSource.FromVisual(window) is null)
            window.SourceInitialized += (_, _) => TryRestore(window, formId);
        else
            TryRestore(window, formId);

        window.Closing += (_, _) => TrySave(window, formId);
    }

    private static void TryRestore(Window window, string formId)
    {
        try
        {
            var mlRest = EDServices.Current.MLRest;
            if (mlRest is null) return;
            var del = mlRest.Delimiter;

            var input = new object[]
            {
                $"formID{del}{formId}",
                $"ciselnik{del}state",
            };
            var output = mlRest.QueryOrThrow("ALVA.LIBRARY.Form", "LoadParams", input);
            ApplyToWindow(window, ParseOutput(output, del));
        }
        catch
        {
            // Best-effort restore — chyba neblokuje start okna, použijí se XAML defaults.
        }
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

            var input = new object[]
            {
                $"formID{del}{formId}",
                $"clear{del}1",
                $"param{del}location{del}{bounds.Left:0};{bounds.Top:0}",
                $"param{del}size{del}{bounds.Width:0};{bounds.Height:0}",
                $"param{del}state{del}{stateInt}",
            };
            mlRest.QueryOrThrow("ALVA.LIBRARY.Form", "SaveParams", input);
        }
        catch
        {
            // Best-effort save — neblokujeme zavírání okna.
        }
    }

    private static Dictionary<string, string> ParseOutput(object[] output, char del)
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

    private static void ApplyToWindow(Window window, Dictionary<string, string> prefs)
    {
        // Pořadí: rozměry → pozice → stav. Maximize as last, jinak by se velikost nezachovala
        // pro un-maximize krok.
        if (prefs.TryGetValue("size", out var size) && TryParsePair(size, out var w, out var h))
        {
            if (w > 0) window.Width = w;
            if (h > 0) window.Height = h;
        }
        if (prefs.TryGetValue("location", out var loc) && TryParsePair(loc, out var x, out var y))
        {
            window.Left = x;
            window.Top = y;
        }
        if (prefs.TryGetValue("state", out var state))
            window.WindowState = state == "1" ? WindowState.Maximized : WindowState.Normal;
    }

    private static bool TryParsePair(string s, out double a, out double b)
    {
        a = b = 0;
        var parts = s.Split(';');
        return parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out a)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out b);
    }
}
