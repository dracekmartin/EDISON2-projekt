using System.Windows;
using System.Windows.Controls;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Controls;

public partial class FormatToolbar : UserControl
{
    private Action<IDesignerCommand>? _execute;
    private Action?                   _invalidate;
    private bool                      _suppress;

    private BoolPropertyDescriptor? _bold, _italic, _underline, _strikethrough;
    private EnumPropertyDescriptor? _hAlign, _vAlign;

    public FormatToolbar() => InitializeComponent();

    public void SetContext(Action<IDesignerCommand> execute, Action invalidate)
    {
        _execute    = execute;
        _invalidate = invalidate;
    }

    /// <summary>
    /// Nastaví stav toolbaru podle deskriptorů. Null nebo prázdný seznam = skryj toolbar.
    /// </summary>
    public void ShowDescriptors(IReadOnlyList<PropertyDescriptor>? descriptors)
    {
        _suppress = true;
        try
        {
            _bold = _italic = _underline = _strikethrough = null;
            _hAlign = _vAlign = null;

            if (descriptors is not null)
            {
                foreach (var d in descriptors)
                {
                    if (d is BoolPropertyDescriptor b && b.Group == "Font")
                        switch (b.Label)
                        {
                            case "Tučné":       _bold          = b; break;
                            case "Kurzíva":     _italic        = b; break;
                            case "Podtržení":   _underline     = b; break;
                            case "Přeškrtnutí": _strikethrough = b; break;
                        }
                    else if (d is EnumPropertyDescriptor e && e.Group == "Zarovnání")
                        switch (e.Label)
                        {
                            case "Vodorovně": _hAlign = e; break;
                            case "Svisle":    _vAlign = e; break;
                        }
                }
            }

            bool hasText = _bold is not null || _hAlign is not null || _vAlign is not null;
            RootBorder.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
            if (!hasText) return;

            BtnBold.IsChecked          = _bold          is not null && _bold.Get();
            BtnItalic.IsChecked        = _italic        is not null && _italic.Get();
            BtnUnderline.IsChecked     = _underline     is not null && _underline.Get();
            BtnStrikethrough.IsChecked = _strikethrough is not null && _strikethrough.Get();
            BtnBold.IsEnabled          = _bold          is not null;
            BtnItalic.IsEnabled        = _italic        is not null;
            BtnUnderline.IsEnabled     = _underline     is not null;
            BtnStrikethrough.IsEnabled = _strikethrough is not null;

            if (_hAlign is not null)
            {
                var v = _hAlign.Get();
                BtnAlignLeft.IsChecked    = v is TextHorizontalAlignment.Left;
                BtnAlignCenter.IsChecked  = v is TextHorizontalAlignment.Center;
                BtnAlignRight.IsChecked   = v is TextHorizontalAlignment.Right;
                BtnAlignJustify.IsChecked = v is TextHorizontalAlignment.Justify;
            }
            BtnAlignLeft.IsEnabled    =
            BtnAlignCenter.IsEnabled  =
            BtnAlignRight.IsEnabled   =
            BtnAlignJustify.IsEnabled = _hAlign is not null;

            if (_vAlign is not null)
            {
                var v = _vAlign.Get();
                BtnVAlignTop.IsChecked    = v is TextVerticalAlignment.Top;
                BtnVAlignMiddle.IsChecked = v is TextVerticalAlignment.Middle;
                BtnVAlignBottom.IsChecked = v is TextVerticalAlignment.Bottom;
            }
            BtnVAlignTop.IsEnabled    =
            BtnVAlignMiddle.IsEnabled =
            BtnVAlignBottom.IsEnabled = _vAlign is not null;
        }
        finally
        {
            _suppress = false;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ToggleBool(BoolPropertyDescriptor? desc)
    {
        if (_suppress || desc is null) return;
        bool oldVal = desc.Get();
        bool newVal = !oldVal;
        _execute?.Invoke(new SetPropertyCommand(desc.Label, oldVal, newVal,
            v => { desc.Set((bool)v!); _invalidate?.Invoke(); }));
    }

    private void SetEnum(EnumPropertyDescriptor? desc, object value)
    {
        if (_suppress || desc is null) return;
        var oldVal = desc.Get();
        if (Equals(oldVal, value)) return;
        _execute?.Invoke(new SetPropertyCommand(desc.Label, oldVal, value,
            v => { desc.Set(v!); _invalidate?.Invoke(); }));
    }

    // ── Click handlery ────────────────────────────────────────────────────────

    private void BtnBold_Click(object sender, RoutedEventArgs e)          => ToggleBool(_bold);
    private void BtnItalic_Click(object sender, RoutedEventArgs e)        => ToggleBool(_italic);
    private void BtnUnderline_Click(object sender, RoutedEventArgs e)     => ToggleBool(_underline);
    private void BtnStrikethrough_Click(object sender, RoutedEventArgs e) => ToggleBool(_strikethrough);

    private void BtnAlignLeft_Click(object sender, RoutedEventArgs e)    => SetEnum(_hAlign, TextHorizontalAlignment.Left);
    private void BtnAlignCenter_Click(object sender, RoutedEventArgs e)  => SetEnum(_hAlign, TextHorizontalAlignment.Center);
    private void BtnAlignRight_Click(object sender, RoutedEventArgs e)   => SetEnum(_hAlign, TextHorizontalAlignment.Right);
    private void BtnAlignJustify_Click(object sender, RoutedEventArgs e) => SetEnum(_hAlign, TextHorizontalAlignment.Justify);

    private void BtnVAlignTop_Click(object sender, RoutedEventArgs e)    => SetEnum(_vAlign, TextVerticalAlignment.Top);
    private void BtnVAlignMiddle_Click(object sender, RoutedEventArgs e) => SetEnum(_vAlign, TextVerticalAlignment.Middle);
    private void BtnVAlignBottom_Click(object sender, RoutedEventArgs e) => SetEnum(_vAlign, TextVerticalAlignment.Bottom);
}
