using System.Text.Json;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Dvouosý model registru agend (agenda × varianta, 2026-07-11): DisplayName, fallback
/// pro starší Caché bez nových klíčů a deserializace wire JSON z AgendyRegistr.ListAgendy.
/// </summary>
public class AgendaRegistryEntryTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void DisplayName_SVariantou_AgendaTeckaVarianta()
    {
        var e = new AgendaRegistryEntry("ZASTAVKOVY_JR_AD", "Zastávkový JŘ (AD)", "cls",
            Agenda: "ZASTAVKOVY_JR", AgendaLabel: "Zastávkový JŘ", Variant: "AD", VariantLabel: "AD");

        Assert.Equal("Zastávkový JŘ · AD", e.DisplayName);
        Assert.Equal("Zastávkový JŘ", e.EffectiveAgendaLabel);
    }

    /// <summary>Uživatel nevidí kód varianty („AD_ROZDILOVY"), ale její popisek (`B194` A1).</summary>
    [Fact]
    public void DisplayName_PouzijePopisekVarianty_NeKod()
    {
        var e = new AgendaRegistryEntry("LINKOVY_JR_AD_ROZDILOVY", "Linkový JŘ (AD_ROZDILOVY)", "cls",
            Agenda: "LINKOVY_JR", AgendaLabel: "Linkový JŘ", Variant: "AD_ROZDILOVY", VariantLabel: "AD - porovnání verzí");

        Assert.Equal("Linkový JŘ · AD - porovnání verzí", e.DisplayName);
    }

    [Fact]
    public void DisplayName_BezVarianty_JenAgenda()
    {
        var e = new AgendaRegistryEntry("FAKTURY", "Faktury", "cls",
            Agenda: "FAKTURY", AgendaLabel: "Faktury");

        Assert.Equal("Faktury", e.DisplayName);
    }

    [Fact]
    public void StarsiCacheBezNovychKlicu_FallbackNaKombinovanyLabel()
    {
        // Starší server neposílá agenda/variant klíče → defaulty "" → fallback na Label.
        var e = new AgendaRegistryEntry("ZASTAVKOVY_JR_AD", "Zastávkový JŘ (AD)", "cls");

        Assert.Equal("Zastávkový JŘ (AD)", e.EffectiveAgendaLabel);
        Assert.Equal("Zastávkový JŘ (AD)", e.DisplayName);
    }

    [Fact]
    public void WireJson_DeserializujeObeOsy()
    {
        // Tvar 1:1 s AgendyRegistr.ListAgendy (staré klíče code/label + nové osy aditivně).
        const string json = """
            [{"code":"ZASTAVKOVY_JR_AD","label":"Zastávkový JŘ (AD)",
              "agenda":"ZASTAVKOVY_JR","agendaLabel":"Zastávkový JŘ",
              "variant":"AD","variantLabel":"AD",
              "cacheClass":"ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd",
              "instanceParamName":"LINKA_ID","elementKind":"STOP",
              "instanceLabel":"linka","instanceLabelPlural":"linky",
              "released":true}]
            """;

        var list = JsonSerializer.Deserialize<List<AgendaRegistryEntry>>(json, Options)!;

        var e = Assert.Single(list);
        Assert.Equal("ZASTAVKOVY_JR_AD", e.Code);
        Assert.Equal(("ZASTAVKOVY_JR", "AD"), (e.Agenda, e.Variant));
        Assert.Equal("Zastávkový JŘ · AD", e.DisplayName);
        Assert.Equal("STOP", e.ElementKind);
        Assert.True(e.Released);
    }

    [Fact]
    public void Released_DefaultTrue_ProStarsiCacheBezKlice()
    {
        // Starší server klíč neposílá → default true = vše viditelné (dnešní chování);
        // nezveřejněná agenda z nového serveru → false (filtruje se z UI výběru).
        var stary = new AgendaRegistryEntry("X", "X", "cls");
        Assert.True(stary.Released);

        const string json = """[{"code":"LINKOVY_JR_AD","label":"Linkový JŘ (AD)","cacheClass":"c","released":false}]""";
        var e = Assert.Single(JsonSerializer.Deserialize<List<AgendaRegistryEntry>>(json, Options)!);
        Assert.False(e.Released);
    }
}
