using TemplateDesigner.Models;
using TemplateDesigner.PdfImport;

namespace TemplateDesigner.Tests.PdfImport;

/// <summary>
/// Překlad názvu fontu z PDF na rodinu písma. PDF nesou řez v názvu („Arial,Bold") a oddělovač
/// v rodině zůstával - výsledkem byla rodina „Arial," s čárkou, kterou pak systém neznal a šablona
/// po importu hlásila „Font Arial není na tomto počítači nainstalovaný".
/// </summary>
public class PdfFontMapperTests
{
    [Theory]
    [InlineData("Arial", "Arial")]
    [InlineData("Arial,Bold", "Arial")]
    [InlineData("Arial,BoldItalic", "Arial")]
    [InlineData("Arial-BoldMT", "Arial")]
    [InlineData("ArialMT", "Arial")]
    [InlineData("ABCDEF+Arial,Bold", "Arial")]
    [InlineData("Helvetica,Bold", "Arial")]
    [InlineData("Times-Roman", "Times New Roman")]
    [InlineData("Arial Narrow,Bold", "Arial Narrow")]
    public void RezSeZNazvuOdloupneIVCetneOddelovace(string pdfName, string expected)
    {
        var (family, _, _) = PdfFontMapper.Map(pdfName);

        Assert.Equal(expected, family);
    }

    [Theory]
    [InlineData("Arial,Bold", true, false)]
    [InlineData("Arial,BoldItalic", true, true)]
    [InlineData("Arial-ItalicMT", false, true)]
    [InlineData("Arial", false, false)]
    public void RezSePozna(string pdfName, bool bold, bool italic)
    {
        var (_, isBold, isItalic) = PdfFontMapper.Map(pdfName);

        Assert.Equal(bold, isBold);
        Assert.Equal(italic, isItalic);
    }

    /// <summary>Rodina z importu musí být taková, jakou systém zná - jinak šablona hned po importu
    /// tvrdí, že font chybí, a uživatel nemá co doinstalovat.</summary>
    [Theory]
    [InlineData("Arial,Bold")]
    [InlineData("Helvetica,Bold")]
    [InlineData("ArialMT")]
    public void BezneFontyZPdfJsouPoImportuZnameSystemu(string pdfName)
    {
        var (family, _, _) = PdfFontMapper.Map(pdfName);

        Assert.True(FontResolver.IsInstalled(family), $"rodina „{family}\" není známá");
    }
}
