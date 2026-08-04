using System;
using System.IO;
using EDControls.UserFonts;

namespace EDControls.Tests;

/// <summary>
/// UserFontPreference je shared-file-by-design (cross-process sync mezi shell a TD).
/// FileSystemWatcher debounce je integration territory (FontService smoke test v produkci);
/// tady se ověřuje wire kontrakt font.json — přesný camelCase tvar na disku — přes přesměrovanou
/// <see cref="UserFontPreference.FilePath"/> (internal seam), ne přes reálné %APPDATA%.
/// </summary>
public class UserFontPreferenceTests
{
    /// <summary>Spustí <paramref name="body"/> s <see cref="UserFontPreference.FilePath"/>
    /// přesměrovanou na dočasný soubor; po dokončení cestu obnoví a načte reálný stav.</summary>
    private static void WithTempFontFile(string? initialContent, Action<string> body)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"edfont_{Guid.NewGuid():N}.json");
        var saved = UserFontPreference.FilePath;
        try
        {
            if (initialContent != null) File.WriteAllText(tmp, initialContent);
            UserFontPreference.FilePath = tmp;
            body(tmp);
        }
        finally
        {
            UserFontPreference.FilePath = saved;
            if (File.Exists(tmp)) File.Delete(tmp);
            UserFontPreference.Load();   // obnovit Current z reálného souboru
        }
    }

    [Fact]
    public void Load_DeserializesOnDiskCamelCaseShape()
    {
        // Pinned tvar sdílený s TemplateDesigner — přejmenování property (bez JsonPropertyName)
        // nebo změna naming policy tenhle kontrakt rozbije.
        WithTempFontFile("""{"fontApplied":true,"fontFamily":"Calibri","fontSize":14.5,"version":1}""", _ =>
        {
            UserFontPreference.Load();

            Assert.True(UserFontPreference.Current.FontApplied);
            Assert.Equal("Calibri", UserFontPreference.Current.FontFamily);
            Assert.Equal(14.5, UserFontPreference.Current.FontSize);
        });
    }

    [Fact]
    public void Load_LegacyFileWithoutVersion_DropsSizeKeepsWhitelistedFamily()
    {
        // Velikost bez verze pochází z buildu, který pt vykresloval jako px — uživatel ji
        // ladil podle výsledku, takže po opravě nedává smysl. Rodina chybou zasažená nebyla.
        WithTempFontFile("""{"fontApplied":true,"fontFamily":"Calibri","fontSize":10}""", _ =>
        {
            UserFontPreference.Load();

            Assert.Null(UserFontPreference.Current.FontSize);
            Assert.Equal("Calibri", UserFontPreference.Current.FontFamily);
            Assert.True(UserFontPreference.Current.FontApplied);
        });
    }

    [Fact]
    public void Load_LegacyFileWithSymbolFont_DropsOverrideEntirely()
    {
        // Původní systémový picker pustil i symbolové fonty — whitelist je má uklidit.
        WithTempFontFile("""{"fontApplied":true,"fontFamily":"Wingdings","fontSize":10}""", _ =>
        {
            UserFontPreference.Load();

            Assert.False(UserFontPreference.Current.FontApplied);
            Assert.Null(UserFontPreference.Current.FontFamily);
        });
    }

    [Fact]
    public void Save_StampsVersion_SoNextLoadKeepsSize()
    {
        // Bez razítka by MigrateLegacy zahodila i velikost právě zvolenou v dialogu.
        WithTempFontFile(null, _ =>
        {
            new UserFontPreference { FontApplied = true, FontFamily = "Calibri", FontSize = 12 }.Save();
            UserFontPreference.Load();

            Assert.Equal(12, UserFontPreference.Current.FontSize);
        });
    }

    [Fact]
    public void Save_WritesCamelCaseKeys()
    {
        WithTempFontFile(null, tmp =>
        {
            new UserFontPreference { FontApplied = true, FontFamily = "Calibri", FontSize = 14.5 }.Save();

            var json = File.ReadAllText(tmp);
            Assert.Contains("\"fontApplied\"", json);
            Assert.Contains("\"fontFamily\"", json);
            Assert.Contains("\"fontSize\"", json);
        });
    }

    [Fact]
    public void Load_MalformedJson_DoesNotThrow_KeepsCurrentNonNull()
    {
        // Filesystem corruption / partial write — Load catch blok drží Current jako default
        // / poslední úspěšný stav, aby aplikace nepadla na startu kvůli rozbitému font.json.
        WithTempFontFile("{ not valid json", _ =>
        {
            var ex = Record.Exception(UserFontPreference.Load);
            Assert.Null(ex);
            Assert.NotNull(UserFontPreference.Current);
        });
    }
}
