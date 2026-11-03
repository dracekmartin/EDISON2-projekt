using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EDControls.Tests;

/// <summary>
/// <c>EDTheme.PrimaryButton</c>: bílý text na plné modré. Regrese, kterou to hlídá: plocha převzatá
/// z <c>Accent.Blue</c> (v dark světlejší) nebo z implicitního Buttonu by bílý text udělala nečitelným.
/// </summary>
public class EDThemePrimaryButtonTests
{
    static EDThemePrimaryButtonTests() => _ = Application.ResourceAssembly;

    private const string LightUri  = "pack://application:,,,/EDControls;component/EDTheme/EDTheme.xaml";
    private const string DarkUri   = "pack://application:,,,/EDControls;component/EDTheme/EDThemeDark.xaml";
    private const string StylesUri = "pack://application:,,,/EDControls;component/EDControlStyles/EDControlStyles.xaml";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WhiteText_HasReadableContrastOnButton(bool dark)
    {
        EDTesting.Sta.Run(() =>
        {
            var light = Load(LightUri);
            var theme = dark ? Load(DarkUri) : light;
            // OnAccent.Text se v dark nepřepisuje, bere se vždy ze základního slovníku.
            var text = ((SolidColorBrush)light["EDTheme.OnAccent.Text"]).Color;
            var bg = ((SolidColorBrush)theme["EDTheme.PrimaryButton.Bg"]).Color;

            Assert.True(Contrast(text, bg) >= 4.5, $"Kontrast {Contrast(text, bg):F2} < 4.5 (WCAG AA)");
        });
    }

    [Fact]
    public void Style_IsButtonBasedOnImplicitStyle()
    {
        // BasedOn nese font tokeny a focus visual; bez něj by tlačítko nereagovalo na user škálování fontu.
        EDTesting.Sta.Run(() =>
        {
            var style = Load(StylesUri)["EDTheme.PrimaryButton"] as Style;
            Assert.NotNull(style);
            Assert.Equal(typeof(Button), style!.TargetType);
            Assert.Equal(typeof(Button), style.BasedOn?.TargetType);
        });
    }

    private static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(Color c) =>
        0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);

    private static double Linear(byte channel)
    {
        double s = channel / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    private static ResourceDictionary Load(string uri) => new() { Source = new Uri(uri) };
}
