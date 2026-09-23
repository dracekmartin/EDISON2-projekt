using System.Text.Json.Nodes;
using ALWA.SYS;
using EDControls;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISON2.IntegrationTests;

/// <summary>Uživatelská data (<c>ALVA.EDControls.UserData</c>) přes produkční služby, pod testovací firmou a uživatelem.</summary>
[Trait("Category", "Integration")]
public class UserPrefsTests
{
    private const string Scope = "EDISON2.IntegrationTests";

    // Save a Delete chyby polykají a hlásí; bez zachycení by neúspěšný zápis prošel a hlášení by šlo na podporu.
    private readonly List<Exception> _failures = [];

    private readonly IMLRest _rest = DevServer.Connect(DevServer.WriteIdfa, DevServer.WriteMlp);

    private CacheUserPrefsService CreatePrefs() => new(() => _rest, _failures.Add);

    [DevServerFact]
    public async Task SaveLoadDelete_RealServer_EmptyDiffersFromMissing()
    {
        var prefs = CreatePrefs();
        const string key = "roundtrip";
        var data = JsonNode.Parse("""{"a":1,"items":["x","y"]}""")!;
        try
        {
            await prefs.SaveAsync(Scope, key, data);
            Assert.True(JsonNode.DeepEquals(data, await prefs.LoadAsync(Scope, key)));

            await prefs.SaveAsync(Scope, key, new JsonArray());
            Assert.Empty(Assert.IsType<JsonArray>(await prefs.LoadAsync(Scope, key)));

            await prefs.DeleteAsync(Scope, key);
            Assert.Null(await prefs.LoadAsync(Scope, key));
        }
        finally
        {
            await prefs.DeleteAsync(Scope, key);
        }
        Assert.Empty(_failures);
    }

    // Plochu zapisuje i legacy EDISONMAIN2API (JSON skládá ručně), shell ji čte přes System.Text.Json.
    [DevServerFact]
    public async Task DesktopShortcutAdd_LegacySinkJson_ShellLoadsShortcut()
    {
        var prefs = CreatePrefs();
        // Tvar podle CacheShortcutSink.ToJson v repu EDISON (EDISONMAIN2API).
        const string item = """{"Kind":"PrintReport","Id":"1003","ElementId":"145008","Name":"Test","Exe":"EDISONMAIN2API.exe","Form":"ALVA.TEST.formTEST","Input":"","Report":{"Agenda":"A","Oblast":"O","TypSestavy":"T","Sestava":"S","Predvolba":"P","JeVerejny":true}}""";
        try
        {
            await prefs.DeleteAsync("EDISONMAIN2", "Shortcuts");
            var input = new CVar();
            input["item"] = new CVar(item);
            _rest.QueryOrThrow("ALVA.EDControls.DesktopShortcuts", "Add", input);

            var shortcuts = await new ModulePreferencesService(prefs).LoadShortcutsAsync();

            var shortcut = Assert.Single(Assert.IsType<List<DesktopShortcutDto>>(shortcuts));
            Assert.Equal(("PrintReport", "1003", "145008", "ALVA.TEST.formTEST"),
                         (shortcut.Kind, shortcut.Id, shortcut.ElementId, shortcut.Form));
            Assert.Equal(new ReportPayload("A", "O", "T", "S", "P", JeVerejny: true), shortcut.Report);
        }
        finally
        {
            await prefs.DeleteAsync("EDISONMAIN2", "Shortcuts");
        }
        Assert.Empty(_failures);
    }
}
