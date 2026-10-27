using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// `B194e` A2: agenda ze stromu EDISONu říká jen modul (JŘ / MHD) - návrhář otevře tu agendu téhož
/// modulu, se kterou uživatel pracoval naposledy. Do 27. 10. vždy zastávkový JŘ a kdo dělal linkové,
/// musel se po každém spuštění přepnout.
/// </summary>
public class StartovniAgendaTests
{
    private static readonly List<AgendaRegistryEntry> Agendy =
    [
        new("ZASTAVKOVY_JR_AD", "x", "c", Variant: "AD"),
        new("LINKOVY_JR_AD", "x", "c", Variant: "AD"),
        new("LINKOVY_JR_AD_ROZDILOVY", "x", "c", Variant: "AD_ROZDILOVY", Released: false),
        new("ZASTAVKOVY_JR_MHD", "x", "c", Variant: "MHD"),
        new("LINKOVY_JR_MHD", "x", "c", Variant: "MHD"),
        new("GENERAL", "x", ""),
    ];

    [Theory]
    // Uzel JŘ: naposledy linkový AD → linkový AD; napoprvé zastávkový ze shellu.
    [InlineData("ZASTAVKOVY_JR_AD", true, "LINKOVY_JR_AD", "LINKOVY_JR_AD")]
    [InlineData("ZASTAVKOVY_JR_AD", true, null, "ZASTAVKOVY_JR_AD")]
    // Uzel MHD: poslední agenda z jiného modulu se nepoužije, platí ta ze shellu.
    [InlineData("ZASTAVKOVY_JR_MHD", true, "LINKOVY_JR_AD", "ZASTAVKOVY_JR_MHD")]
    [InlineData("ZASTAVKOVY_JR_MHD", true, "LINKOVY_JR_MHD", "LINKOVY_JR_MHD")]
    // Obecná agenda žádný modul nemá.
    [InlineData("ZASTAVKOVY_JR_AD", true, "GENERAL", "ZASTAVKOVY_JR_AD")]
    // Spuštění s linkou (porovnání linek) určuje agendu přesně.
    [InlineData("LINKOVY_JR_AD_ROZDILOVY", false, "LINKOVY_JR_AD", "LINKOVY_JR_AD_ROZDILOVY")]
    [InlineData("ZASTAVKOVY_JR_AD", false, "LINKOVY_JR_AD", "ZASTAVKOVY_JR_AD")]
    // Bez shellu poslední agenda; skrytá se neobnoví.
    [InlineData(null, false, "LINKOVY_JR_MHD", "LINKOVY_JR_MHD")]
    [InlineData(null, false, "LINKOVY_JR_AD_ROZDILOVY", null)]
    // Neznámý kód ze shellu se přeskočí.
    [InlineData("NEZNAMA", true, "LINKOVY_JR_AD", "LINKOVY_JR_AD")]
    public void Vyber(string? zeShellu, bool jenModul, string? posledni, string? ocekavana) =>
        Assert.Equal(ocekavana, TemplateDesignerViewModel.StartovniAgenda(Agendy, zeShellu, jenModul, posledni));

    /// <summary>Skrytá agenda jako poslední použitá se nevrátí ani přes modul (nález Jan 1. 9.).</summary>
    [Fact]
    public void SkrytaPosledniSeNevrati()
    {
        var agendy = Agendy.Select(a => a.Code == "LINKOVY_JR_AD" ? a with { Released = false } : a).ToList();

        Assert.Equal("ZASTAVKOVY_JR_AD", TemplateDesignerViewModel.StartovniAgenda(agendy, "ZASTAVKOVY_JR_AD", true, "LINKOVY_JR_AD"));
    }
}
