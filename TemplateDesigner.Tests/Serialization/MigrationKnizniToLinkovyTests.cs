using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Testy migrace 1.1.0 → 1.2.0 (<see cref="MigrationKnizniToLinkovy"/>): kód agendy
/// <c>KNIZNI_JR_*</c> → <c>LINKOVY_JR_*</c> po přejmenování agendy. Bez převodu by stará šablona
/// svou agendu v registru nenašla a otevřela by se bez schématu (žádná nabídka datových polí,
/// prázdný náhled) - a kód agendy je persistovaný klíč, takže se to nedá opravit jinak než migrací.
/// </summary>
public class MigrationKnizniToLinkovyTests
{
    private static string? AgendaCode(JsonNode root) =>
        root["agendaCode"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static JsonNode Migrate(string json) =>
        new MigrationKnizniToLinkovy().Migrate(JsonNode.Parse(json)!);

    [Theory]
    [InlineData("KNIZNI_JR_AD",  "LINKOVY_JR_AD")]
    [InlineData("KNIZNI_JR_MHD", "LINKOVY_JR_MHD")]
    public void StaryKodAgendy_SePrejmenuje(string before, string after)
    {
        var root = Migrate($$"""{ "agendaCode": "{{before}}" }""");

        Assert.Equal(after, AgendaCode(root));
    }

    [Theory]
    [InlineData("LINKOVY_JR_AD")]        // už převedené
    [InlineData("ZASTAVKOVY_JR_AD")]     // jiná agenda
    [InlineData("GENERAL")]              // šablona bez vazby na databázi
    [InlineData("")]
    public void OstatniKody_ZustanouBezeZmeny(string code)
    {
        var root = Migrate($$"""{ "agendaCode": "{{code}}" }""");

        Assert.Equal(code, AgendaCode(root));
    }

    [Fact]
    public void OpakovanaMigrace_DavaStejnyVysledek()
    {
        var once  = Migrate("""{ "agendaCode": "KNIZNI_JR_MHD" }""");
        var twice = new MigrationKnizniToLinkovy().Migrate(once);

        Assert.Equal("LINKOVY_JR_MHD", AgendaCode(twice));
    }

    [Fact]
    public void SablonaBezKoduAgendy_Nespadne()
    {
        var root = Migrate("""{ "version": "1.1.0" }""");

        Assert.Null(AgendaCode(root));
    }

    /// <summary>Poškozená šablona (číslo místo textu) nesmí shodit otevření.</summary>
    [Fact]
    public void NetextovyKodAgendy_Nespadne()
    {
        var root = Migrate("""{ "agendaCode": 42 }""");

        Assert.Equal(42, root["agendaCode"]!.GetValue<int>());
    }

    [Fact]
    public void ZbytekSablonySeNedotkne()
    {
        var root = Migrate("""
            { "agendaCode": "KNIZNI_JR_AD", "name": "Knižní JŘ linky 304",
              "sections": [ { "name": "Hlavička" } ] }
            """);

        Assert.Equal("Knižní JŘ linky 304", root["name"]!.GetValue<string>());
        Assert.Equal("Hlavička", root["sections"]![0]!["name"]!.GetValue<string>());
    }
}
