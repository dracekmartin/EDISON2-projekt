using System.Text.Json;
using EDControls.UserFonts;

namespace EDControls.Tests;

/// <summary>
/// UserFontPreference je shared-file-by-design (cross-process sync mezi shell a TD).
/// Většina chování (FileSystemWatcher debounce, Load/Save přes real disk) je integration
/// territory; pokrývá ji FontService smoke test v produkci. Tady jen POCO contract.
/// </summary>
public class UserFontPreferenceTests
{
    [Fact]
    public void Default_AllPropertiesNullOrFalse()
    {
        var p = new UserFontPreference();

        Assert.False(p.FontApplied);
        Assert.Null(p.FontFamily);
        Assert.Null(p.FontSize);
    }

    [Fact]
    public void JsonRoundtrip_PreservesAllProperties()
    {
        // Slouží jako regression test pro change format — pokud někdo přejmenuje property
        // bez rebuildu (JsonPropertyName attr), tento test selže.
        var original = new UserFontPreference
        {
            FontApplied = true,
            FontFamily = "Calibri",
            FontSize = 14.5,
        };
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<UserFontPreference>(json, options);

        Assert.NotNull(deserialized);
        Assert.True(deserialized.FontApplied);
        Assert.Equal("Calibri", deserialized.FontFamily);
        Assert.Equal(14.5, deserialized.FontSize);
    }

    [Fact]
    public void Current_IsNonNullByDefault()
    {
        // Static singleton se inicializuje na new() — ne null, ne nevyplněný stav.
        Assert.NotNull(UserFontPreference.Current);
    }
}
