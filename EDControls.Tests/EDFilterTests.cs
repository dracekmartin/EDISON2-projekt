using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using EDControls.EDFilter;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Kontrakt filtru: co se posílá cíli (^||ML("IN","filter",Key) u Caché komponent)
/// a že obnovené hodnoty načtou data právě jednou.
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

        /// <summary>Změna hodnoty uživatelem.</summary>
        public void Change(string value)
        {
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

    /// <summary>Změna pole načítá hned; tlačítko slouží jen k vynucenému znovunačtení.</summary>
    [Fact]
    public void FieldChange_RefreshesTargetImmediately()
    {
        RunSta(() =>
        {
            var target = new FakeTarget();
            var field = new FakeFilterField { Key = "linka" };
            var filter = Arrange(new EDFilter.EDFilter { Target = target }, new FakePrefs());
            filter.Fields.Add(field);

            field.Change("7");

            Assert.Equal("7", Assert.Single(target.Calls)["linka"]);
        });
    }

    /// <summary>Target se v TD váže přes ElementName a nemusí být dosazený, když refresh přijde.</summary>
    [Fact]
    public void RefreshAsync_WithoutTarget_DoesNotThrow()
    {
        RunSta(() =>
        {
            var filter = new EDFilter.EDFilter();
            filter.Fields.Add(new FakeFilterField { Key = "linka", Value = "7" });

            filter.RefreshAsync().GetAwaiter().GetResult();
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

    /// <summary>Zavření okna, kde nikdo filtrem nehnul, nemá posílat request.</summary>
    [Fact]
    public void Unload_WithoutChange_SavesNothing()
    {
        RunSta(() =>
        {
            var prefs = new FakePrefs();
            var filter = Arrange(new EDFilter.EDFilter { HostId = "Test.Filter" }, prefs);
            filter.Fields.Add(new FakeFilterField { Key = "agendaCode" });

            filter.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.Equal(0, prefs.Saves);
        });
    }

    [Fact]
    public void Unload_WithPendingChange_FlushesSave()
    {
        RunSta(() =>
        {
            var prefs = new FakePrefs();
            var field = new FakeFilterField { Key = "agendaCode" };
            var filter = Arrange(new EDFilter.EDFilter { HostId = "Test.Filter" }, prefs);
            filter.Fields.Add(field);

            field.Change("MHD");   // rozjede debounce, který by sám doběhl až za 500 ms
            filter.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.Equal(1, prefs.Saves);
            Assert.Equal("MHD", prefs.Stored?["values"]?["agendaCode"]?.GetValue<string>());
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

    /// <summary>Odpojené pole se odvěsí z cizí kolekce, po návratu do stromu obnovu dokončí.</summary>
    [Fact]
    public void Combo_PendingRestore_SurvivesUnloadReload()
    {
        RunSta(() =>
        {
            var items = new ObservableCollection<Agenda>();
            var combo = new EDFilterCombo { SelectedValuePath = "Code", ItemsSource = items };

            combo.RestoreValue("MHD");
            combo.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            items.Add(new Agenda("MHD", "MHD"));
            Assert.Equal("", combo.GetValue());   // odpojené pole na kolekci neposlouchá

            combo.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

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
