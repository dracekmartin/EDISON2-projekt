using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using EDControls;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.ChangelogWindow;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Rozhodovací logika auto-popupu: existence změn × snooze hash × jednou za session.
/// Zdroj je mock, presenter je záznamová lambda - žádné WPF okno.
/// </summary>
public class ChangelogPromptServiceTests
{
    private readonly IChangelogSource _source = Substitute.For<IChangelogSource>();
    private readonly IUserPrefsService _prefs = Substitute.For<IUserPrefsService>();
    private readonly List<(string Title, ChangelogContent Content, bool Snoozed, Action<bool> OnClosed)> _shown = [];

    private ChangelogPromptService Create()
    {
        var service = new ChangelogPromptService(_source, new ModulePreferencesService(_prefs),
            (title, content, snoozed, onClosed) => _shown.Add((title, content, snoozed, onClosed)));
        return service;
    }

    private static ChangelogContent Content(string key = "AD", string? changes = "novinka") =>
        new(key, changes, "historie",
            changes is null ? "" : InfChangelogSource.ComputeChangeSetId(changes));

    private void SourceReturns(string moduleKey, ChangelogContent? content) =>
        _source.LoadAsync(moduleKey).Returns(Task.FromResult(content));

    [Fact]
    public async Task NoChangelog_NothingShown()
    {
        SourceReturns("X.exe", null);

        await Create().ShowIfNewAsync("X.exe", "Modul X");

        Assert.Empty(_shown);
    }

    [Fact]
    public async Task NoNewChanges_NothingShown()
    {
        SourceReturns("X.exe", Content(key: "X", changes: null));

        await Create().ShowIfNewAsync("X.exe", "Modul X");

        Assert.Empty(_shown);
    }

    [Fact]
    public async Task NewChanges_ShownWithContent()
    {
        var content = Content();
        SourceReturns("ADSETTINGS.exe", content);

        await Create().ShowIfNewAsync("ADSETTINGS.exe", "Nastavení AD");

        var shown = Assert.Single(_shown);
        Assert.Equal("Nastavení AD", shown.Title);
        Assert.Same(content, shown.Content);
    }

    [Fact]
    public async Task SnoozedSameChangeSet_NothingShown()
    {
        var content = Content();
        SourceReturns("ADSETTINGS.exe", content);
        var service = Create();
        service.SeedSnooze(new() { ["AD"] = content.ChangeSetId });

        await service.ShowIfNewAsync("ADSETTINGS.exe", "Nastavení AD");

        Assert.Empty(_shown);
    }

    [Fact]
    public async Task SnoozedOlderChangeSet_NewChangesShown()
    {
        SourceReturns("ADSETTINGS.exe", Content(changes: "čerstvé změny"));
        var service = Create();
        service.SeedSnooze(new() { ["AD"] = InfChangelogSource.ComputeChangeSetId("staré změny") });

        await service.ShowIfNewAsync("ADSETTINGS.exe", "Nastavení AD");

        Assert.Single(_shown);
    }

    [Fact]
    public async Task SameChangeSetKey_ShownOncePerSession()
    {
        // Dvě AD agendy + opětovné otevření první (re-focus dedup v ShowLegacyAsync) = 1 popup.
        SourceReturns("ADSETTINGS.exe", Content());
        SourceReturns("ADZPRACOVANI.exe", Content());
        var service = Create();

        await service.ShowIfNewAsync("ADSETTINGS.exe", "Nastavení AD");
        await service.ShowIfNewAsync("ADZPRACOVANI.exe", "Zpracování AD");
        await service.ShowIfNewAsync("ADSETTINGS.exe", "Nastavení AD");

        Assert.Single(_shown);
    }

    [Fact]
    public async Task ShowAlways_SnoozedAndAlreadyShown_StillShows()
    {
        var content = Content();
        SourceReturns("ADSETTINGS.exe", content);
        var service = Create();
        service.SeedSnooze(new() { ["AD"] = content.ChangeSetId });

        await service.ShowIfNewAsync("ADSETTINGS.exe", "Nastavení AD");
        Assert.Empty(_shown);

        Assert.True(await service.ShowAlwaysAsync("ADSETTINGS.exe", "Nastavení AD"));

        var shown = Assert.Single(_shown);
        Assert.Same(content, shown.Content);
        // Checkbox startuje s uloženým stavem, aby šlo odložení zrušit.
        Assert.True(shown.Snoozed);
    }

