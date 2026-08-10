using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using EDControls;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.ChangelogWindow;
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
    private readonly List<(string Title, ChangelogContent Content, Action<bool> OnClosed)> _shown = [];

    private ChangelogPromptService Create()
    {
        var service = new ChangelogPromptService(_source, new ModulePreferencesService(_prefs),
            (title, content, onClosed) => _shown.Add((title, content, onClosed)));
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
public class ChangelogViewModelTests
{
    private static ChangelogViewModel Create(ChangelogContent content)
    {
        EDServices.Initialize(new EDServices(MLRestMocks.Create("CB_DP"),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));
        try
        {
            return new ChangelogViewModel("EDISON", content);
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
    public void WithoutChanges_ShowsFullHistory()
    {
        var vm = Create(new ChangelogContent("AD", null, "historie", ""));

        Assert.False(vm.HasChanges);
        Assert.False(vm.ShowChangesOnly);
        Assert.Equal("historie", vm.DisplayText);
    }
}
