using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TemplateDesigner.Controls;
using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner;

public partial class HighlightRulesEditorDialog : Window
{
    private readonly HighlightRulesEditorDialogViewModel _vm;

    /// <summary>Výsledná pravidla po OK — pass-through z VM.</summary>
    public List<HighlightRule> ResultRules => _vm.ResultRules;

    public HighlightRulesEditorDialog(IReadOnlyList<HighlightRule>? rules)
    {
        _vm = new HighlightRulesEditorDialogViewModel(rules);
        DataContext = _vm;
        InitializeComponent();

        _vm.RequestColorPicker = hex => ColorPickerHelper.PickColor(this, hex);
        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }
}

/// <summary>Hex <c>#AARRGGBB</c> → <see cref="Brush"/> pro swatch. Prázdné/neplatné = průhledné.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try { return new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)); }
            catch { /* neplatný hex → průhledné */ }
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
