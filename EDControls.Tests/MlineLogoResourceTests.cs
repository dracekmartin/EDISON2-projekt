using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EDControls.EDLogo;

namespace EDControls.Tests;

/// <summary>
/// Vektorová loga sady: značka M-line (<c>EDIcons.MlineLogo</c>, ruční převod <c>mline-logo.svg</c>,
/// složení dvou SVG matic, FillRule Nonzero) a logo EDISON (<c>EDIcons.Molecule</c>, <c>MoleculeLarge</c>).
/// Regrese, kterou to hlídá: chyba v path datech nebo v matici se neprojeví výjimkou, jen prázdným /
/// deformovaným <see cref="Image"/> (patička a hlavička přihlášení, ikona okna).
/// </summary>
public class MlineLogoResourceTests
{
    // Testovací host nevytváří WPF Application → pack:// loading vyžaduje ručně triggernout
    // Application type initializer.
    static MlineLogoResourceTests() => _ = Application.ResourceAssembly;

    private const string IconsUri  = "pack://application:,,,/EDControls;component/EDIcons/EDIcons.xaml";
    private const string StylesUri = "pack://application:,,,/EDControls;component/EDControlStyles/EDControlStyles.xaml";

    [Theory]
    [InlineData("EDIcons.MlineLogo")]
    [InlineData("EDIcons.Molecule")]
    [InlineData("EDIcons.MoleculeLarge")]
    public void VectorLogo_IsDrawingImageWithNonEmptyBounds(string key)
    {
        EDTesting.Sta.Run(() =>
        {
            var logo = Load(IconsUri)[key] as DrawingImage;
            Assert.NotNull(logo);
            Assert.False(logo!.Drawing.Bounds.IsEmpty);
            Assert.True(logo.Width > 0 && logo.Height > 0);
        });
    }

    [Theory]
    [InlineData("EDIcons.MlineLogo")]
    [InlineData("EDIcons.Molecule")]
    [InlineData("EDIcons.MoleculeLarge")]
    public void VectorLogo_IsSquare(string key)
    {
        // Špatně spočítaná složená matice (scale + překlopení Y) se pozná právě na poměru stran.
        EDTesting.Sta.Run(() =>
        {
            var logo = (DrawingImage)Load(IconsUri)[key];
            Assert.InRange(logo.Width / logo.Height, 0.95, 1.05);
        });
    }

    [Fact]
    public void MlineLogo_KeepsBrandColorInBothThemes()
    {
        // Barva je vědomě natvrdo v geometrii, ne theme token - dark mode ji nesmí přebít.
        EDTesting.Sta.Run(() =>
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

    [Theory]
    [InlineData("EDIcons.Molecule")]
    [InlineData("EDIcons.MoleculeLarge")]
    public void EdisonLogo_HasEightAtomsAndTwelveBonds(string key)
    {
        // Průhledný čtverec 64×64 drží bounds i poměr stran, prázdnou kresbu pozná až obsah.
        EDTesting.Sta.Run(() =>
        {
            var drawings = Flatten(((DrawingImage)Load(IconsUri)[key]).Drawing).OfType<GeometryDrawing>().ToList();

            Assert.Equal(8, drawings.Count(d => d.Brush is RadialGradientBrush));
            Assert.Equal(12, drawings.Count(d => d.Pen is not null));
        });
    }

    [Fact]
    public void MoleculeLarge_MatchesCubeAssembly()
    {
        // EDLogo skládá stejnou krychli, v EDIcons.xaml jsou čísla opsaná ručně.
        EDTesting.Sta.Run(() =>
        {
            var logo = (DrawingImage)Load(IconsUri)["EDIcons.MoleculeLarge"];
            var drawings = Flatten(logo.Drawing).OfType<GeometryDrawing>().ToList();
            var iconAtoms = drawings.Where(d => d.Brush is RadialGradientBrush)
                                    .Select(d => (EllipseGeometry)d.Geometry)
                                    .ToList();
            var iconBonds = drawings.Where(d => d.Pen is not null).ToList();
            var cube = new CubeAssembly();

            Assert.Equal(cube.Atoms.Count, iconAtoms.Count);
            Assert.All(cube.Atoms, atom =>
            {
                var icon = iconAtoms.MinBy(e => Math.Abs(e.Center.X - atom.X) + Math.Abs(e.Center.Y - atom.Y))!;
                Assert.Equal(atom.X, icon.Center.X, 0.05);
                Assert.Equal(atom.Y, icon.Center.Y, 0.05);
                Assert.Equal(atom.Radius, icon.RadiusX, 0.05);
            });

            Assert.Equal(cube.Bonds.Count, iconBonds.Count);
            Assert.All(cube.Bonds, bond =>
            {
                // Úsečku jednoznačně určí její obdélník, krychle nemá dvě hrany se stejnými konci.
                var expected = new Rect(new Point(bond.From.X, bond.From.Y), new Point(bond.To.X, bond.To.Y));
                var icon = iconBonds.MinBy(d => Distance(d.Geometry.Bounds, expected))!;
                Assert.InRange(Distance(icon.Geometry.Bounds, expected), 0, 0.2);
                Assert.Equal(bond.Width, icon.Pen.Thickness, 0.05);
            });
        });
    }

    private static double Distance(Rect a, Rect b) =>
        Math.Abs(a.Left - b.Left) + Math.Abs(a.Top - b.Top) + Math.Abs(a.Right - b.Right) + Math.Abs(a.Bottom - b.Bottom);

    [Fact]
    public void LinkButtonStyle_TargetsButtonAndInheritsFontTokens()
    {
        // Bez BasedOn na implicitní Button by odkaz nereagoval na user škálování fontu.
        EDTesting.Sta.Run(() =>
        {
            var style = Load(StylesUri)["EDTheme.LinkButton"] as Style;
            Assert.NotNull(style);
            Assert.Equal(typeof(Button), style!.TargetType);
            Assert.Equal(typeof(Button), style.BasedOn?.TargetType);
        });
    }

    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        if (drawing is DrawingGroup group)
            return group.Children.SelectMany(Flatten);

        return new[] { drawing };
    }

    private static ResourceDictionary Load(string uri) => new() { Source = new Uri(uri) };

}
