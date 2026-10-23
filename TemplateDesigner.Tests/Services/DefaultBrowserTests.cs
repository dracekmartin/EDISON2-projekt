using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Services;

/// <summary>
/// Příkaz výchozího prohlížeče z registru Windows → spuštění s adresou PDF. Tvary příkazů jsou
/// opsané z registru běžných prohlížečů (ProgId z volby uživatele pro https).
/// </summary>
public class DefaultBrowserTests
{
    private const string Url = "file:///C:/Users/x/AppData/Local/Temp/EDISON/Linkov%C3%BD%20J%C5%98%20380760.pdf";

    [Theory]
    // Edge a Chrome: --single-argument bere zbytek řádku doslova, adresa proto bez uvozovek
    [InlineData("\"C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe\" --single-argument %1",
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", "--single-argument " + Url)]
    [InlineData("\"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe\" --single-argument %1",
                @"C:\Program Files\Google\Chrome\Application\chrome.exe", "--single-argument " + Url)]
    // Firefox má %1 v uvozovkách
    [InlineData("\"C:\\Program Files\\Mozilla Firefox\\firefox.exe\" -osint -url \"%1\"",
                @"C:\Program Files\Mozilla Firefox\firefox.exe", "-osint -url " + Url)]
    // Bez %1 se adresa připojí, program bez uvozovek do první mezery
    [InlineData(@"C:\browser\b.exe -new", @"C:\browser\b.exe", "-new " + Url)]
    [InlineData(@"C:\browser\b.exe", @"C:\browser\b.exe", Url)]
    public void PrikazZRegistru_DostaneAdresu(string command, string exe, string args)
    {
        var start = DefaultBrowser.BuildStart(command, Url);

        Assert.NotNull(start);
        Assert.Equal(exe, start.Value.Exe);
        Assert.Equal(args, start.Value.Arguments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\"C:\\neuzavrena cesta.exe %1")]
    public void NeplatnyPrikaz_Null(string command) =>
        Assert.Null(DefaultBrowser.BuildStart(command, Url));
}
