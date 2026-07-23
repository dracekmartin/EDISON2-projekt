using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Vyrenderuje loose XAML (bez x:Class) offscreen ve světlém i tmavém režimu do dvou PNG.
// Theming se aplikuje stejně jako v appce (ThemeService): base slovníky + na dark navíc EDThemeDark.
// Použití:  ThemeRender <input.xaml> [outDir] [--width N]
// Loose XAML používej pro ověření stylů/šablon/token kombinací; live controly závislé na
// EDServices/MLRest tudy nevyrenderuješ (ty postav do malého loose XAML harness souboru).

internal static class Program
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    private static readonly string[] BaseDicts =
    {
        Comp + "EDTheme/EDTheme.xaml",
        Comp + "EDControlStyles/EDControlStyles.xaml",
        Comp + "EDIcons/EDIcons.xaml",
        Comp + "EDStrings/EDStrings.xaml",
    };

    private const string DarkDict = Comp + "EDTheme/EDThemeDark.xaml";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: ThemeRender <input.xaml> [outDir] [--width N]");
            return 2;
        }

        string xamlPath = Path.GetFullPath(args[0]);
        string outDir = args.Length > 1 && !args[1].StartsWith("--")
            ? Path.GetFullPath(args[1])
            : Path.GetDirectoryName(xamlPath)!;
        double? fixedWidth = null;
        for (int i = 1; i < args.Length - 1; i++)
            if (args[i] == "--width" && double.TryParse(args[i + 1], out var w)) fixedWidth = w;

        if (!File.Exists(xamlPath))
        {
            Console.Error.WriteLine($"not found: {xamlPath}");
            return 2;
        }
        Directory.CreateDirectory(outDir);

        var app = new Application();
        foreach (var uri in BaseDicts)
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri) });

        string name = Path.GetFileNameWithoutExtension(xamlPath);
        string lightPng = Path.Combine(outDir, name + ".light.png");
        Render(app, xamlPath, lightPng, fixedWidth);

        // Dark = přidat override slovník na konec (přebije barevné brush klíče), stejně jako ThemeService.
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(DarkDict) });
        string darkPng = Path.Combine(outDir, name + ".dark.png");
        Render(app, xamlPath, darkPng, fixedWidth);

        Console.WriteLine(lightPng);
        Console.WriteLine(darkPng);
        return 0;
    }

    private static void Render(Application app, string xamlPath, string outPng, double? fixedWidth)
    {
        FrameworkElement el;
        using (var fs = File.OpenRead(xamlPath))
            el = (FrameworkElement)XamlReader.Load(fs);

        var host = new Border { Child = el, Padding = new Thickness(16) };
        host.Background = app.Resources.Contains("EDTheme.Bg.Base")
            ? (Brush)app.Resources["EDTheme.Bg.Base"]
            : Brushes.White;

        var avail = new Size(fixedWidth ?? double.PositiveInfinity, double.PositiveInfinity);
        host.Measure(avail);
        var size = host.DesiredSize;
        if (fixedWidth is double fw) size.Width = fw;
        if (size.Width < 1) size.Width = 400;
        if (size.Height < 1) size.Height = 200;
        host.Arrange(new Rect(size));
        host.UpdateLayout();

        var rtb = new RenderTargetBitmap(
            (int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height), 96, 96, PixelFormats.Pbgra32);
        rtb.Render(host);

        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var os = File.Create(outPng);
        enc.Save(os);
    }
}
