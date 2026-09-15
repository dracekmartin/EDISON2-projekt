using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EDControls.EDFilter;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Kontrakt filtru: co se posílá cíli (^||ML("IN","filter",Key) u Caché komponent),
/// že cíl může být komponenta i příkaz VM, a že obnovené hodnoty načtou data právě jednou.
/// </summary>
public class EDFilterTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    private sealed class FakeFilterField : EDFilterFieldBase
    {
        public string Value { get; set; } = "";
        public List<string> Restored { get; } = [];

        public override string GetValue() => Value;

        public override void RestoreValue(string value)
        {
            Restored.Add(value);
            Value = value;
            RaiseValueChanged();
        }
    }

    private sealed class FakeTarget : IEDFilterTarget
    {
        public List<IReadOnlyDictionary<string, string>> Calls { get; } = [];

        public Task ApplyFilterAsync(IReadOnlyDictionary<string, string> filter)
        {
            Calls.Add(new Dictionary<string, string>(filter));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCommand : ICommand
    {
        public List<object?> Executed { get; } = [];
        public bool CanRun { get; set; } = true;

        public bool CanExecute(object? parameter) => CanRun;
        public void Execute(object? parameter) => Executed.Add(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    /// <summary>Prefs v paměti. <c>Stored</c> = co filtr uložil, <c>Seed</c> = co najde při startu.</summary>
    private sealed class FakePrefs : IUserPrefsService
    {
        public JsonNode? Seed { get; set; }
        public JsonNode? Stored { get; private set; }
        public int Saves { get; private set; }

        public Task<JsonNode?> LoadAsync(string scope, string key) => Task.FromResult(Seed);

        public Task SaveAsync(string scope, string key, JsonNode data)
        {
            Stored = data;
            Saves++;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string scope, string key) => Task.CompletedTask;
    }

    // ── Sběr hodnot ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildFilterDict_SkipsEmptyKeysAndEmptyValues()
    {
        RunSta(() =>
        {
            var filter = new EDFilter.EDFilter();
            filter.Fields.Add(new FakeFilterField { Key = "agendaCode", Value = "MHD" });
            filter.Fields.Add(new FakeFilterField { Key = "", Value = "bez klíče" });
            filter.Fields.Add(new FakeFilterField { Key = "obdobi", Value = "" });

            var dict = filter.BuildFilterDict();

            Assert.Equal(new Dictionary<string, string> { ["agendaCode"] = "MHD" }, dict);
        });
    }

    [Fact]
    public void BuildFilterDict_AllEmpty_ReturnsEmptyDict()
    {
        RunSta(() =>
        {
            var filter = new EDFilter.EDFilter();
            filter.Fields.Add(new FakeFilterField { Key = "agendaCode", Value = "" });

            Assert.Empty(filter.BuildFilterDict());
        });
    }

    // ── Cíl filtru ────────────────────────────────────────────────────────────

    [Fact]
    public void RefreshAsync_PassesFilterToTarget()
    {
        RunSta(() =>
        {
            var target = new FakeTarget();
            var filter = new EDFilter.EDFilter { Target = target };
            filter.Fields.Add(new FakeFilterField { Key = "agendaCode", Value = "MHD" });

            filter.RefreshAsync().GetAwaiter().GetResult();

            Assert.Equal(new Dictionary<string, string> { ["agendaCode"] = "MHD" },
                         Assert.Single(target.Calls));
        });
    }

    /// <summary>Cíl řízený ViewModelem (mapa) se napojuje příkazem, ne komponentou.</summary>
    [Fact]
    public void RefreshAsync_InvokesFilterCommandWithDict()
    {
        RunSta(() =>
        {
            var command = new FakeCommand();
            var filter = new EDFilter.EDFilter { FilterCommand = command };
            filter.Fields.Add(new FakeFilterField { Key = "linka", Value = "7" });

            filter.RefreshAsync().GetAwaiter().GetResult();

            var param = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(Assert.Single(command.Executed));
            Assert.Equal("7", param["linka"]);
        });
    }

    [Fact]
    public void RefreshAsync_TargetAndCommandTogether_BothGetCalled()
    {
        RunSta(() =>
        {
            var target = new FakeTarget();
            var command = new FakeCommand();
            var filter = new EDFilter.EDFilter { Target = target, FilterCommand = command };

            filter.RefreshAsync().GetAwaiter().GetResult();

            Assert.Single(target.Calls);
            Assert.Single(command.Executed);
        });
    }

    [Fact]
    public void RefreshAsync_CommandCannotExecute_IsSkipped()
    {
        RunSta(() =>
        {
            var command = new FakeCommand { CanRun = false };
            var filter = new EDFilter.EDFilter { FilterCommand = command };

            filter.RefreshAsync().GetAwaiter().GetResult();

            Assert.Empty(command.Executed);
        });
    }

    [Fact]
    public void RefreshAsync_WithoutTargetOrCommand_NoOpWithoutFilterChanged()
    {
        // Bez cíle se nesmí ani střílet FilterChanged - host by reagoval na refresh,
        // který neproběhne.
        RunSta(() =>
        {
            var filter = new EDFilter.EDFilter();
            bool raised = false;
            filter.FilterChanged += (_, _) => raised = true;

            filter.RefreshAsync().GetAwaiter().GetResult();

            Assert.False(raised);
        });
    }

    [Fact]
    public void EDList_IsFilterTarget()
    {
        RunSta(() => Assert.IsAssignableFrom<IEDFilterTarget>(new EDList.EDList()));
    }

    // ── Pamatování hodnot ─────────────────────────────────────────────────────

    /// <summary>Obnova musí proběhnout před prvním načtením, jinak cíl načítá dvakrát.</summary>
    [Fact]
    public void InitialLoad_RestoresValuesAndLoadsOnce()
    {
        RunSta(() =>
        {
            var prefs = new FakePrefs { Seed = JsonNode.Parse("""{"version":1,"values":{"agendaCode":"MHD"}}""") };
            var target = new FakeTarget();
            var field = new FakeFilterField { Key = "agendaCode" };
            var filter = Arrange(new EDFilter.EDFilter { Target = target, HostId = "Test.Filter" }, prefs);
            filter.Fields.Add(field);

            filter.InitialLoadAsync().GetAwaiter().GetResult();

            Assert.Equal(["MHD"], field.Restored);
            Assert.Equal("MHD", Assert.Single(target.Calls)["agendaCode"]);
        });
    }

    [Fact]
    public void InitialLoad_WithoutHostId_IgnoresStoredValues()
    {
        RunSta(() =>
        {
            var prefs = new FakePrefs { Seed = JsonNode.Parse("""{"values":{"agendaCode":"MHD"}}""") };
            var target = new FakeTarget();
            var field = new FakeFilterField { Key = "agendaCode" };
            var filter = Arrange(new EDFilter.EDFilter { Target = target }, prefs);
            filter.Fields.Add(field);

            filter.InitialLoadAsync().GetAwaiter().GetResult();

            Assert.Empty(field.Restored);
            Assert.Empty(Assert.Single(target.Calls));
        });
    }

    [Fact]
    public void InitialLoad_MalformedPrefs_LoadsWithoutFilter()
    {
        RunSta(() =>
        {
            var prefs = new FakePrefs { Seed = JsonNode.Parse("""{"values":"tohle je string, ne objekt"}""") };
            var target = new FakeTarget();
            var filter = Arrange(new EDFilter.EDFilter { Target = target, HostId = "Test.Filter" }, prefs);
            filter.Fields.Add(new FakeFilterField { Key = "agendaCode" });

            filter.InitialLoadAsync().GetAwaiter().GetResult();

            Assert.Empty(Assert.Single(target.Calls));
        });
    }

    /// <summary>Obnovené hodnoty se nesmí hned uložit zpátky ani spustit druhý refresh.</summary>
    [Fact]
    public void InitialLoad_RestoreDoesNotSaveOrRefreshAgain()
    {
        RunSta(() =>
        {
            var prefs = new FakePrefs { Seed = JsonNode.Parse("""{"values":{"agendaCode":"MHD"}}""") };
            var target = new FakeTarget();
            var filter = Arrange(new EDFilter.EDFilter { Target = target, HostId = "Test.Filter" }, prefs);
            filter.Fields.Add(new FakeFilterField { Key = "agendaCode" });

            filter.InitialLoadAsync().GetAwaiter().GetResult();

            Assert.Single(target.Calls);
            Assert.Equal(0, prefs.Saves);
        });
    }

    // ── EDFilterCombo ─────────────────────────────────────────────────────────

    private sealed record Agenda(string Code, string Label);

    [Fact]
    public void Combo_RestoreValue_SelectsMatchingItem()
    {
        RunSta(() =>
        {
            var combo = new EDFilterCombo
            {
                SelectedValuePath = "Code",
                ItemsSource = new List<Agenda> { new("MHD", "MHD"), new("FAKTURY", "Faktury") },
            };

            combo.RestoreValue("FAKTURY");

            Assert.Equal("FAKTURY", combo.GetValue());
        });
    }

    /// <summary>VM plní seznam až po Loaded - obnova musí počkat, jinak hodnota zmizí.</summary>
    [Fact]
    public void Combo_RestoreValue_AppliesAfterItemsArriveLater()
    {
        RunSta(() =>
        {
            var items = new ObservableCollection<Agenda>();
            var combo = new EDFilterCombo { SelectedValuePath = "Code", ItemsSource = items };

            combo.RestoreValue("MHD");
            Assert.Equal("", combo.GetValue());

            items.Add(new Agenda("MHD", "MHD"));

            Assert.Equal("MHD", combo.GetValue());
        });
    }

    [Fact]
    public void Combo_RestoreValue_UnknownCode_LeavesFieldEmpty()
    {
        RunSta(() =>
        {
            var combo = new EDFilterCombo
            {
                SelectedValuePath = "Code",
                ItemsSource = new List<Agenda> { new("MHD", "MHD") },
            };

            combo.RestoreValue("ZRUSENA");

            Assert.Equal("", combo.GetValue());
        });
    }

    // ── Infrastruktura ────────────────────────────────────────────────────────

    private static EDFilter.EDFilter Arrange(EDFilter.EDFilter filter, IUserPrefsService prefs)
    {
        filter.ServicesOverride = new EDServices(
            mlRest: null,
            windowNavigator: Substitute.For<IWindowNavigator>(),
            popupDisplayer: Substitute.For<IPopupDisplayer>())
        { UserPrefs = prefs };
        return filter;
    }

    private static void EnsureApplication()
    {
        if (Application.Current is not null) return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
    }

    private static void RunSta(Action action) =>
        EDTesting.Sta.RunOnAppThread(() => { EnsureApplication(); action(); });
}
