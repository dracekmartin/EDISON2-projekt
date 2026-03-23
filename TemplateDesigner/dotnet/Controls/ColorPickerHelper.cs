using System.Windows;
using System.Windows.Interop;
using SkiaSharp;

namespace TemplateDesigner.Controls;

/// <summary>
/// Sdílený helper pro zobrazení WinForms ColorDialogu z WPF oken.
/// View concern — nativní dialog vyžaduje Window handle.
/// </summary>
static class ColorPickerHelper
{
    /// <summary>
    /// Zobrazí systémový color picker s aktuální barvou.
    /// Vrátí novou barvu jako #AARRGGBB string, nebo null pokud uživatel zrušil.
    /// </summary>
    public static string? PickColor(Window owner, string currentHex)
    {
        var dlg = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(currentHex))
            {
                var c = SKColor.Parse(currentHex);
                dlg.Color = System.Drawing.Color.FromArgb(c.Red, c.Green, c.Blue);
            }
        }
        catch { /* ignore parse errors */ }

        var handle = new WindowInteropHelper(owner).Handle;
        if (dlg.ShowDialog(new Win32Window(handle)) == System.Windows.Forms.DialogResult.OK)
        {
            var c = dlg.Color;
            return $"#FF{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        return null;
    }

    private class Win32Window(nint handle) : System.Windows.Forms.IWin32Window
    {
        public nint Handle { get; } = handle;
    }
}
