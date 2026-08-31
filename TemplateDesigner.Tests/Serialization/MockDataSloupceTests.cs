using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Ukázková data náhledu musí respektovat <b>definici sloupců ze šablony</b> (#13158).
/// </summary>
/// <remarks>
/// <para>Kdo si nadefinoval vlastní sloupce a neměl vybranou konkrétní linku, neviděl v návrháři
/// žádnou změnu - ukázková data se ze serveru tahala <b>bez jakýchkoli voleb</b>, takže server
/// spadl na rozvržení podle linky. Vypadalo to jako nefunkční featura, přestože s vybranou linkou
/// fungovala.</para>
/// <para>Druhá polovina té pasti byla <b>cache</b>: držela se pod samotným kódem agendy, takže i po
/// opravě vstupu by se po změně sloupců vrátil starý vzorek. Proto je definice součástí klíče.</para>
/// <para>Testuje se to, co jde bez serveru - složení vstupu a klíč cache. Že se vzorek opravdu
/// překreslí, je klikací cesta v návrháři.</para>
/// </remarks>
public class MockDataSloupceTests
{
    private const string Definice = """[{"key":"PRAC","dny":"12345"},{"key":"VIKEND","dny":"67+"}]""";

    // ── vstup pro server ──────────────────────────────────────────────────────

    /// <summary>Definice sloupců se serveru pošle - bez ní staví vzorek podle linky.</summary>
    [Fact]
    public void DefiniceSloupcu_SePosilaServeru()
    {
        var input = CacheAgendaProvider.MockDataInput(Definice);

        Assert.Equal(Definice, input["DEFINICE_SLOUPCU"]?.Value?.ToString());
    }

    /// <summary>
    /// Bez vlastních sloupců se neposílá nic - server má zůstat v režimu „podle linky".
    /// </summary>
    /// <remarks>
    /// ⚠️ Měří se <b>výčtem klíčů</b>, ne čtením indexerem: `CVar` si na chybějící klíč uzel vyrobí,
    /// takže `input["DEFINICE_SLOUPCU"]` není nikdy null a test by prošel i s poslaným prázdnem.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BezVlastnichSloupcu_SeKlicNeposila(string? definice)
    {
        var input = CacheAgendaProvider.MockDataInput(definice);

        Assert.DoesNotContain(input, kvp => kvp.Key == "DEFINICE_SLOUPCU");
    }

    // ── cache ────────────────────────────────────────────────────────────────

    /// <summary>Jiná definice = jiný klíč, takže se vzorek načte znovu.</summary>
    [Fact]
    public void ZmenaDefinice_ZmeniKlicCache()
    {
        string bez = CacheAgendaProvider.MockCacheKey("ZASTAVKOVY_JR_AD", null);
        string s = CacheAgendaProvider.MockCacheKey("ZASTAVKOVY_JR_AD", Definice);
        string jina = CacheAgendaProvider.MockCacheKey("ZASTAVKOVY_JR_AD", """[{"key":"VSE"}]""");

        Assert.NotEqual(bez, s);
        Assert.NotEqual(s, jina);
    }

    /// <summary>
    /// Beze změny definice zůstává klíč shodný - cache se musí dál uplatnit.
    /// </summary>
    /// <remarks>
    /// Cache existuje proto, že načtení ukázkových dat je synchronní a trvá pár vteřin. Kdyby se
    /// klíč lišil pokaždé, tekl by ten cold-start do každého překreslení náhledu.
    /// </remarks>
    [Fact]
    public void StejnaDefinice_DaStejnyKlic()
    {
        Assert.Equal(
            CacheAgendaProvider.MockCacheKey("ZASTAVKOVY_JR_AD", Definice),
            CacheAgendaProvider.MockCacheKey("ZASTAVKOVY_JR_AD", Definice));
    }

    /// <summary>Dvě agendy se nesmí potkat v jednom klíči, ani když mají stejnou definici.</summary>
    [Fact]
    public void RuzneAgendy_MajiRuznyKlic()
    {
        Assert.NotEqual(
            CacheAgendaProvider.MockCacheKey("ZASTAVKOVY_JR_AD", Definice),
            CacheAgendaProvider.MockCacheKey("ZASTAVKOVY_JR_MHD", Definice));
    }
}
