using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Controls;

/// <summary>
/// Sdílený editor TextStyle zóny (záhlaví NEBO data) pro všechny dialogy sloupců.
/// DataContext = CellStyleVM. EditOptions řídí, které řádky se zobrazí.
/// Placeholdery zobrazují zděděné hodnoty (CellStyleVM.Inherited) u polí, která uživatel
/// explicitně nepřepsal. Tri-state checkboxy = null znamená „dědit".
/// Pick/Clear commands deleguje na rodiče (dialog VM).
/// </summary>
public partial class CellStyleEditor : UserControl
{
    public CellStyleEditor() { InitializeComponent(); }

    // ── Nullable-enum ItemsSource pro HAlign/VAlign ComboBoxy ─────────────
    // Value je nullable, první položka reprezentuje „dědit" (null sentinel).

    public record HAlignOption(string Label, TextHorizontalAlignment? Value);
    public record VAlignOption(string Label, TextVerticalAlignment? Value);

    // Nullable hodnoty potřebují nullable Value. ComboBox s SelectedValue=null jednoduše nic nevybere
    // a overlay TextBlock v XAMLu zobrazí zděděnou hodnotu jako placeholder. Reset zpět na „dědit"
    // dělá samostatné „×" tlačítko (přes CellStyleVM.ResetFieldCommand).
    public static readonly HAlignOption[] HAlignOptionsDefault =
    [
        new("Vlevo",            TextHorizontalAlignment.Left),
        new("Na střed",         TextHorizontalAlignment.Center),
        new("Vpravo",           TextHorizontalAlignment.Right),
    ];

    public static readonly VAlignOption[] VAlignOptionsDefault =
    [
        new("Nahoru",           TextVerticalAlignment.Top),
        new("Na střed",         TextVerticalAlignment.Middle),
        new("Dolů",             TextVerticalAlignment.Bottom),
    ];

    public HAlignOption[] HAlignOptions
    {
        get => (HAlignOption[])GetValue(HAlignOptionsProperty);
        set => SetValue(HAlignOptionsProperty, value);
    }
    public static readonly DependencyProperty HAlignOptionsProperty =
        DependencyProperty.Register(nameof(HAlignOptions), typeof(HAlignOption[]), typeof(CellStyleEditor),
            new PropertyMetadata(HAlignOptionsDefault));

    public VAlignOption[] VAlignOptions
    {
        get => (VAlignOption[])GetValue(VAlignOptionsProperty);
        set => SetValue(VAlignOptionsProperty, value);
    }
    public static readonly DependencyProperty VAlignOptionsProperty =
        DependencyProperty.Register(nameof(VAlignOptions), typeof(VAlignOption[]), typeof(CellStyleEditor),
            new PropertyMetadata(VAlignOptionsDefault));

    public float[] FontSizeOptions
    {
        get => (float[])GetValue(FontSizeOptionsProperty);
        set => SetValue(FontSizeOptionsProperty, value);
    }
    public static readonly DependencyProperty FontSizeOptionsProperty =
        DependencyProperty.Register(nameof(FontSizeOptions), typeof(float[]), typeof(CellStyleEditor),
            new PropertyMetadata(new[] { 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 14f, 16f, 18f, 20f, 24f }));

    // ── Options (co zobrazit / skrýt) ─────────────────────────────────────

    public CellStyleEditOptions EditOptions
    {
        get => (CellStyleEditOptions)GetValue(EditOptionsProperty);
        set => SetValue(EditOptionsProperty, value);
    }
    public static readonly DependencyProperty EditOptionsProperty =
        DependencyProperty.Register(nameof(EditOptions), typeof(CellStyleEditOptions), typeof(CellStyleEditor),
            new PropertyMetadata(CellStyleEditOptions.AllEnabled));

    // ── Color picker commands (delegovány na rodiče) ──────────────────────

    public ICommand? PickTextColorCommand
    {
        get => (ICommand?)GetValue(PickTextColorCommandProperty);
        set => SetValue(PickTextColorCommandProperty, value);
    }
    public static readonly DependencyProperty PickTextColorCommandProperty =
        DependencyProperty.Register(nameof(PickTextColorCommand), typeof(ICommand), typeof(CellStyleEditor));

    public ICommand? ClearTextColorCommand
    {
        get => (ICommand?)GetValue(ClearTextColorCommandProperty);
        set => SetValue(ClearTextColorCommandProperty, value);
    }
    public static readonly DependencyProperty ClearTextColorCommandProperty =
        DependencyProperty.Register(nameof(ClearTextColorCommand), typeof(ICommand), typeof(CellStyleEditor));

    public ICommand? PickBgColorCommand
    {
        get => (ICommand?)GetValue(PickBgColorCommandProperty);
        set => SetValue(PickBgColorCommandProperty, value);
    }
    public static readonly DependencyProperty PickBgColorCommandProperty =
        DependencyProperty.Register(nameof(PickBgColorCommand), typeof(ICommand), typeof(CellStyleEditor));

    public ICommand? ClearBgColorCommand
    {
        get => (ICommand?)GetValue(ClearBgColorCommandProperty);
        set => SetValue(ClearBgColorCommandProperty, value);
    }
    public static readonly DependencyProperty ClearBgColorCommandProperty =
        DependencyProperty.Register(nameof(ClearBgColorCommand), typeof(ICommand), typeof(CellStyleEditor));
}