    [Fact]
    public async Task ShowAlways_WithoutDiff_ShowsFullHistory()
    {
        SourceReturns("X.exe", Content(key: "X", changes: null));

        Assert.True(await Create().ShowAlwaysAsync("X.exe", "Modul X"));

        var shown = Assert.Single(_shown);
        Assert.Equal("historie", shown.Content.FullText);
        Assert.False(shown.Snoozed);
    }

    [Fact]
    public async Task ShowAlways_NoInf_ReturnsFalseNothingShown()
    {
        SourceReturns("X.exe", null);

        Assert.False(await Create().ShowAlwaysAsync("X.exe", "Modul X"));

        Assert.Empty(_shown);
    }

    [Fact]
    public async Task ShowAlways_DoesNotConsumeSessionDedup()
    {
        SourceReturns("ADSETTINGS.exe", Content());
        var service = Create();

        await service.ShowAlwaysAsync("ADSETTINGS.exe", "Nastavení AD");
        await service.ShowIfNewAsync("ADSETTINGS.exe", "Nastavení AD");

        Assert.Equal(2, _shown.Count);
    }

    [Fact]
    public async Task ShowAlways_UncheckedSnooze_DropsEntryAndPersists()
    {
        JsonNode? saved = null;
        _prefs.SaveAsync("EDISONMAIN2", "ChangelogSnooze", Arg.Do<JsonNode>(n => saved = n))
            .Returns(Task.CompletedTask);
        var content = Content();
        SourceReturns("ADSETTINGS.exe", content);
        var service = Create();
        service.SeedSnooze(new() { ["AD"] = content.ChangeSetId, ["MHD"] = "JINY" });

        await service.ShowAlwaysAsync("ADSETTINGS.exe", "Nastavení AD");
        _shown.Single().OnClosed(false);

        Assert.NotNull(saved);
        var snooze = saved.Deserialize<Dictionary<string, string>>();
        Assert.False(snooze!.ContainsKey("AD"));
        Assert.Equal("JINY", snooze["MHD"]);
    }

    [Fact]
    public async Task ShowAlways_WithoutDiff_NeverTouchesSnooze()
    {
        // Prázdný ChangeSetId nemá co klíčovat - okno má checkbox zakázaný a nesmí nic uložit.
        SourceReturns("X.exe", Content(key: "X", changes: null));
        var service = Create();
        service.SeedSnooze(new() { ["X"] = "STARY" });

        await service.ShowAlwaysAsync("X.exe", "Modul X");
        _shown.Single().OnClosed(false);

        await _prefs.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<JsonNode>());
    }

    [Fact]
    public async Task SnoozeConfirmed_PersistsHashUnderInfKey()
    {
        JsonNode? saved = null;
        _prefs.SaveAsync("EDISONMAIN2", "ChangelogSnooze", Arg.Do<JsonNode>(n => saved = n))
            .Returns(Task.CompletedTask);
        var content = Content();
        SourceReturns("ADSETTINGS.exe", content);

        await Create().ShowIfNewAsync("ADSETTINGS.exe", "Nastavení AD");
        _shown.Single().OnClosed(true);

        Assert.NotNull(saved);
        var snooze = saved.Deserialize<Dictionary<string, string>>();
        Assert.Equal(content.ChangeSetId, snooze!["AD"]);
    }

    [Fact]
    public async Task ClosedWithoutSnooze_NothingPersisted()
    {
        SourceReturns("ADSETTINGS.exe", Content());

        await Create().ShowIfNewAsync("ADSETTINGS.exe", "Nastavení AD");
        _shown.Single().OnClosed(false);

        await _prefs.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<JsonNode>());
    }

    [Fact]
    public async Task SourceThrows_SwallowedNothingShown()
    {
        _source.LoadAsync("X.exe").Returns(Task.FromException<ChangelogContent?>(new IOException("disk")));

        await Create().ShowIfNewAsync("X.exe", "Modul X");

        Assert.Empty(_shown);
    }
}

