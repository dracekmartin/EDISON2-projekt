using EDISONMAIN2.Windows.EdisonMainWindow.SupportStrip;

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
    public void Edison2_PreLogin_CarriesIdfaOnly()
    {
        // Přihlašovací obrazovka zná firmu z pre-login MLRest, uživatele ani e-mail ještě ne.
        var url = WidgetUrls.Edison2("ALVA", "", "");

        Assert.EndsWith("&idfa=ALVA&user=&email=", url);
    }
}
