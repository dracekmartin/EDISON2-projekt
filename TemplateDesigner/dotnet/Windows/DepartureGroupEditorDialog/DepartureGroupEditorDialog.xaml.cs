using System.Windows;
using SkiaSharp;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner;

public partial class DepartureGroupEditorDialog : Window
{
    private readonly DepartureGroupEditorDialogViewModel _vm;

    public List<DepartureGroupColumnStyle> ResultGroupColumns => _vm.ResultGroupColumns;

    public DepartureGroupEditorDialog(
        List<SkupinaDnuData> availableGroups,
        List<DepartureGroupColumnStyle> currentStyles)
    {
        _vm = new DepartureGroupEditorDialogViewModel(availableGroups, currentStyles);
        DataContext = _vm;
        InitializeComponent();

        _vm.RequestColorPicker = currentColor => PickColor(currentColor);
        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }

    private string? PickColor(string currentHex)
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
        catch { /* ignore */ }

        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
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