/// <summary>Přepínání změny/historie ve ViewModelu okna (EDServices nutné pro EDViewModel base).</summary>
[Collection(SharedStaticStateCollection.Name)]
public class ChangelogViewModelTests
{
    private static ChangelogViewModel Create(ChangelogContent content, bool snoozed = false)
    {
        EDServices.Initialize(new EDServices(MLRestMocks.Create("CB_DP"),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));
        try
        {
            return new ChangelogViewModel("EDISON", content, snoozed);
        }
        finally
        {
            EDServices.Reset();
        }
    }

    [Fact]
    public void WithChanges_DefaultsToChangesOnly_TogglesToFullHistory()
    {
        var vm = Create(new ChangelogContent("AD", "změny", "historie", "HASH"));

        Assert.True(vm.HasChanges);
        Assert.True(vm.ShowChangesOnly);
        Assert.Equal("změny", vm.DisplayText);

        vm.ShowFullHistory = true;

        Assert.False(vm.ShowChangesOnly);
        Assert.Equal("historie", vm.DisplayText);
    }

    [Fact]
    public void ForcedOpenWhileSnoozed_CheckboxStartsChecked()
    {
        var vm = Create(new ChangelogContent("AD", "změny", "historie", "HASH"), snoozed: true);

        Assert.True(vm.SnoozeChecked);
    }

    [Fact]
    public void WithoutChanges_ShowsFullHistory()
    {
        var vm = Create(new ChangelogContent("AD", null, "historie", ""));

        Assert.False(vm.HasChanges);
        Assert.False(vm.ShowChangesOnly);
        Assert.Equal("historie", vm.DisplayText);
    }
}

/// <summary>
/// Vstupní body vynucených novinek v shellu (menu Nápověda, kontextové menu stromu, dlaždice
/// plochy). Testuje se jen cesta bez INF - zobrazení okna vyžaduje STA vlákno.
/// </summary>
[Collection(SharedStaticStateCollection.Name)]
public class ChangelogEntryPointTests
{
    private readonly IChangelogSource _source = Substitute.For<IChangelogSource>();

    private EdisonMainViewModel CreateVM(IPopupDisplayer popup, ModuleShortcutsService? shortcuts = null)
    {
        _source.LoadAsync(Arg.Any<string>()).Returns(Task.FromResult<ChangelogContent?>(null));
        return EdisonMainViewModelHarness.CreateVM(popup: popup, changelogSource: _source, shortcuts: shortcuts);
    }

    [Fact]
    public async Task ShellChangelog_WithoutInf_TellsUserInsteadOfDoingNothing()
    {
        var popup = Substitute.For<IPopupDisplayer>();
        var vm = CreateVM(popup);

        vm.ShowShellChangelogCommand.Execute(null);

        await _source.Received().LoadAsync("EDISONMAIN");
        popup.Received().Info(Arg.Is<string>(m => m.Contains("informace o změnách")));
    }

    [Fact]
    public async Task ModuleChangelog_UsesExecutablePath()
    {
        var popup = Substitute.For<IPopupDisplayer>();
        var vm = CreateVM(popup);

        vm.ShowModuleChangelogCommand.Execute(
            new ExecutableModule { DisplayedName = "Turnusy", ExecutablePath = "t.exe", FormName = "" });

        await _source.Received().LoadAsync("t.exe");
        popup.Received().Info(Arg.Is<string>(m => m.Contains("Turnusy")));
    }

    [Fact]
    public void ModuleChangelog_Family_DoesNothing()
    {
        // Rodina modulů nemá exe, takže se nemá co dohledávat ani na co stěžovat.
        var popup = Substitute.For<IPopupDisplayer>();
        var vm = CreateVM(popup);

        vm.ShowModuleChangelogCommand.Execute(new ModuleFamily { DisplayedName = "MHD" });

        _source.DidNotReceive().LoadAsync(Arg.Any<string>());
        popup.DidNotReceive().Info(Arg.Any<string>());
    }

    [Fact]
    public async Task ShortcutChangelogRequest_ReachesShell()
    {
        var popup = Substitute.For<IPopupDisplayer>();
        var shortcuts = new ModuleShortcutsService();
        CreateVM(popup, shortcuts);

        shortcuts.RequestChangelog(new DesktopShortcut { ExecutablePath = "t.exe", DisplayedName = "Turnusy" });

        await _source.Received().LoadAsync("t.exe");
    }
}
