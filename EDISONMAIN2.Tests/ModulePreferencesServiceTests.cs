using System.Text.Json;
using System.Text.Json.Nodes;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Kontrakty persistence stavu stromu: chybějící blob vs. uložené prázdno (na tom stojí
/// "ponech serverové výchozí rozbalení"), degradace staré hodnoty a polykání chyb.
/// </summary>
public class ModulePreferencesServiceTests
{
    private const string Scope = "EDISONMAIN2";

    private readonly IUserPrefsService _prefs = Substitute.For<IUserPrefsService>();
    private readonly ModulePreferencesService _service;

    public ModulePreferencesServiceTests() => _service = new ModulePreferencesService(_prefs);

    private void PrefsReturn(string key, JsonNode? node) =>
        _prefs.LoadAsync(Scope, key).Returns(Task.FromResult(node));

    [Fact]
    public async Task LoadExpansion_NeverSaved_ReturnsNull()
    {
        // null = nikdy neuloženo → caller ponechá serverové výchozí rozbalení
        PrefsReturn("TreeExpansion", null);

        Assert.Null(await _service.LoadExpansionAsync());
    }

    [Fact]
    public async Task LoadExpansion_SavedEmptyList_ReturnsEmptyNotNull()
    {
        // Uživatel vše sbalil a uložil - prázdný list je platný stav, ne "nikdy neuloženo"
        PrefsReturn("TreeExpansion", new JsonArray());

        var expansion = await _service.LoadExpansionAsync();

        Assert.NotNull(expansion);
        Assert.Empty(expansion);
    }

    [Fact]
    public async Task SaveExpansion_WritesListUnderTreeExpansionKey()
    {
        await _service.SaveExpansionAsync(["main|0", "main|0|2"]);

        await _prefs.Received(1).SaveAsync(Scope, "TreeExpansion",
            Arg.Is<JsonNode>(n => n.ToJsonString(null) == """["main|0","main|0|2"]"""));
    }

    [Fact]
    public async Task LoadShortcutsUsageSnooze_MissingBlobs_DegradeToEmpty()
    {
        PrefsReturn("Shortcuts", null);
        PrefsReturn("ModuleUsage", null);
        PrefsReturn("ChangelogSnooze", null);

        Assert.Empty((await _service.LoadShortcutsAsync())!);
        Assert.Empty(await _service.LoadUsageAsync());
        Assert.Empty(await _service.LoadChangelogSnoozeAsync());
    }

    [Fact]
    public async Task LoadSelectedTab_OldNumericValue_DegradesToNull()
    {
        // Dřív se ukládal index záložky - stará číselná hodnota nesmí spadnout, jen se zahodí
        PrefsReturn("SelectedTab", JsonValue.Create(2));

        Assert.Null(await _service.LoadSelectedTabAsync());
    }

    [Fact]
    public async Task LoadSelectedTab_StoredKey_ReturnsIt()
    {
        PrefsReturn("SelectedTab", JsonValue.Create("desktop"));

        Assert.Equal("desktop", await _service.LoadSelectedTabAsync());
    }

    [Fact]
    public async Task SaveUsage_RoundTripsThroughLoad()
    {
        JsonNode? saved = null;
        await _prefs.SaveAsync(Scope, "ModuleUsage", Arg.Do<JsonNode>(n => saved = n));
        var stamp = new DateTime(2026, 9, 8, 10, 0, 0, DateTimeKind.Utc);
        await _service.SaveUsageAsync(new Dictionary<string, UsageEntry>
        {
            ["66001"] = new(Count: 3, LastUsedUtc: stamp),
        });
        PrefsReturn("ModuleUsage", JsonNode.Parse(saved!.ToJsonString(null)));

        var usage = await _service.LoadUsageAsync();

        Assert.Equal(new UsageEntry(3, stamp), usage["66001"]);
    }

    [Fact]
    public async Task Load_PrefsThrow_DegradesInsteadOfThrowing()
    {
        _prefs.LoadAsync(Scope, Arg.Any<string>())
            .Returns<Task<JsonNode?>>(_ => throw new InvalidOperationException("server down"));

        // Plocha je výjimka: null hlásí selhání, aby ji VM nepřepsala prázdnem; zbytek degraduje.
        Assert.Null(await _service.LoadShortcutsAsync());
        Assert.Empty(await _service.LoadUsageAsync());
        Assert.Null(await _service.LoadExpansionAsync());
        Assert.Null(await _service.LoadSelectedTabAsync());
    }

    [Fact]
    public async Task Save_PrefsThrow_Swallowed()
    {
        _prefs.SaveAsync(Scope, Arg.Any<string>(), Arg.Any<JsonNode>())
            .Returns(_ => throw new InvalidOperationException("server down"));

        await _service.SaveSelectedTabAsync("desktop");
        await _service.SaveExpansionAsync([]);
        await _service.SaveChangelogSnoozeAsync(new Dictionary<string, string> { ["AD"] = "hash" });
    }
}
