using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace EDControls.EDSearchTextBox;

/// <summary>
/// TextBox pro vyhledávání: <see cref="Placeholder"/> uvnitř prázdného pole, křížek (✕)
/// pro vyčištění a Esc. Vzhled (template s PART_ClearButton) je implicitním stylem
/// v EDControlStyles.xaml.
/// </summary>
public class EDSearchTextBox : TextBox
{
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(EDSearchTextBox),
            new PropertyMetadata(""));

    /// <summary>Nápověda zobrazená v prázdném poli.</summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_ClearButton") is ButtonBase clear)
        {
            clear.Click -= OnClearClick;
            clear.Click += OnClearClick;
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        Clear();
        Focus();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Esc s textem vyčistí filtr; prázdné pole nechá klávesu probublat
        // (v dialogu ji zpracuje IsCancel tlačítko nebo EDFooter).
        if (e.Key == Key.Escape && Text.Length > 0)
        {
            Clear();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }
}
