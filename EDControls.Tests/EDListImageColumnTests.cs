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
