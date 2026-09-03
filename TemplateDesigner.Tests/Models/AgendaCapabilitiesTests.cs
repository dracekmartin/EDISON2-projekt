using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Které prvky jízdního řádu paleta nabídne u kterého druhu agendy.
/// </summary>
/// <remarks>
/// Na tuhle vazbu nebyl do 3. 9. žádný test, a přitom se z ní dá vyčíst, co uživatel v aplikaci
/// vůbec může vložit. Projevilo se to takhle: prvek Poznámky nebyl u linkových jízdních řádů
/// v paletě a v evidenci přesto stálo, že požadavek zákazníka „poznámky vedle tabulky" umíme
/// (#11373) - protože to platilo pro zastávkové (nález Jan 3. 9.).
/// </remarks>
public class AgendaCapabilitiesTests
{
    [Fact]
    public void ZastavkovyJizdniRad_MaTriPrvky()
    {
        var typy = AgendaCapabilities.ScheduleElementTypes("STOP");

        Assert.Contains(ElementTypes.StopList, typy);
        Assert.Contains(ElementTypes.DepartureGrid, typy);
        Assert.Contains(ElementTypes.NotesLegend, typy);
    }

    [Fact]
    public void LinkovyJizdniRad_MaTabulkuIPoznamky()
    {
        // Poznámky přibyly 3. 9. (`B117`): do té doby si uživatel legendu skládal dynamickou
        // tabulkou, takže nedostal inverzní box negativních značek ani řazení podle typu.
        var typy = AgendaCapabilities.ScheduleElementTypes("LINE");

        Assert.Contains(ElementTypes.LineTable, typy);
        Assert.Contains(ElementTypes.NotesLegend, typy);
    }

    [Fact]
    public void LinkovyJizdniRad_NemaZastavkovePrvky()
    {
        // Seznam zastávek ani minuty odjezdů do linkového JŘ nepatří - tabulka zastávky × spoje
        // je jiný tvar sestavy a prvky by nad jeho daty nenašly, co čtou.
        var typy = AgendaCapabilities.ScheduleElementTypes("LINE");

        Assert.DoesNotContain(ElementTypes.StopList, typy);
        Assert.DoesNotContain(ElementTypes.DepartureGrid, typy);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("FAKTURY")]
    public void AgendaBezJizdnichRadu_NemaZadnyPrvekJizdnihoRadu(string? druh)
    {
        Assert.Empty(AgendaCapabilities.ScheduleElementTypes(druh));
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("Line")]
    public void DruhSeCteBezOhleduNaVelikostPismen(string druh)
    {
        Assert.NotEmpty(AgendaCapabilities.ScheduleElementTypes(druh));
    }

    [Fact]
    public void PrvekPoznamekCteKolekciPoznamek()
    {
        // Vazba, na které stojí povolení prvku u linkových: server posílá kolekci „POZNAMKY"
        // u obou druhů agendy a prvek z ní čte. Kdyby se výchozí zdroj přejmenoval, legenda
        // by u linkových zmizela bez chyby - jen by byla prázdná.
        Assert.Equal("POZNAMKY", new NotesLegendElement().PoznamkyDataSource);
    }
}
