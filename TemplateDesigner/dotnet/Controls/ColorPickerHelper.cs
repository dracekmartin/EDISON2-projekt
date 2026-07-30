using System.Windows;
using System.Windows.Interop;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Services;

namespace TemplateDesigner.Controls;

/// <summary>
/// Sdílený helper pro zobrazení WinForms ColorDialogu z WPF oken.
/// View concern - nativní dialog vyžaduje Window handle.
///
/// <para>
/// Recent Colors integrace: pokud je nastavena <see cref="Preferences"/> (typicky v
/// App.OnStartup), populuje se `dlg.CustomColors` z <c>Preferences.RecentColors</c>
/// (16-slot grid „Custom colors" v dolní části nativního dialogu). Po úspěšném výběru
/// se barva přidá do recent přes <see cref="TdPreferencesService.AddRecentColorAsync"/>.
/// </para>
/// </summary>
static class ColorPickerHelper
{
    /// <summary>
    /// Globální reference na TdPreferencesService - nastavit jednou v App.OnStartup.
    /// Když null, color picker funguje bez recent colors integrace.
    /// </summary>
    public static TdPreferencesService? Preferences { get; set; }

    /// <summary>
    /// Zobrazí systémový color picker s aktuální barvou. Pokud je <see cref="Preferences"/>
    /// nastavena, předá Recent Colors do „Custom colors" slotu a po výběru přidá novou
    /// barvu do recent (fire-and-forget save).
    /// Vrátí novou barvu jako `"#AARRGGBB"` string, nebo null pokud uživatel zrušil.
    /// </summary>
    /// <remarks>Barva z dat („pozadí = barva linky") se sem záměrně nemotá - zapisuje se do pole
    /// jako <c>{BARVA_LINKY}</c>. Mezikrok s volbou zdroje by přidal klik všem uživatelům kvůli
    /// funkci, která se použije na pár prvcích.</remarks>
    public static string? PickColor(Window owner, string currentHex)
    {
        var dlg = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true
        };

        if (ColorHex.TryParse(currentHex, out var current))
            dlg.Color = System.Drawing.Color.FromArgb(current.Red, current.Green, current.Blue);

        // Populate „Custom colors" grid s recent colors uživatele (max 16 slotů).
        if (Preferences is { RecentColors.Count: > 0 } prefs)
            dlg.CustomColors = HexListToBgrInts(prefs.RecentColors);

        var handle = new WindowInteropHelper(owner).Handle;
        if (dlg.ShowDialog(new Win32Window(handle)) != System.Windows.Forms.DialogResult.OK)
            return null;

        var picked = dlg.Color;
        string hex = $"#FF{picked.R:X2}{picked.G:X2}{picked.B:X2}";

        // Fire-and-forget save - nesmí blokovat color picker zavření.
        _ = Preferences?.AddRecentColorAsync(hex);

        return hex;
    }

    /// <summary>
    /// Konverze hex stringů na BGR int formát očekávaný WinForms ColorDialog.CustomColors.
    /// Pozn.: WinForms používá COLORREF (0x00BBGGRR), ne ARGB.
    /// </summary>
    private static int[] HexListToBgrInts(IReadOnlyList<string> hexColors)
    {
        var result = new int[Math.Min(hexColors.Count, 16)];
        for (int i = 0; i < result.Length; i++)
        {
            var c = ColorHex.Parse(hexColors[i], SkiaSharp.SKColors.White);
            result[i] = (c.Blue << 16) | (c.Green << 8) | c.Red;
        }
        return result;
    }

    private class Win32Window(nint handle) : System.Windows.Forms.IWin32Window
    {
        public nint Handle { get; } = handle;
    }
}
