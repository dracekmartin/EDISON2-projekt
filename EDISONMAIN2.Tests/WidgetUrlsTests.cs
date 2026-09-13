using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Tests;

public class WidgetUrlsTests
{
    private const string WidgetsBase = "https://podpora.m-line.cz/api/widgets/";

    [Fact]
    public void Edison2_CarriesEscapedIdentityAndEmail()
    {
        var url = WidgetUrls.Edison2("A B", "user@x", "jan.novak@firma.cz");

        Assert.StartsWith($"{WidgetsBase}edison2?", url);
        Assert.Contains("key=", url);
        Assert.Contains("idfa=A%20B", url);
        Assert.Contains("user=user%40x", url);
        Assert.EndsWith("&email=jan.novak%40firma.cz", url);
    }

    [Fact]
    public void Edison2_EmptyValues_StayAsEmptyParameters()
    {
        // Parita s legacy skládáním URL: prázdná hodnota se posílá jako prázdný parametr, ne vynechá.
        var url = WidgetUrls.Edison2("", "", "");

        Assert.EndsWith("&idfa=&user=&email=", url);
    }

    [Fact]
    public void Novinky_IsAuthFree()
    {
        var url = WidgetUrls.Novinky;

        Assert.StartsWith($"{WidgetsBase}info?", url);
        Assert.Contains("key=", url);
        // Pre-login bezpečné → žádná identita v URL.
        Assert.DoesNotContain("idfa=", url);
        Assert.DoesNotContain("user=", url);
    }

    [Fact]
    public void Wiki_IsAuthFree()
    {
        var url = WidgetUrls.Wiki;

        Assert.StartsWith($"{WidgetsBase}wiki?", url);
        Assert.Contains("key=", url);
        Assert.DoesNotContain("idfa=", url);
        Assert.DoesNotContain("user=", url);
    }

    [Fact]
    public void Trainings_CarriesEscapedIdentity()
    {
        var url = WidgetUrls.Trainings("A B", "user@x");

        Assert.StartsWith($"{WidgetsBase}trainings?", url);
        Assert.Contains("key=", url);
        Assert.Contains("idfa=A%20B", url);
        Assert.Contains("user=user%40x", url);
    }

    [Fact]
    public void Trainings_EmptyIdentity_PreLogin()
    {
        var url = WidgetUrls.Trainings("", "");

        Assert.Contains("idfa=&", url);
        Assert.EndsWith("user=", url);
    }
}
