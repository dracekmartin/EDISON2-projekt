using System;
using System.Windows;
using System.Windows.Interop;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace EDISONMAIN2;

/// <summary>
/// Picker pro EDInputForm pole Color - prostý wrapper nad WinForms ColorDialog. EDISONMAIN2
/// nemá recent colors infrastrukturu (na rozdíl od TD), takže baseline dialog stačí.
/// Registruje se v <see cref="AppBootstrapper"/> jako <c>EDInputForm.PickColorImpl</c>.
/// </summary>
internal static class WinFormsColorPicker
{
    internal static string? Pick(Window owner, string currentHex)
    {
        var dlg = new Forms.ColorDialog { FullOpen = true, AnyColor = true };

        if (TryParseHex(currentHex, out byte r, out byte g, out byte b))
            dlg.Color = Drawing.Color.FromArgb(r, g, b);

        var handle = new WindowInteropHelper(owner).Handle;
        if (dlg.ShowDialog(new Win32Window(handle)) != Forms.DialogResult.OK)
            return null;

        var c = dlg.Color;
        return $"#FF{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    /// <summary>#AARRGGBB / #RRGGBB / bez '#'; alfa se zahazuje. Nevalidní vstup vrátí false
    /// (dialog pak zůstane na své default barvě).</summary>
    internal static bool TryParseHex(string? hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        string h = (hex ?? "").TrimStart('#');
        try
        {
            if (h.Length == 8) h = h.Substring(2);
            if (h.Length != 6) return false;
            r = Convert.ToByte(h.Substring(0, 2), 16);
            g = Convert.ToByte(h.Substring(2, 2), 16);
            b = Convert.ToByte(h.Substring(4, 2), 16);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private class Win32Window(nint handle) : Forms.IWin32Window
    {
        public nint Handle { get; } = handle;
    }
}
