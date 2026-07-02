using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// DocumentDefaults = procesně-globální kořen kaskády (výchozí styl šablony).
/// Každý test MUSÍ v finally vrátit Style = null, jinak „přeteče" do dalších testů.
/// </summary>
[Collection("StyleCascade")]
public class DocumentDefaultsTests
{
    [Fact]
    public void BezDokumentovehoDefaultu_PlatiZapeceneHodnoty()
    {
        DocumentDefaults.Style = null;

        var resolved = ResolvedTextStyle.Default;

        Assert.Equal("Arial", resolved.FontFamily);
        Assert.Equal(10f, resolved.FontSize);
    }

    [Fact]
    public void DokumentovyFont_SePropiseDoKoreneKaskady()
    {
        try
        {
            DocumentDefaults.Style = new TextStyle { FontFamily = "Roboto Condensed" };

            var resolved = ResolvedTextStyle.Default;

            Assert.Equal("Roboto Condensed", resolved.FontFamily);
            Assert.Equal(10f, resolved.FontSize);   // nevyplněné zůstává zapečené
        }
        finally { DocumentDefaults.Style = null; }
    }

    [Fact]
    public void DokumentovyDefault_SePropagujeAzDoResolveElementu()
    {
        try
        {
            DocumentDefaults.Style = new TextStyle { FontFamily = "Verdana" };

            // Element bez vlastního fontu → zdědí dokumentový přes celou kaskádu
            var resolved = new TextStyle { FontSize = 8f }.Resolve();

            Assert.Equal("Verdana", resolved.FontFamily);
            Assert.Equal(8f, resolved.FontSize);
        }
        finally { DocumentDefaults.Style = null; }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PrazdnyDokumentovyFont_NeprepisujeZapeceny(string? fontFamily)
    {
        try
        {
            DocumentDefaults.Style = new TextStyle { FontFamily = fontFamily };

            Assert.Equal("Arial", ResolvedTextStyle.Default.FontFamily);
        }
        finally { DocumentDefaults.Style = null; }
    }

    [Fact]
    public void DokumentovyFont_SeOrezavaOdMezer()
    {
        try
        {
            DocumentDefaults.Style = new TextStyle { FontFamily = " Tahoma " };

            Assert.Equal("Tahoma", ResolvedTextStyle.Default.FontFamily);
        }
        finally { DocumentDefaults.Style = null; }
    }
}
