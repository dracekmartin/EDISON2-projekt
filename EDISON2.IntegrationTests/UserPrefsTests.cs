using System.Text.Json.Nodes;
using ALWA.SYS;
using EDControls;
using EDControls.UserPrefs;
using EDISONMAIN2.Desktop;
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

    // Agenda zapíše sestavu na plochu, zatímco shell drží svůj snímek z načtení. ★ v shellu ji nesmí přepsat.
    [DevServerFact]
    public async Task DesktopStar_AfterAgendaAdd_BlobKeepsBoth()
    {
        var prefs = CreatePrefs();
        var modulePrefs = CreateModulePrefs(prefs);
        var module = MenuModule("145009");
        try
        {
            await prefs.DeleteAsync("EDISONMAIN2", "Shortcuts");
            var shell = new ModuleShortcutsService();
            shell.Seed([module], await modulePrefs.LoadShortcutsAsync());
            AgendaAdd(SinkReportItem);

            shell.Add(module);
            await modulePrefs.AddShortcutAsync(shell.Items.Single().ToDto());

            var stored = Assert.IsType<List<DesktopShortcutDto>>(await modulePrefs.LoadShortcutsAsync());
            Assert.Equal([("PrintReport", "1003"), ("MenuModule", "145009")], stored.Select(s => (s.Kind, s.Id)));
        }
        finally
        {
            await prefs.DeleteAsync("EDISONMAIN2", "Shortcuts");
        }
        Assert.Empty(_failures);
    }

    // Drop na obsazenou buňku pošle Add nové dlaždice a Place té vytlačené: různé klíče, na pořadí nesmí záležet.
    [DevServerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DesktopDropOnOccupiedCell_TilesLoadOnTheirCells_InEitherOrder(bool placeFirst)
    {
        var prefs = CreatePrefs();
        var modulePrefs = CreateModulePrefs(prefs);
        var a = MenuModule("145009");
        var b = MenuModule("145010");
        try
        {
            await prefs.DeleteAsync("EDISONMAIN2", "Shortcuts");
            var shell = new ModuleShortcutsService();
            shell.Seed([a, b], []);
            shell.Add(a);
            await modulePrefs.AddShortcutAsync(Tile(shell, a).ToDto());

            shell.Add(b, (0, 0));
            if (placeFirst) await modulePrefs.PlaceShortcutsAsync([Tile(shell, a).ToDto()]);
            await modulePrefs.AddShortcutAsync(Tile(shell, b).ToDto());
            if (!placeFirst) await modulePrefs.PlaceShortcutsAsync([Tile(shell, a).ToDto()]);

            var restarted = new ModuleShortcutsService();
            restarted.Seed([a, b], await modulePrefs.LoadShortcutsAsync());
            Assert.Equal((0, 0), (Tile(restarted, b).Col, Tile(restarted, b).Row));
            Assert.Equal((1, 0), (Tile(restarted, a).Col, Tile(restarted, a).Row));
        }
        finally
        {
            await prefs.DeleteAsync("EDISONMAIN2", "Shortcuts");
        }
        Assert.Empty(_failures);
    }

    private ModulePreferencesService CreateModulePrefs(CacheUserPrefsService prefs) =>
        new(prefs, new RestCallRunner(() => _rest), _failures.Add);

    private static ExecutableModule MenuModule(string elementId) =>
        new() { Name = "Test " + elementId, ElementId = elementId, ExecutablePath = "TEST.exe", FormName = "ALVA.TEST.formTEST" };

    private static DesktopShortcut Tile(ModuleShortcutsService shell, ExecutableModule module) =>
        shell.Items.Single(i => i.Module == module);

    private const string SinkReportItem = """{"Kind":"PrintReport","Id":"1003","ElementId":"145008","Name":"Test","Exe":"EDISONMAIN2API.exe","Form":"ALVA.TEST.formTEST","Input":""}""";

    private void AgendaAdd(string item)
    {
        var input = new CVar();
        input["item"] = new CVar(item);
        _rest.QueryOrThrow("ALVA.EDControls.DesktopShortcuts", "Add", input);
    }

    // Varianty modulu menu (Převody AD/MHD) mají shodné ElementId i Id: server je nesmí slít a shell každou
    // dlaždici přiřadí její variantě (klíč DesktopShortcuts.KeyOf = DesktopShortcut.Key).
    [DevServerFact]
    public async Task DesktopShortcutAdd_MenuModuleVariants_ServerKeepsBoth_ShellResolvesEach()
    {
        var prefs = CreatePrefs();
        static string Item(string input) =>
            $$"""{"Kind":"MenuModule","Id":"146016","ElementId":"146016","Name":"Převody {{input}}","Exe":"MZDY2.exe","Form":"ALVA.MZDY2.formMZDY2Prevod","Input":"{{input}}"}""";
        try
        {
            await prefs.DeleteAsync("EDISONMAIN2", "Shortcuts");
            foreach (var input in new[] { "AD", "MHD", "AD" })
            {
                var cvar = new CVar();
                cvar["item"] = new CVar(Item(input));
                _rest.QueryOrThrow("ALVA.EDControls.DesktopShortcuts", "Add", cvar);
            }

            var stored = await new ModulePreferencesService(prefs).LoadShortcutsAsync();
            ExecutableModule Prevod(string input) => new()
                { Name = "Převody " + input, ElementId = "146016", ExecutablePath = "MZDY2.exe", FormName = "ALVA.MZDY2.formMZDY2Prevod", InputParam = input };
            var ad = Prevod("AD"); var mhd = Prevod("MHD");
            var svc = new ModuleShortcutsService();
            svc.Seed([ad, mhd], stored);

            Assert.Equal([ad, mhd], svc.Items.Select(i => i.Module));
        }
        finally
        {
            await prefs.DeleteAsync("EDISONMAIN2", "Shortcuts");
        }
        Assert.Empty(_failures);
    }
}
