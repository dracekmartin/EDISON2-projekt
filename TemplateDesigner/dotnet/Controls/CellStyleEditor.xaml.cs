using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Controls;

/// <summary>
/// Sdílený editor TextStyle zóny (záhlaví NEBO data) pro všechny dialogy sloupců.
/// DataContext = CellStyleVM. EditOptions řídí, které řádky se zobrazí.
/// Placeholdery zobrazují zděděné hodnoty (CellStyleVM.Inherited) u polí, která uživatel
/// explicitně nepřepsal. Tri-state checkboxy = null znamená „dědit".
/// Tlačítka barev jedou přes příkazy na <see cref="CellStyleVM"/>; tenhle control mu jen dosadí,
/// jak výběr barvy vyvolat.
/// </summary>
public partial class CellStyleEditor : UserControl
{
    public CellStyleEditor()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

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
        new("Nahoře",           TextVerticalAlignment.Top),
        new("Na střed",         TextVerticalAlignment.Middle),
        new("Dole",             TextVerticalAlignment.Bottom),
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

    // Nainstalované fonty pro editovatelný font ComboBox (default = SKFontManager přes FontResolver).
    public IReadOnlyList<string> FontFamilyOptions
    {
        get => (IReadOnlyList<string>)GetValue(FontFamilyOptionsProperty);
        set => SetValue(FontFamilyOptionsProperty, value);
    }
    public static readonly DependencyProperty FontFamilyOptionsProperty =
        DependencyProperty.Register(nameof(FontFamilyOptions), typeof(IReadOnlyList<string>), typeof(CellStyleEditor),
            new PropertyMetadata(FontResolver.InstalledFamilies));

    // ── Options (co zobrazit / skrýt) ─────────────────────────────────────

    public CellStyleEditOptions EditOptions
    {
        get => (CellStyleEditOptions)GetValue(EditOptionsProperty);
        set => SetValue(EditOptionsProperty, value);
    }
    public static readonly DependencyProperty EditOptionsProperty =
        DependencyProperty.Register(nameof(EditOptions), typeof(CellStyleEditOptions), typeof(CellStyleEditor),
            new PropertyMetadata(CellStyleEditOptions.AllEnabled));

    // Výběr barvy potřebuje okno jako rodiče, a to zná jedině tenhle control - dosadí ho proto
    // VM sám. Dialogy tím nemusí drátovat nic (do 21. 9. každý 8 příkazů a vlastní picker).
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is CellStyleVM vm)
            vm.RequestColorPicker = hex =>
                Window.GetWindow(this) is { } okno ? ColorPickerHelper.PickColor(okno, hex) : null;
    }
}
