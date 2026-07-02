using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Testy TemplateMigrator. Část testů dokumentuje SOUČASNÉ chování,
/// část (Skip = "T3") popisuje ŽÁDOUCÍ chování — odskipovat spolu s T3 opravami
/// (forward-compat guard, hlášení chyb migrace). Viz analyza-stav-a-legacy-nahrada-2026-07.md §T3.
/// </summary>
public class TemplateMigratorTests
{
    private static JsonNode Json(string? version) =>
        JsonNode.Parse(version is null
            ? """{ "name": "Test" }"""
            : $$"""{ "version": "{{version}}", "name": "Test" }""")!;

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
        // ⚠️ Dokumentované současné chování: šablony bez "version" (před-versioningová éra,
        // vč. flat-TextStyle formátu) jsou nerozlišitelné od aktuálních — případná budoucí
        // migrace V0→V1 by na ně nedosáhla. Viz T3 bod 3 (heuristika / rozhodnutí o V1).
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
    public void VerzeBezPatchSegmentu_SeNormalizujeAOznaciJakoMigrovana()
    {
        // Nález z testování: early-out v Migrate porovnává verze STRING equality
        // ("1.0" != "1.0.0"), ne VersionComparerem — sémanticky shodná verze projde
        // pipeline a označí se WasMigrated (= UI warning). Neškodné (verze se tím
        // normalizuje), ale při T3 zvážit porovnání VersionComparerem.
        var (migrated, wasMigrated, original) = new TemplateMigrator().Migrate(Json("1.0"));

        Assert.True(wasMigrated);
        Assert.Equal("1.0", original);
        Assert.Equal(TemplateMigrator.CurrentVersion, migrated["version"]!.GetValue<string>());
    }

    [Fact(Skip = "T3: forward-compat guard zatím neimplementován — šablona z novější verze " +
                 "aplikace se dnes tiše přerazítkuje na nižší verzi (riziko tiché ztráty dat). " +
                 "Po implementaci má Migrate novější verzi odmítnout.")]
    public void NovejsiVerze_SeOdmitne()
    {
        // Žádoucí chování (R5): verze > CurrentVersion → výjimka / chybový výsledek,
        // ne tiché snížení verze. Přesný kontrakt (exception vs. result flag) určí T3.
        Assert.ThrowsAny<Exception>(() => new TemplateMigrator().Migrate(Json("99.0.0")));
    }

    [Fact]
    public void SoucasneChovani_NovejsiVerze_SeTisePrerazitkuje()
    {
        // Charakterizační test současného (nežádoucího) chování — po T3 opravě
        // tento test SMAZAT a odskipovat NovejsiVerze_SeOdmitne výše.
        var (migrated, _, original) = new TemplateMigrator().Migrate(Json("99.0.0"));

        Assert.Equal("99.0.0", original);
        Assert.Equal(TemplateMigrator.CurrentVersion, migrated["version"]!.GetValue<string>());
    }
}
