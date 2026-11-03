using ALWA.SYS;
using TemplateDesigner.Services;

namespace EDISON2.IntegrationTests;

/// <summary>
/// Příjem šablon od jiných firem (`API.SdileniSablon`, `B189`) přes skutečné MLRest a parser, kterým odpověď čte
/// návrhář (<see cref="ReceivedSummary.Parse"/>). Jen firma bez klíče sdílení: server ISYCloud nevolá, takže test
/// nezávisí na síti ani na testovacím ISYCloudu. Celý tok se ověřuje proti testovacímu ISYCloudu ručně.
/// </summary>
[Trait("Category", "Integration")]
public class SdileniSablonTests
{
    [DevServerFact]
    public void Souhrn_FirmaBezKlice_NulaBezISYCloudu()
    {
        // Vymyšlená firma pro zápisy testů sdílení nikdy nepoužila, klíč nemá.
        var mlRest = DevServer.Connect(DevServer.WriteIdfa, DevServer.WriteMlp);

        var odpoved = mlRest.QueryOrThrow("ALVA.TemplateDesigner.API.SdileniSablon", "GetSouhrnPrijatych", new CVar())["1"]?.Value?.ToString();

        Assert.Equal(ReceivedSummary.None, ReceivedSummary.Parse(odpoved));
        Assert.Contains("\"pocet\":0", odpoved);
    }
}
