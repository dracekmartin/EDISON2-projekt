using TemplateDesigner.Models;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// `B194a` C11: chyba v datech linky (linka v JŘ není, spoj MHD bez kalendáře…) se uživateli ukáže jako
/// informace s tím, co opravit, ne jako „Načtení dat agendy 'X' selhalo: … neexistuje v ^JRL".
/// Agenda ji posílá jako obecnou chybu 5001 (`BaseAgenda.ChybaDat`); jiná chyba serveru zůstává chybou
/// programu. Tisk se zastaví (rozhodnutí Jan 26. 10.), hláška řekne u které linky.
/// </summary>
public class ChybaDatTests
{
    private const string Text = "Linka 999999 ve skupině 1 v jízdních řádech není. Mohla být smazána nebo přesunuta do jiné skupiny - vyberte linky znovu.";

    /// <summary>Zpráva tak, jak ji MLRest vrací (ověřeno voláním 26. 10.): kód, text, výpis volání.</summary>
    private const string ZpravaServeru = "1~\r\nERROR #5001: " + Text
        + "<EDISON>   at ALWA.SYS.MLRest.Query(String calledClass, String calledMethod, CVar varIn) in C:\\DEV\\EDISON\\MLRest\\MLRest\\MLRest.cs:line 649\r\n1~\r\nERROR #5001: " + Text;

    [Fact]
    public void ChybaDat_JenTextBezKoduAVypisu() =>
        Assert.Equal(Text, CacheAgendaProvider.ChybaDat(ZpravaServeru));

    [Theory]
    [InlineData("1~\r\n<UNDEFINED>zGetData+5^ALVA.TemplateDesigner.Agendy.LinkovyJrAd.1 *x")]
    [InlineData("Spojení se serverem bylo přerušeno.")]
    [InlineData("")]
    [InlineData(null)]
    public void JinaChyba_NeniChybaDat(string? zprava) =>
        Assert.Null(CacheAgendaProvider.ChybaDat(zprava));

    [Fact]
    public void Hlaska_JeProblemLinkySTextemJakoRadou()
    {
        var h = KontrolaVystupu.LinkaChybaDat("18 - Máj - Nádraží", Text);

        Assert.Equal(ZavaznostHlaseni.Chyba, h.Zavaznost);
        Assert.Equal("18 - Máj - Nádraží", h.Linka);
        Assert.Equal(Text, h.Rada);
    }

    [Fact]
    public void ChybaDat_ZustavaChybouAgendy()
    {
        var ex = new CacheAgendaDataException(Text, "18");

        Assert.IsAssignableFrom<CacheAgendaException>(ex);   // stávající ošetření chyb agendy ji dál chytí
        Assert.Equal("18", ex.Linka);
    }
}
