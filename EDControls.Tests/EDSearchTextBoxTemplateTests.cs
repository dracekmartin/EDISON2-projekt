using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace EDControls.Tests;

/// <summary>
/// Placeholder v <c>EDSearchTextBox</c> je overlay TextBlock nad PART_ContentHost, takže leží
/// přesně tam, kde stojí caret na pozici 0. HD 13132/2: bez skrytí při fokusu kurzor bliká uvnitř
/// prvního písmene nápovědy. Test hlídá i pořadí triggerů - to je celá past.
/// </summary>
public class EDSearchTextBoxTemplateTests
{
    static EDSearchTextBoxTemplateTests() => _ = Application.ResourceAssembly;

    private const string StylesUri = "pack://application:,,,/EDControls;component/EDControlStyles/EDControlStyles.xaml";

    [Fact]
    public void Placeholder_CollapsesOnKeyboardFocus()
    {
        EDTesting.Sta.Run(() =>
        {
            var trigger = PlaceholderTriggers()
                .Single(t => t.Property == UIElement.IsKeyboardFocusWithinProperty);

            Assert.True((bool)trigger.Value!);
            var setter = trigger.Setters.OfType<Setter>().Single(s => s.TargetName == "Placeholder");
            Assert.Equal(Visibility.Collapsed, (Visibility)setter.Value!);
        });
    }

    [Fact]
    public void FocusTrigger_ComesAfterEmptyTextTrigger()
    {
        // U shodné property vyhrává poslední triggerující. Prohozené pořadí = placeholder
        // se ve fokusovaném prázdném poli zobrazí zpátky a bug je tiše zpět.
        EDTesting.Sta.Run(() =>
        {
            var triggers = PlaceholderTriggers();
            int emptyText = triggers.FindIndex(t => t.Property == TextBox.TextProperty);
            int focus = triggers.FindIndex(t => t.Property == UIElement.IsKeyboardFocusWithinProperty);

            Assert.True(emptyText >= 0, "chybí trigger na prázdný Text");
            Assert.True(focus > emptyText, "trigger na fokus musí být ZA triggerem na prázdný Text");
        });
    }

    // Triggery templatu EDSearchTextBox, které sahají na element "Placeholder".
    private static List<Trigger> PlaceholderTriggers()
    {
        var style = (Style)new ResourceDictionary { Source = new Uri(StylesUri) }
            [typeof(EDControls.EDSearchTextBox.EDSearchTextBox)];
        var template = (ControlTemplate)style.Setters.OfType<Setter>()
            .Single(s => s.Property == Control.TemplateProperty).Value;

        return [.. template.Triggers.OfType<Trigger>()
                   .Where(t => t.Setters.OfType<Setter>().Any(s => s.TargetName == "Placeholder"))];
    }
}
