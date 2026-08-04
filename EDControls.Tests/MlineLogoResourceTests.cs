using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EDControls.Tests;

/// <summary>
/// Značka M-line (<c>EDIcons.MlineLogo</c>) je jediná vektorová položka sady - geometrie vznikla
/// ručním převodem <c>mline-logo.svg</c> (složení dvou SVG matic, FillRule Nonzero). Regrese, kterou
/// to hlídá: chyba v path datech nebo v matici se neprojeví výjimkou, jen prázdným / deformovaným
/// <see cref="Image"/> v patičce přihlašovací obrazovky.
/// </summary>
public class MlineLogoResourceTests
{
    // Testovací host nevytváří WPF Application → pack:// loading vyžaduje ručně triggernout
    // Application type initializer.
    static MlineLogoResourceTests() => _ = Application.ResourceAssembly;

    private const string IconsUri  = "pack://application:,,,/EDControls;component/EDIcons/EDIcons.xaml";
    private const string StylesUri = "pack://application:,,,/EDControls;component/EDControlStyles/EDControlStyles.xaml";

    [Fact]
    public void MlineLogo_IsDrawingImageWithNonEmptyBounds()
    {
        RunSta(() =>
        {
            var logo = Load(IconsUri)["EDIcons.MlineLogo"] as DrawingImage;
            Assert.NotNull(logo);
            Assert.False(logo!.Drawing.Bounds.IsEmpty);
            Assert.True(logo.Width > 0 && logo.Height > 0);
        });
    }

    [Fact]
    public void MlineLogo_IsSquare()
    {
        // Špatně spočítaná složená matice (scale + překlopení Y) se pozná právě na poměru stran.
        RunSta(() =>
        {
            var logo = (DrawingImage)Load(IconsUri)["EDIcons.MlineLogo"];
            Assert.InRange(logo.Width / logo.Height, 0.95, 1.05);
        });
    }

    [Fact]
    public void MlineLogo_KeepsBrandColorInBothThemes()
    {
        // Barva je vědomě natvrdo v geometrii, ne theme token - dark mode ji nesmí přebít.
        RunSta(() =>
        {
            var logo = (DrawingImage)Load(IconsUri)["EDIcons.MlineLogo"];
            var brushes = Flatten(logo.Drawing).OfType<GeometryDrawing>()
                                               .Select(d => d.Brush)
                                               .OfType<SolidColorBrush>()
                                               .Select(b => b.Color)
                                               .Distinct()
                                               .ToList();

            Assert.Equal(new[] { Color.FromRgb(0x1D, 0x5A, 0x82) }, brushes);
        });
    }

    [Fact]
    public void LinkButtonStyle_TargetsButtonAndInheritsFontTokens()
    {
        // Bez BasedOn na implicitní Button by odkaz nereagoval na user škálování fontu.
        RunSta(() =>
        {
            var style = Load(StylesUri)["EDTheme.LinkButton"] as Style;
            Assert.NotNull(style);
            Assert.Equal(typeof(Button), style!.TargetType);
            Assert.Equal(typeof(Button), style.BasedOn?.TargetType);
        });
    }

    // ---------- Helpers ----------

    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        if (drawing is DrawingGroup group)
            return group.Children.SelectMany(Flatten);

        return new[] { drawing };
    }

    private static ResourceDictionary Load(string uri) => new() { Source = new Uri(uri) };

    // Spustí akci na dedikovaném STA vlákně (WPF resource loading to vyžaduje) a propaguje
    // výjimku zpět.
    private static void RunSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured != null) throw captured;
    }
}
