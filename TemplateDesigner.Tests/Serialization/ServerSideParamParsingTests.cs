using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Příznak <c>serverSide</c> z <c>GetParameters</c> - podle něj klient pozná, že změna hodnoty
/// vyžaduje načtení dat, ne jen překreslení náhledu. Do 21. 9. si ten seznam držel klient natvrdo.
/// </summary>
/// <remarks>
/// Serverovou stranu hlídá <see cref="ServerSideParamsDriftTests"/> (parametr čtený v GetData musí
/// mít příznak), tenhle test drží druhou půlku cesty: že příznak přežije parsování odpovědi.
/// </remarks>
public class ServerSideParamParsingTests
{
    [Fact]
    public void PriznakZOdpovedi_SeNacte()
    {
        // Server posílá číslo (%DynamicObject s hodnotou 1), ne JSON bool.
        var parametry = CacheAgendaProvider.ParseParameters(
            """[{"name":"ROZDELENI_SLOUPCU","label":"Rozdělení sloupců","type":"enum","serverSide":1}]""");

        Assert.True(Assert.Single(parametry).ServerSide);
    }

    [Fact]
    public void BezPriznaku_JeParametrKlientsky()
    {
        // Starší server příznak neposílá vůbec (aditivní klíč kontraktu) - parametr se pak chová
        // jako klientský, tedy se jen překreslí náhled.
        var parametry = CacheAgendaProvider.ParseParameters(
            """[{"name":"CASOVA_OSA","label":"Časová osa","type":"enum"}]""");

        Assert.False(Assert.Single(parametry).ServerSide);
    }

    [Fact]
    public void PriznakJakoBool_SeTakyNacte()
    {
        // Tolerance pro agendu, která by příznak zapsala jako JSON bool - stejný důvod, proč server
        // čte bool hodnoty parametrů tolerantně.
        var parametry = CacheAgendaProvider.ParseParameters(
            """[{"name":"OBOUSMERNY_TISK","label":"Obousměrný tisk","type":"bool","serverSide":true}]""");

        Assert.True(Assert.Single(parametry).ServerSide);
    }
}
