using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EDControls.EDInputForm;
using Form = EDControls.EDInputForm.EDInputForm;

namespace EDControls.Tests;

/// <summary>
/// <see cref="EDNumericInput.IsValid"/> je paste-validace numerických polí: číslice, jeden vedoucí
/// <c>-</c>, v <c>Decimal</c> módu jeden oddělovač (<c>.</c> nebo <c>,</c>). Ostatní se odmítne.
/// </summary>
public class EDNumericInputTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Theory]
    [InlineData("123", "Integer")]
    [InlineData("-5", "Integer")]
    [InlineData("0", "Integer")]
    [InlineData("", "Integer")]
    public void IsValid_Integer_AcceptsDigitsAndLeadingMinus(string text, string mode)
    {
        Assert.True(EDNumericInput.IsValid(text, mode));
    }

    [Theory]
    [InlineData("1.5", "Decimal")]
    [InlineData("1,5", "Decimal")]
    [InlineData("-3.14", "Decimal")]
    [InlineData(".5", "Decimal")]
    public void IsValid_Decimal_AcceptsSingleSeparator(string text, string mode)
    {
        Assert.True(EDNumericInput.IsValid(text, mode));
    }

    [Theory]
    [InlineData("12a3", "Integer")]      // písmeno
    [InlineData("1-2", "Integer")]       // minus jinde než na začátku
    [InlineData("1.5", "Integer")]       // oddělovač v Integer módu
    [InlineData("1.2.3", "Decimal")]     // dva oddělovače
    [InlineData("1.2,3", "Decimal")]     // dva různé oddělovače
    [InlineData("1 2", "Decimal")]       // mezera
    public void IsValid_Rejects(string text, string mode)
    {
        Assert.False(EDNumericInput.IsValid(text, mode));
    }

    // Omezení vstupu číselného pole visí jen na attached property v šabloně EDInputForm.xaml.
    [Fact]
    public void NumberFieldTemplates_SetModeOnTheirTextBox()
    {
        RunSta(() =>
        {
            var group = new Group { Number = 1, Key = "g1", GroupName = "Vůz", IsExpanded = true };
            var form = new Form
            {
                Items = new List<Item>
                {
                    group,
                    new Field { Number = 2, Key = "mista", Label = "Počet míst", FieldType = FieldType.Number, ParentGroup = group },
                    new Field { Number = 3, Key = "delka", Label = "Délka", FieldType = FieldType.Decimal, ParentGroup = group },
                },
            };
            var root = new Grid();
            root.Children.Add(form);
            root.Measure(new Size(800, 600));
            root.Arrange(new Rect(0, 0, 800, 600));
            root.UpdateLayout();

            var modes = Descendants<TextBox>(form).Select(EDNumericInput.GetMode).Where(m => m is not null).ToList();

            Assert.Equal(new[] { "Integer", "Decimal" }, modes);
        });
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    // EDInputForm.xaml čte StaticResource EDIcons/EDStrings → Application se slovníky appky.
    private static void EnsureApplication()
    {
        if (Application.Current is not null) return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
    }

    private static void RunSta(Action action) =>
        EDTesting.Sta.RunOnAppThread(() => { EnsureApplication(); action(); });
}
