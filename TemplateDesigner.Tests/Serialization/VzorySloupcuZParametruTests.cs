using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Popisky vzorů sloupců posílá server u voleb parametru „Rozdělení sloupců" (klíč vzoru v <c>optionKeys</c>).
/// Do 30. 10. je měl klient opsané pro vývěsku i linku zvlášť a musely souhlasit se zamčenými volbami
/// na serveru. Že server klíče posílá, hlídá Caché test kontraktu (<c>KontraktAgendTest</c>).
/// </summary>
public class VzorySloupcuZParametruTests
{
    private const string Parametr = """
        [{"name":"ROZDELENI_SLOUPCU","type":"enum",
          "options":["Automaticky (dle linky)","Pracovní / Sobota / Neděle+svátky","","Pracovní / víkend (So+Ne+svátky)","Vlastní rozložení"],
          "optionKeys":["","PRAC_SO_NE","NIC","PRAC_VIKEND",""]}]
        """;

    [Fact]
    public void VzoryJsouVolbySKlicem()
    {
        var nabidka = DepartureColumnPresets.FromParameter(Assert.Single(CacheAgendaProvider.ParseParameters(Parametr)));

        Assert.Equal(
            [new("PRAC_SO_NE", "Pracovní / Sobota / Neděle+svátky"), new("PRAC_VIKEND", "Pracovní / víkend (So+Ne+svátky)")],
            nabidka);
    }

    /// <summary>Prázdná volba se vyřadí i se svým klíčem - jinak by se klíče posunuly k sousedním volbám.</summary>
    [Fact]
    public void PrazdnaVolbaVyradiISvujKlic()
    {
        var parametr = Assert.Single(CacheAgendaProvider.ParseParameters(Parametr));

        Assert.Equal(parametr.Options!.Count, parametr.OptionKeys!.Count);
        Assert.DoesNotContain("NIC", parametr.OptionKeys);
    }

    /// <summary>Starší server klíče neposílá - vzory se pak nenabízejí, klient si popisky nevymýšlí.</summary>
    [Fact]
    public void BezKlicuNejsouVzory()
    {
        var parametr = Assert.Single(CacheAgendaProvider.ParseParameters(
            """[{"name":"ROZDELENI_SLOUPCU","type":"enum","options":["Automaticky (dle linky)","Pracovní / Sobota / Neděle+svátky"]}]"""));

        Assert.Null(parametr.OptionKeys);
        Assert.Empty(DepartureColumnPresets.FromParameter(parametr));
    }
}
