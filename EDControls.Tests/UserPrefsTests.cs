using System.Text.Json.Nodes;
using ALWA.SYS;
using EDControls.UserPrefs;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EDControls.Tests;

/// <summary>
/// Testy <see cref="CacheUserPrefsService"/> + <see cref="NullUserPrefsService"/>.
/// MLRest mockujeme přes NSubstitute - testuje se kontrakt volání ne sítová komunikace.
/// Služba volá vždy klon (<c>With()</c>), takže očekávání sedí na klonu, ne na sdílené instanci.
/// </summary>
public class UserPrefsTests
{
    private static (CacheUserPrefsService Service, IMLRest MockMl) Setup()
    {
        var (svc, _, clone, _) = SetupFull();
        return (svc, clone);
    }

    private static (CacheUserPrefsService Service, IMLRest Shared, IMLRest Clone, List<Exception> Reported) SetupFull()
    {
        var shared = Substitute.For<IMLRest>();
        var clone = Substitute.For<IMLRest>();
        shared.With().Returns(clone);
        var reported = new List<Exception>();
        return (new CacheUserPrefsService(() => shared, reported.Add), shared, clone, reported);
    }

    [Fact]
    public async Task Load_RunsOnClone_NotOnSharedInstance()
    {
        // RestStatus je per-instance: preference ukládá debouncer/zavření okna souběžně s UI threadem,
        // takže request na sdílené instanci by si prohodil stav s cizím voláním.
        var (svc, shared, clone, _) = SetupFull();
        clone.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>()).Returns(new CVar());

        await svc.LoadAsync("scope", "key");
        await svc.SaveAsync("scope", "key", JsonNode.Parse("{}")!);
        await svc.DeleteAsync("scope", "key");

        shared.DidNotReceiveWithAnyArgs().QueryOrThrow(default!, default!, default(CVar)!);
        clone.Received(3).QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>());
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
    public async Task Load_InvalidJsonInData_ReturnsNullAndReports()
    {
        // Poškozený blob je k ničemu → null ho nechá příštím Save přepsat; ale nesmí projít tiše.
        var (svc, _, ml, reported) = SetupFull();
        var result = new CVar();
        result["data"] = new CVar("{ not valid json");
        ml.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>())
          .Returns(result);

        var loaded = await svc.LoadAsync("scope", "key");

        Assert.Null(loaded);
        Assert.Single(reported);
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
    public async Task Load_MLRestThrows_PropagatesAndReports()
    {
        // Null je vyhrazený pro „klíč neexistuje". Výpadek maskovaný jako prázdno by volajícího
        // svedl k přepsání uživatelových dat prázdnem (plocha shellu, layout sloupců v TD).
        var (svc, _, ml, reported) = SetupFull();
        var failure = new InvalidOperationException("Caché unavailable");
        ml.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>()).Throws(failure);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.LoadAsync("scope", "key"));

        Assert.Same(failure, ex);
        Assert.Same(failure, Assert.Single(reported));
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
    public async Task Save_MLRestThrows_NoExceptionPropagates_ButReports()
    {
        var (svc, _, ml, reported) = SetupFull();
        var failure = new InvalidOperationException("Caché down");
        ml.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>()).Throws(failure);

        // Nemělo by hodit - Save je fire-and-forget vůči errorům; ztráta uložení ale jde do telemetrie.
        await svc.SaveAsync("scope", "key", JsonNode.Parse("{}")!);

        Assert.Same(failure, Assert.Single(reported));
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
