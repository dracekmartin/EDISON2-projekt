using System.Windows;
using System.Windows.Controls;
using EDControls.EDFooter;
using Footer = EDControls.EDFooter.EDFooter;

namespace EDControls.Tests;

/// <summary>Viditelnost tlačítek dle FooterMode a propsání LeftContent (bez plného okna).</summary>
public class EDFooterTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void Default_ShowsCloseOnly()
    {
        RunSta(() =>
        {
            var footer = new Footer();

            Assert.Equal(Visibility.Collapsed, footer.OkBtn.Visibility);
            Assert.Equal(Visibility.Collapsed, footer.StornoBtn.Visibility);
            Assert.Equal(Visibility.Visible, footer.CloseBtn.Visibility);
        });
    }

    [Fact]
    public void OkCancelMode_SwapsButtons_AndBack()
    {
        RunSta(() =>
        {
            var footer = new Footer { FooterMode = FooterMode.OkCancel };

            Assert.Equal(Visibility.Visible, footer.OkBtn.Visibility);
            Assert.Equal(Visibility.Visible, footer.StornoBtn.Visibility);
            Assert.Equal(Visibility.Collapsed, footer.CloseBtn.Visibility);

            footer.FooterMode = FooterMode.Close;

            Assert.Equal(Visibility.Collapsed, footer.OkBtn.Visibility);
            Assert.Equal(Visibility.Collapsed, footer.StornoBtn.Visibility);
            Assert.Equal(Visibility.Visible, footer.CloseBtn.Visibility);
        });
    }

    [Fact]
    public void LeftContent_LandsInPresenter()
    {
        RunSta(() =>
        {
            var footer = new Footer();
            var content = new TextBlock { Text = "3 záznamy" };

            footer.LeftContent = content;

            Assert.Same(content, footer.LeftContentPresenter.Content);
        });
    }

    // Footer čte StaticResource EDStrings.* - potřebuje Application se slovníky reálné appky.
    private static void EnsureApplication()
    {
        if (Application.Current is not null) return;

        var app = new Application();
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
    }

    private static void RunSta(Action action) =>
        EDTesting.Sta.Run(() => { EnsureApplication(); action(); });
}
