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
        // ⚠️ Dokumentované chování: šablony bez "version" (před-versioningová éra,
        // vč. flat-TextStyle formátu) jsou nerozlišitelné od aktuálních — případná budoucí
        // migrace V0→V1 by na ně nedosáhla. Viz T3 bod „rozhodnutí o V1" (H-19).
        var (_, wasMigrated, original) = new TemplateMigrator().Migrate(Json(null));

        Assert.Equal("1.0.0", original);
        Assert.False(wasMigrated);
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
        // Sémantické porovnání (VersionComparer): "1.0" == "1.0.0" → žádná migrace ani warning
        var (_, wasMigrated, _) = new TemplateMigrator().Migrate(Json("1.0"));

        Assert.False(wasMigrated);
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
