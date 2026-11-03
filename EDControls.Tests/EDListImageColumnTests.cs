using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EDControls.EDList;

namespace EDControls.Tests;

/// <summary>
/// Sloupec IMAGE (první konzument okno šablon návrháře, 3. 11.): hodnota je klíč sdílené ikony, seznam nakreslí
/// obrázek. Do té doby byl typ rezervovaný a hodnota se ukazovala jako text.
/// </summary>
public class EDListImageColumnTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void KlicIkony_JeObrazek_PrazdnyANeznamyNic()
    {
        RunSta(() =>
        {
            var prevod = new ListIconKeyToImageConverter();
            object? Ikona(object? hodnota) => prevod.Convert(hodnota!, typeof(ImageSource), null!, CultureInfo.InvariantCulture);

            Assert.IsAssignableFrom<ImageSource>(Ikona("EDIcons.MlineLogo"));
            Assert.Null(Ikona(""));
            Assert.Null(Ikona(null));
            Assert.Null(Ikona("EDIcons.NeexistujiciIkona"));
        });
    }

    [Fact]
    public void SloupecImage_KresliObrazekNeText()
    {
        RunSta(() =>
        {
            var sloupec = EDListColumnFactory.BuildDataColumn(new ListColumn { Key = "ikona", Type = ListColumnType.Image });

            var sablona = Assert.IsType<DataGridTemplateColumn>(sloupec).CellTemplate;
            Assert.Equal(typeof(Image), sablona.VisualTree.Type);
        });
    }

    /// <summary>Ikona u textu (název šablony): samostatný sloupec ikony bez názvu vypadal divně (Jan 3. 11.).</summary>
    [Fact]
    public void TextSIkonou_IkonaPredTextem()
    {
        RunSta(() =>
        {
            var sloupec = EDListColumnFactory.BuildDataColumn(new ListColumn { Key = "name", IconKey = "ikona" });

            var strom = Assert.IsType<DataGridTemplateColumn>(sloupec).CellTemplate.VisualTree;
            Assert.Equal(typeof(DockPanel), strom.Type);
            Assert.Equal(typeof(Image), strom.FirstChild.Type);
            Assert.Equal(typeof(TextBlock), strom.FirstChild.NextSibling.Type);
            Assert.Equal("name", sloupec.GetValue(FrameworkElement.TagProperty));   // hledání a řazení podle textu
        });
    }

    /// <summary>Značka výchozí šablony pro tisk (Jan 3. 11.): hvězdička za názvem, aby název neposunula proti ostatním
    /// řádkům. Panel s textem a značkou je zarovnaný vlevo, jinak by značka stála u pravého okraje sloupce.</summary>
    [Fact]
    public void TextSIkonouZa_IkonaHnedZaTextem()
    {
        RunSta(() =>
        {
            var sloupec = EDListColumnFactory.BuildDataColumn(
                new ListColumn { Key = "name", IconKey = "ikona", TrailingIconKey = "znacka" });

            var sablona = Assert.IsType<DataGridTemplateColumn>(sloupec).CellTemplate;
            sablona.Seal();   // jako DataGrid před prvním použitím, LoadContent jinak odmítne
            var bunka = Assert.IsType<DockPanel>(sablona.LoadContent());
            Assert.IsType<Image>(bunka.Children[0]);
            var textAZnacka = Assert.IsType<DockPanel>(bunka.Children[1]);
            Assert.Equal(HorizontalAlignment.Left, textAZnacka.HorizontalAlignment);
            Assert.IsType<Image>(textAZnacka.Children[0]);
            Assert.Equal(Dock.Right, DockPanel.GetDock(textAZnacka.Children[0]));
            Assert.IsType<TextBlock>(textAZnacka.Children[1]);
        });
    }

    [Fact]
    public void JenIkonaZa_BezIkonyPred()
    {
        RunSta(() =>
        {
            var sloupec = EDListColumnFactory.BuildDataColumn(new ListColumn { Key = "name", TrailingIconKey = "znacka" });

            var sablona = Assert.IsType<DataGridTemplateColumn>(sloupec).CellTemplate;
            sablona.Seal();
            var bunka = Assert.IsType<DockPanel>(sablona.LoadContent());
            Assert.IsType<DockPanel>(Assert.Single(bunka.Children.Cast<UIElement>()));
        });
    }

    [Fact]
    public void TextBezIkony_ZustavaTextovymSloupcem() =>
        RunSta(() => Assert.IsType<DataGridTextColumn>(EDListColumnFactory.BuildDataColumn(new ListColumn { Key = "name" })));

    // Klíče ikon se hledají v Application.Resources jako v reálné appce (viz EDComponentToolbarTests).
    private static void RunSta(Action action) =>
        EDTesting.Sta.RunOnAppThread(() =>
        {
            if (Application.Current is null)
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                             "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
            }
            action();
        });
}
