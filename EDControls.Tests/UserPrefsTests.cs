using System.Text.Json.Nodes;
using ALWA.SYS;
using EDControls.UserPrefs;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EDControls.Tests;

/// <summary>
/// Testy <see cref="CacheUserPrefsService"/> + <see cref="NullUserPrefsService"/>.
/// MLRest mockujeme přes NSubstitute - testuje se kontrakt volání ne sítová komunikace.
/// </summary>
public class UserPrefsTests
{
    private static (CacheUserPrefsService Service, IMLRest MockMl) Setup()
    {
        var mlRest = Substitute.For<IMLRest>();
        return (new CacheUserPrefsService(mlRest), mlRest);
    }

    [Fact]
    public async Task Null_LoadReturnsNull()
    {
        var svc = new NullUserPrefsService();
        var result = await svc.LoadAsync("EDControls.EDList", "TD.TemplatePicker");
        Assert.Null(result);
    }

    [Fact]
    public async Task Load_EmptyScope_Throws()
    {
        var (svc, _) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.LoadAsync("", "key"));
    }

    [Fact]
    public async Task Load_EmptyKey_Throws()
    {
        var (svc, _) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.LoadAsync("scope", ""));
    }

    [Fact]
    public async Task Save_EmptyScope_Throws()
    {
        var (svc, _) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.SaveAsync("", "key", JsonNode.Parse("{}")!));
    }

    [Fact]
    public async Task Save_WhitespaceKey_Throws()
    {
        var (svc, _) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.SaveAsync("scope", "   ", JsonNode.Parse("{}")!));
    }

    // Delete sdílí stejný ValidateArgs jako Load/Save - pin, ať refactor rozdělující
    // validaci na jednotlivé metody spadne.
    [Fact]
    public async Task Delete_EmptyScope_Throws()
    {
        var (svc, _) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.DeleteAsync("", "key"));
    }

    [Fact]
    public async Task Delete_WhitespaceKey_Throws()
    {
        var (svc, _) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.DeleteAsync("scope", "   "));
    }

    [Fact]
    public async Task Load_PassesScopeAndKeyToMLRest()
    {
        var (svc, ml) = Setup();
        var emptyResult = new CVar();
        ml.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>())
          .Returns(emptyResult);

        await svc.LoadAsync("EDControls.EDList", "TD.TemplatePicker");

        ml.Received(1).QueryOrThrow(
            "ALVA.EDControls.UserData",
            "Load",
            Arg.Is<CVar>(cv =>
                cv["scope"].Value!.ToString() == "EDControls.EDList" &&
                cv["key"].Value!.ToString()   == "TD.TemplatePicker"));
    }

    [Fact]
    public async Task Load_ParsesReturnedJson()
    {
        var (svc, ml) = Setup();
        var result = new CVar();
        result["data"] = new CVar("""{"widths":{"name":150,"date":80}}""");
        ml.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>())
          .Returns(result);

        var loaded = await svc.LoadAsync("scope", "key");

        Assert.NotNull(loaded);
        Assert.Equal(150, loaded!["widths"]!["name"]!.GetValue<int>());
        Assert.Equal(80,  loaded["widths"]!["date"]!.GetValue<int>());
    }

    [Fact]
    public async Task Load_InvalidJsonInData_ReturnsNull()
    {
        var (svc, ml) = Setup();
        var result = new CVar();
        result["data"] = new CVar("{ not valid json");
        ml.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>())
          .Returns(result);

        var loaded = await svc.LoadAsync("scope", "key");
        Assert.Null(loaded);  // JsonNode.Parse hodí → catch → null (odlišné od MLRest-throws)
    }

    [Fact]
    public async Task Load_EmptyData_ReturnsNull()
    {
        var (svc, ml) = Setup();
        var result = new CVar();
        result["data"] = new CVar("");
        ml.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>())
          .Returns(result);

        var loaded = await svc.LoadAsync("scope", "key");
        Assert.Null(loaded);  // IsNullOrEmpty větev (ne jen chybějící klíč)
    }

    [Fact]
    public async Task Load_MissingDataKey_ReturnsNull()
    {
        var (svc, ml) = Setup();
        var emptyResult = new CVar();  // bez "data" klíče
        ml.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>())
          .Returns(emptyResult);

        var loaded = await svc.LoadAsync("scope", "key");
        Assert.Null(loaded);
    }

    [Fact]
    public async Task Load_MLRestThrows_ReturnsNullNoException()
    {
        var (svc, ml) = Setup();
        ml.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>())
          .Throws(new InvalidOperationException("Caché unavailable"));

        var loaded = await svc.LoadAsync("scope", "key");
        Assert.Null(loaded);  // ne re-throw, ne padáme
    }

    [Fact]
    public async Task Save_PassesScopeKeyDataToMLRest()
    {
        var (svc, ml) = Setup();
        ml.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>())
          .Returns(new CVar());

        var data = JsonNode.Parse("""{"widths":{"a":100}}""")!;
        await svc.SaveAsync("EDControls.EDList", "TD.X", data);

        ml.Received(1).QueryOrThrow(
            "ALVA.EDControls.UserData",
            "Save",
            Arg.Is<CVar>(cv =>
                cv["scope"].Value!.ToString() == "EDControls.EDList" &&
                cv["key"].Value!.ToString()   == "TD.X" &&
                cv["data"].Value!.ToString()!.Contains("\"widths\"")));
    }

    [Fact]
    public async Task Save_MLRestThrows_NoExceptionPropagates()
    {
        var (svc, ml) = Setup();
        ml.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>())
          .Throws(new InvalidOperationException("Caché down"));

        // Nemělo by hodit - Save je fire-and-forget vůči errorům
        await svc.SaveAsync("scope", "key", JsonNode.Parse("{}")!);
    }

    [Fact]
    public async Task Delete_PassesScopeKeyToMLRest()
    {
        var (svc, ml) = Setup();
        ml.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>())
          .Returns(new CVar());

        await svc.DeleteAsync("scope", "key");

        ml.Received(1).QueryOrThrow(
            "ALVA.EDControls.UserData",
            "Delete",
            Arg.Is<CVar>(cv =>
                cv["scope"].Value!.ToString() == "scope" &&
                cv["key"].Value!.ToString()   == "key"));
    }

    [Fact]
    public async Task Delete_MLRestThrows_NoExceptionPropagates()
    {
        var (svc, ml) = Setup();
        ml.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>())
          .Throws(new InvalidOperationException("Caché down"));

        await svc.DeleteAsync("scope", "key");
    }

    [Fact]
    public async Task Save_ComplexNestedJson_SerializesCorrectly()
    {
        var (svc, ml) = Setup();
        ml.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>())
          .Returns(new CVar());

        var complex = JsonNode.Parse("""
            {
              "widths": { "name": 150, "date": 80, "active": 60 },
              "order": ["name", "active", "date"],
              "hidden": ["legacy_col"]
            }
            """)!;

        await svc.SaveAsync("scope", "key", complex);

        ml.Received(1).QueryOrThrow(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Is<CVar>(cv =>
                cv["data"].Value!.ToString()!.Contains("\"widths\"") &&
                cv["data"].Value!.ToString()!.Contains("\"order\"") &&
                cv["data"].Value!.ToString()!.Contains("\"hidden\"") &&
                cv["data"].Value!.ToString()!.Contains("legacy_col")));
    }
}
