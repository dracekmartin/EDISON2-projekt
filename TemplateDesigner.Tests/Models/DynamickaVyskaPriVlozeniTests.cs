using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Prvky, které rostou z dat, přicházejí z palety **bez výšky**.
/// </summary>
/// <remarks>
/// <para>Nález Jan 7. 9.: legenda poznámek se vkládala s pevnou výškou 25 mm a pevná výška na
/// těchhle prvcích obsah **zmenšuje do fitu**, ne ořezává - legenda se pak vykreslila písmem na
/// 46 % a poznalo se to až z papíru. Tabulka linky přitom bez výšky přicházela už dřív, takže to
/// byla jen nekonzistence.</para>
/// <para>⚠️ **Seznam zastávek a Minuty odjezdů sem nepatří** a není to opomenutí: seznam zastávek
/// si render ořezává na výšku prvku a při prázdné by bral 60 mm ze zástupné hodnoty (obsah nad ni
/// by zmizel), Minuty odjezdů mají volbu „Roztáhnout na výšku", která pevnou výšku potřebuje.</para>
/// </remarks>
public class DynamickaVyskaPriVlozeniTests
{
    [Theory]
    [InlineData(ElementTypes.NotesLegend)]
    [InlineData(ElementTypes.LineTable)]
    public void RostouciPrvek_PrijdeBezVysky(string typ)
    {
        var el = TemplateDesignerViewModel.CreateElementByType(typ, 10, 10);

        Assert.Null(el.Height);
    }

    /// <summary>Šířku dostat musí - ta se z dat nepočítá a bez ní by prvek nešlo uchopit.</summary>
    [Fact]
    public void RostouciPrvek_SirkuDostane()
    {
        var el = TemplateDesignerViewModel.CreateElementByType(ElementTypes.NotesLegend, 0, 0);

        Assert.Equal(190f, el.Width);
    }

    /// <summary>Pojistka, že se prázdná výška nerozlezla i tam, kde ji prvek potřebuje.</summary>
    [Theory]
    [InlineData(ElementTypes.Text)]
    [InlineData(ElementTypes.Rectangle)]
    [InlineData(ElementTypes.StopList)]
    [InlineData(ElementTypes.DepartureGrid)]
    public void OstatniPrvky_VyskuDostanou(string typ)
    {
        var el = TemplateDesignerViewModel.CreateElementByType(typ, 10, 10);

        Assert.NotNull(el.Height);
    }
}
