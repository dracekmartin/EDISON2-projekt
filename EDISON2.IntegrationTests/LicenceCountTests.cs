using EDISONMAIN2;

namespace EDISON2.IntegrationTests;

/// <summary>
/// Počty pro upozornění licence v status baru. Parser chybu i chybějící řádek tiše převede na „počet
/// neznámý" a varování zmizí, proto se hlídá surový výstup. Zaměstnance (<c>gPocZam</c>) test nevolá:
/// server počet přepočítá a zapíše do dat firmy a telemetrie.
/// </summary>
[Trait("Category", "Integration")]
public class LicenceCountTests
{
    [DevServerFact]
    public void VehicleCount_RealServer_ParserMatchesTotalRow()
    {
        var rest = DevServer.Connect();
        var del = rest.Delimiter;
        var raw = rest.QueryOrThrow("ALVA.VOZIDLA.LCC", "PocetProvozovanychVozidel",
            new object[] { $"pro{del}{rest.Idfa}" });

        var rows = WireRows.ByKey(raw, del);
        Assert.True(rows.TryGetValue("celkem", out var total),
            "PocetProvozovanychVozidel nevrátil řádek 'celkem', upozornění na počet vozidel by tiše zmizelo.");
        Assert.True(total.Length > 1, "Řádek 'celkem' nemá hodnotu.");
        Assert.True(int.TryParse(total[1], out var count), "Řádek 'celkem' nemá číselný počet.");

        Assert.Equal(count, LicenceNotice.LoadVehicleCount(rest));
    }
}
