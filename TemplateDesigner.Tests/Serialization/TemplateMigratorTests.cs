using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Testy TemplateMigrator po T3 opravách (forward-compat guard, hlášení chyb migrace,
/// sémantické porovnání verzí). Pipeline se testuje přes injektované fake migrace.
/// </summary>
public class TemplateMigratorTests
{
    private static JsonNode Json(string? version) =>
        JsonNode.Parse(version is null
            ? """{ "name": "Test" }"""
            : $$"""{ "version": "{{version}}", "name": "Test" }""")!;

    // ── Základní chování ─────────────────────────────────────────────────────

    [Fact]
    public void AktualniVerze_ProjdeBezeZmeny()
    {
        var (migrated, wasMigrated, original) = new TemplateMigrator().Migrate(Json(TemplateMigrator.CurrentVersion));

        Assert.False(wasMigrated);
        Assert.Equal(TemplateMigrator.CurrentVersion, original);
        Assert.Equal("Test", migrated["name"]!.GetValue<string>());
    }

    [Fact]
    public void ChybejiciVerze_SePovazujeZa100()
    {
        // Šablony bez "version" (před-versioningová éra) se čtou jako "1.0.0". Od bumpu na 1.1.0
        // (highlightRules migrace) jsou starší než aktuální → přerazítkují se a hlásí migraci.
        var (_, wasMigrated, original) = new TemplateMigrator().Migrate(Json(null));

        Assert.Equal("1.0.0", original);
        Assert.True(wasMigrated);
    }

    [Fact]
    public void StarsiVerze_SeOznaciJakoMigrovanaAVerzeSePrerazitkuje()
    {
        var (migrated, wasMigrated, original) = new TemplateMigrator().Migrate(Json("0.9.0"));

        Assert.True(wasMigrated);   // uživatel dostane warning „uložením přepíšeš starou verzi"
        Assert.Equal("0.9.0", original);
        Assert.Equal(TemplateMigrator.CurrentVersion, migrated["version"]!.GetValue<string>());
    }

    [Fact]
    public void VerzeBezPatchSegmentu_JeEkvivalentniAktualni()
    {
        // Sémantické porovnání (VersionComparer): "1.3" == "1.3.0" (CurrentVersion) → žádná migrace ani warning
        var (_, wasMigrated, _) = new TemplateMigrator().Migrate(Json("1.3"));

        Assert.False(wasMigrated);
    }

    // ── Přejmenování agendy KNIZNI_JR_* → LINKOVY_JR_* (1.1.0 → 1.2.0) ───────

    [Fact]
    public void KnizniAgenda_SePremapujeNaLinkovou()
    {
        var root = JsonNode.Parse("""{ "version": "1.1.0", "agendaCode": "KNIZNI_JR_MHD" }""")!;
        var (migrated, wasMigrated, _) = new TemplateMigrator().Migrate(root);

        Assert.True(wasMigrated);
        Assert.Equal("LINKOVY_JR_MHD", migrated["agendaCode"]!.GetValue<string>());
    }

    [Fact]
    public void NelinkovaAgenda_ZustavaBezeZmeny()
    {
        var root = JsonNode.Parse("""{ "version": "1.1.0", "agendaCode": "ZASTAVKOVY_JR_AD" }""")!;
        var (migrated, _, _) = new TemplateMigrator().Migrate(root);

        Assert.Equal("ZASTAVKOVY_JR_AD", migrated["agendaCode"]!.GetValue<string>());
    }

    // ── Highlight rules migrace (1.0.0 → 1.1.0) ──────────────────────────────

    [Fact]
    public void HighlightRules_StaraPoleSePreklopiNaSeznam()
    {
        var json = JsonNode.Parse("""
        {
          "version": "1.0.0",
          "sections": [ { "elements": [
            { "type": "DynamicTableElement", "rowHighlightExpression": "{CENA} < 0", "rowHighlightColor": "#FFFFCCCC" }
          ] } ]
        }
        """)!;

        var (migrated, wasMigrated, _) = new TemplateMigrator().Migrate(json);

        Assert.True(wasMigrated);
        var el = migrated["sections"]![0]!["elements"]![0]!;
        Assert.Null(el["rowHighlightExpression"]);   // stará pole odstraněna
        Assert.Null(el["rowHighlightColor"]);
        var rules = el["highlightRules"]!.AsArray();
        Assert.Single(rules);
        Assert.Equal("{CENA} < 0", rules[0]!["condition"]!.GetValue<string>());
        Assert.Equal("#FFFFCCCC", rules[0]!["backgroundColor"]!.GetValue<string>());
    }

    [Fact]
    public void HighlightRules_PrazdnaPodminka_ZadnySeznam()
    {
        var json = JsonNode.Parse("""
        { "version": "1.0.0", "sections": [ { "elements": [ { "type": "TextElement" } ] } ] }
        """)!;

        var (migrated, _, _) = new TemplateMigrator().Migrate(json);

        Assert.Null(migrated["sections"]![0]!["elements"]![0]!["highlightRules"]);
    }

    [Fact]
    public void HighlightRules_PodminkaBezBarvy_NevytvoriPravidlo()
    {
        // Staré zvýraznění vyžadovalo podmínku I barvu → bez barvy nic nebarvilo; migrace nevytvoří
        // „dangling" bezbarevné pravidlo, jen odstraní staré pole.
        var json = JsonNode.Parse("""
        { "version": "1.0.0", "sections": [ { "elements": [
          { "type": "DynamicTableElement", "rowHighlightExpression": "{CENA} < 0" }
        ] } ] }
        """)!;

        var (migrated, _, _) = new TemplateMigrator().Migrate(json);

        var el = migrated["sections"]![0]!["elements"]![0]!;
        Assert.Null(el["highlightRules"]);
        Assert.Null(el["rowHighlightExpression"]);
    }

    // ── Forward-compat guard (R5) ────────────────────────────────────────────

    [Fact]
    public void NovejsiVerze_SeOdmitneSrozumitelnouChybou()
    {
        // Šablona z novější verze aplikace se NESMÍ tiše přerazítkovat dolů —
        // starší build by neznámé properties zahodil a uložením nenávratně smazal.
        var ex = Assert.Throws<TemplateDeserializationException>(
            () => new TemplateMigrator().Migrate(Json("99.0.0")));

        Assert.Contains("99.0.0", ex.Message);
        Assert.Contains(TemplateMigrator.CurrentVersion, ex.Message);
    }

    // ── Pipeline s injektovanými migracemi ───────────────────────────────────

    private sealed class FakeMigration(string fromVersion, Func<JsonNode, JsonNode> migrate) : ITemplateMigration
    {
        public string FromVersion => fromVersion;
        public string Description => $"fake {fromVersion}";
        public JsonNode Migrate(JsonNode root) => migrate(root);
    }

    [Fact]
    public void MigracePipeline_TransformujeJsonAPrerazitkuje()
    {
        var migrator = new TemplateMigrator([
            new FakeMigration("0.9.0", root => { root["renamed"] = root["name"]!.GetValue<string>(); return root; })
        ]);

        var (migrated, wasMigrated, _) = migrator.Migrate(Json("0.9.0"));

        Assert.True(wasMigrated);
        Assert.Equal("Test", migrated["renamed"]!.GetValue<string>());
        Assert.Equal(TemplateMigrator.CurrentVersion, migrated["version"]!.GetValue<string>());
    }

    [Fact]
    public void MigracePipeline_AplikujeSePodlePoradiVerzi()
    {
        var order = new List<string>();
        var migrator = new TemplateMigrator([
            new FakeMigration("0.9.5", root => { order.Add("0.9.5"); return root; }),
            new FakeMigration("0.9.0", root => { order.Add("0.9.0"); return root; }),
        ]);

        migrator.Migrate(Json("0.9.0"));

        Assert.Equal(["0.9.0", "0.9.5"], order);
    }

    [Fact]
    public void MigraceProNovejsiVstup_SePreskoci()
    {
        // Migrace s FromVersion < verze šablony se neaplikuje (šablona už je za ní)
        bool applied = false;
        var migrator = new TemplateMigrator([
            new FakeMigration("0.5.0", root => { applied = true; return root; })
        ]);

        migrator.Migrate(Json("0.9.0"));

        Assert.False(applied);
    }

    [Fact]
    public void SelhaniMigrace_VyhodiChybuMistoTichehoPokracovani()
    {
        // Částečně zmigrovaná šablona se nesmí tiše otevřít (a následně uložit) —
        // dřívější chování (Debug.WriteLine + continue) = poškození dat bez vysvětlení.
        var migrator = new TemplateMigrator([
            new FakeMigration("0.9.0", _ => throw new InvalidOperationException("rozbité pole X"))
        ]);

        var ex = Assert.Throws<TemplateDeserializationException>(() => migrator.Migrate(Json("0.9.0")));

        Assert.Contains("rozbité pole X", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }
}
