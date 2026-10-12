using System.Globalization;
using System.Windows;

namespace EDISONMAIN2.Tests;

public class EdisonMainWindowConvertersTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData("desktop", "desktop", Visibility.Visible)]
    [InlineData("desktop", "system", Visibility.Collapsed)]
    [InlineData(null, "system", Visibility.Collapsed)]
    public void TabKey_MatchShowsContent(string? activeKey, string tabKey, Visibility expected)
    {
        var converter = new TabKeyToVisibilityConverter();

        Assert.Equal(expected, converter.Convert(activeKey, typeof(Visibility), tabKey, Culture));
    }

    [Fact]
    public void TabKey_ConvertBack_Throws()
    {
        var converter = new TabKeyToVisibilityConverter();

        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(Visibility.Visible, typeof(string), "desktop", Culture));
    }

    [Fact]
    public void ActiveTree_ShowsOnlyTreeOfActiveTab()
    {
        var converter = new ActiveTreeToVisibilityConverter();
        var active = new System.Collections.ObjectModel.ObservableCollection<int>();
        var other = new System.Collections.ObjectModel.ObservableCollection<int>();

        Assert.Equal(Visibility.Visible, converter.Convert([active, active], typeof(Visibility), null, Culture));
        Assert.Equal(Visibility.Collapsed, converter.Convert([active, other], typeof(Visibility), null, Culture));
        // Záložka bez stromu (Používané) neukáže strom, ani když jeho ItemsSource ještě není navázaný.
        Assert.Equal(Visibility.Collapsed, converter.Convert([null, null], typeof(Visibility), null, Culture));
        Assert.Equal(Visibility.Collapsed,
            converter.Convert([DependencyProperty.UnsetValue, DependencyProperty.UnsetValue], typeof(Visibility), null, Culture));
    }

    [Fact]
    public void ResourceKey_WithoutApplication_ReturnsNull()
    {
        // V testech není Application.Current - converter nesmí spadnout (design-time chování)
        var converter = new ResourceKeyConverter();

        Assert.Null(converter.Convert("EDIcons.Desktop", typeof(object), null, Culture));
    }

    [Fact]
    public void ResourceKey_NonStringValue_ReturnsNull()
    {
        var converter = new ResourceKeyConverter();

        Assert.Null(converter.Convert(42, typeof(object), null, Culture));
    }

    [Fact]
    public void ResourceKey_ConvertBack_Throws()
    {
        var converter = new ResourceKeyConverter();

        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(new object(), typeof(string), null, Culture));
    }
}
