using System;
using System.IO;
using EDControls.UserFonts;

namespace EDControls.Tests;

/// <summary>
/// UserFontPreference je shared-file-by-design (cross-process sync mezi shell a TD).
/// Watcher hlídá <see cref="SharedJsonPreferenceTests"/>; tady se ověřuje wire kontrakt font.json -
/// přesný camelCase tvar na disku a migrace schématu - nad vlastním úložištěm
/// (<see cref="UserFontPreference.CreateStore"/>) v tempu, ne nad statickým stavem procesu.
/// </summary>
public class UserFontPreferenceTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"edfont_{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_file)) File.Delete(_file);
    }

    private SharedJsonPreference<UserFontPreference> StoreWith(string? initialContent)
    {
        if (initialContent != null) File.WriteAllText(_file, initialContent);
        return UserFontPreference.CreateStore(_file);
    }

    [Fact]
    public void Load_DeserializesOnDiskCamelCaseShape()
    {
        // Pinned tvar sdílený s TemplateDesigner - přejmenování property (bez JsonPropertyName)
        // nebo změna naming policy tenhle kontrakt rozbije.
        var store = StoreWith("""{"fontApplied":true,"fontFamily":"Calibri","fontSize":14.5,"version":1}""");

        store.Load();

        Assert.True(store.Current.FontApplied);
        Assert.Equal("Calibri", store.Current.FontFamily);
        Assert.Equal(14.5, store.Current.FontSize);
    }

    [Fact]
    public void Load_LegacyFileWithoutVersion_DropsSizeKeepsWhitelistedFamily()
    {
        // Velikost bez verze pochází z buildu, který pt vykresloval jako px - uživatel ji
        // ladil podle výsledku, takže po opravě nedává smysl. Rodina chybou zasažená nebyla.
        var store = StoreWith("""{"fontApplied":true,"fontFamily":"Calibri","fontSize":10}""");

        store.Load();

        Assert.Null(store.Current.FontSize);
        Assert.Equal("Calibri", store.Current.FontFamily);
        Assert.True(store.Current.FontApplied);
    }

    [Fact]
    public void Load_LegacyFileWithSymbolFont_DropsOverrideEntirely()
    {
        // Původní systémový picker pustil i symbolové fonty - whitelist je má uklidit.
        var store = StoreWith("""{"fontApplied":true,"fontFamily":"Wingdings","fontSize":10}""");

        store.Load();

        Assert.False(store.Current.FontApplied);
        Assert.Null(store.Current.FontFamily);
    }

    [Fact]
    public void Save_StampsVersion_SoNextLoadKeepsSize()
    {
        // Bez razítka by MigrateLegacy zahodila i velikost právě zvolenou v dialogu.
        var store = StoreWith(null);

        new UserFontPreference { FontApplied = true, FontFamily = "Calibri", FontSize = 12 }.SaveTo(store);
        store.Load();

        Assert.Equal(12, store.Current.FontSize);
    }

    [Fact]
    public void Save_WritesCamelCaseKeys()
    {
        var store = StoreWith(null);

        new UserFontPreference { FontApplied = true, FontFamily = "Calibri", FontSize = 14.5 }.SaveTo(store);

        var json = File.ReadAllText(_file);
        Assert.Contains("\"fontApplied\"", json);
        Assert.Contains("\"fontFamily\"", json);
        Assert.Contains("\"fontSize\"", json);
    }

    [Fact]
    public void Load_MalformedJson_DoesNotThrow_KeepsCurrentNonNull()
    {
        // Filesystem corruption / partial write - Load catch blok drží Current jako default
        // / poslední úspěšný stav, aby aplikace nepadla na startu kvůli rozbitému font.json.
        var store = StoreWith("{ not valid json");

        var ex = Record.Exception(store.Load);

        Assert.Null(ex);
        Assert.NotNull(store.Current);
    }
}
