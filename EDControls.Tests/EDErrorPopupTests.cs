using System.IO;
using System.Reflection;
using EDControls.EDViewModel;

namespace EDControls.Tests;

/// <summary>Hláška chybového dialogu: uživatel má vidět příčinu, ne obal (TargetInvocation, Aggregate) ani prázdno.</summary>
public class EDErrorPopupTests
{
    [Fact]
    public void HeadlineFor_PlainException_UsesItsMessage()
    {
        var headline = EDErrorPopup.HeadlineFor(new InvalidOperationException("Nelze spustit modul."));

        Assert.Equal("Nelze spustit modul.", headline);
    }

    [Fact]
    public void HeadlineFor_WrappedException_UsesInnermostMessage()
    {
        var inner = new FileNotFoundException("Chybí EDISONMAIN2API.exe.", "x.exe");
        var wrapped = new TargetInvocationException(new AggregateException(inner));

        Assert.Equal("Chybí EDISONMAIN2API.exe.", EDErrorPopup.HeadlineFor(wrapped));
    }

    [Fact]
    public void HeadlineFor_EmptyMessage_FallsBackToTypeName()
    {
        Assert.Equal(nameof(InvalidOperationException), EDErrorPopup.HeadlineFor(new InvalidOperationException("")));
    }

    [Theory]
    [InlineData("TD", "TD-chyba-20261005-143007.txt")]       // soubor z TemplateDesigneru nesmí tvrdit, že je ze shellu
    [InlineData("EDISON2", "EDISON2-chyba-20261005-143007.txt")]
    [InlineData(null, "chyba-20261005-143007.txt")]           // popup před EDServices.Initialize
    public void SuggestedFileName_CarriesAppPrefix(string? prefix, string expected)
    {
        Assert.Equal(expected, EDErrorPopup.SuggestedFileName(prefix, new DateTime(2026, 10, 5, 14, 30, 7)));
    }
}
