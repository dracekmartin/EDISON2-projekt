using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Controls;

/// <summary>
/// Sdílený editor TextStyle zóny (záhlaví NEBO data) pro všechny dialogy sloupců.
/// DataContext = CellStyleVM. EditOptions řídí, které řádky se zobrazí.
/// Pick/Clear commands deleguje na rodiče (dialog VM).
/// </summary>
public partial class CellStyleEditor : UserControl
{
    public CellStyleEditor()
    {
        InitializeComponent();
    }

    // ── Options (co zobrazit / skrýt) ─────────────────────────────────────

    public static readonly DependencyProperty EditOptionsProperty =
        DependencyProperty.Register(nameof(EditOptions), typeof(CellStyleEditOptions), typeof(CellStyleEditor),
            new PropertyMetadata(CellStyleEditOptions.AllEnabled));

    public CellStyleEditOptions EditOptions
    {
        get => (CellStyleEditOptions)GetValue(EditOptionsProperty);
        set => SetValue(EditOptionsProperty, value);
    }

    // ── Color picker commands (delegovány na rodiče) ──────────────────────

    public static readonly DependencyProperty PickTextColorCommandProperty =
        DependencyProperty.Register(nameof(PickTextColorCommand), typeof(ICommand), typeof(CellStyleEditor));

    public ICommand? PickTextColorCommand
    {
        get => (ICommand?)GetValue(PickTextColorCommandProperty);
        set => SetValue(PickTextColorCommandProperty, value);
    }

    public static readonly DependencyProperty ClearTextColorCommandProperty =
        DependencyProperty.Register(nameof(ClearTextColorCommand), typeof(ICommand), typeof(CellStyleEditor));

    public ICommand? ClearTextColorCommand
    {
        get => (ICommand?)GetValue(ClearTextColorCommandProperty);
        set => SetValue(ClearTextColorCommandProperty, value);
    }

    public static readonly DependencyProperty PickBgColorCommandProperty =
        DependencyProperty.Register(nameof(PickBgColorCommand), typeof(ICommand), typeof(CellStyleEditor));

    public ICommand? PickBgColorCommand
    {
        get => (ICommand?)GetValue(PickBgColorCommandProperty);
        set => SetValue(PickBgColorCommandProperty, value);
    }

    public static readonly DependencyProperty ClearBgColorCommandProperty =
        DependencyProperty.Register(nameof(ClearBgColorCommand), typeof(ICommand), typeof(CellStyleEditor));

    public ICommand? ClearBgColorCommand
    {
        get => (ICommand?)GetValue(ClearBgColorCommandProperty);
        set => SetValue(ClearBgColorCommandProperty, value);
    }

    // ── Read-only volby pro ComboBoxy ─────────────────────────────────────

    public static readonly DependencyProperty HAlignmentOptionsProperty =
        DependencyProperty.Register(nameof(HAlignmentOptions), typeof(string[]), typeof(CellStyleEditor),
            new PropertyMetadata(new[] { "Vlevo", "Na střed", "Vpravo" }));

    public string[] HAlignmentOptions
    {
        get => (string[])GetValue(HAlignmentOptionsProperty);
        set => SetValue(HAlignmentOptionsProperty, value);
    }

    public static readonly DependencyProperty VAlignmentOptionsProperty =
        DependencyProperty.Register(nameof(VAlignmentOptions), typeof(string[]), typeof(CellStyleEditor),
            new PropertyMetadata(new[] { "Nahoru", "Na střed", "Dolů" }));

    public string[] VAlignmentOptions
    {
        get => (string[])GetValue(VAlignmentOptionsProperty);
        set => SetValue(VAlignmentOptionsProperty, value);
    }

    public static readonly DependencyProperty FontSizeOptionsProperty =
        DependencyProperty.Register(nameof(FontSizeOptions), typeof(float[]), typeof(CellStyleEditor),
            new PropertyMetadata(new[] { 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 14f, 16f, 18f, 20f, 24f }));

    public float[] FontSizeOptions
    {
        get => (float[])GetValue(FontSizeOptionsProperty);
        set => SetValue(FontSizeOptionsProperty, value);
    }
}
