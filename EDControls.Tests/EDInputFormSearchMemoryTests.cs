using System.Windows;
using ALWA.SYS;
using EDControls.EDInputForm;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;
using Form = EDControls.EDInputForm.EDInputForm;

namespace EDControls.Tests;

/// <summary>
/// Zapamatování hledaného výrazu mezi otevřeními formuláře (SearchMemoryKey + ISessionState).
/// Scénář: uživatel ladí fonty, dokola otvírá vlastnosti prvku a filtr „font" má zůstat.
/// </summary>
public class EDInputFormSearchMemoryTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void WithoutKey_NothingIsRemembered()
    {
        RunSta(() =>
        {
            var services = NewServices();

            var first = NewForm(services);
            first.Init();
            first.Toolbar.SearchText = "font";

            var second = NewForm(services);
            second.Init();

            Assert.Equal("", second.Toolbar.SearchText);
            Assert.False(FieldOf(second, "velikost").IsFilteredBySearch);
        });
    }

    [Fact]
    public void WithKey_NextInstanceOpensFiltered()
    {
        RunSta(() =>
        {
            var services = NewServices();

            var first = NewForm(services, "TD.ElementEdit");
            first.Init();
            first.Toolbar.SearchText = "font";

            var second = NewForm(services, "TD.ElementEdit");
            second.Init();

            Assert.Equal("font", second.Toolbar.SearchText);
            Assert.False(FieldOf(second, "font").IsFilteredBySearch);
            Assert.True(FieldOf(second, "velikost").IsFilteredBySearch);
        });
    }

    /// <summary>Zrušený filtr se nesmí vrátit - uživatel ho právě smazal.</summary>
    [Fact]
    public void ClearedSearch_IsForgotten()
    {
        RunSta(() =>
        {
            var services = NewServices();

            var first = NewForm(services, "TD.ElementEdit");
            first.Init();
            first.Toolbar.SearchText = "font";
            first.Toolbar.SearchText = "";

            var second = NewForm(services, "TD.ElementEdit");
            second.Init();

            Assert.Equal("", second.Toolbar.SearchText);
            Assert.False(FieldOf(second, "velikost").IsFilteredBySearch);
        });
    }

    [Fact]
    public void DifferentKeys_DoNotShareTheFilter()
    {
        RunSta(() =>
        {
            var services = NewServices();

            var first = NewForm(services, "TD.ElementEdit");
            first.Init();
            first.Toolbar.SearchText = "font";

            var second = NewForm(services, "TD.PrintParams");
            second.Init();

            Assert.Equal("", second.Toolbar.SearchText);
        });
    }

    private static Field FieldOf(Form form, string key) =>
        form.Items!.OfType<Field>().Single(f => f.Key == key);

    private static Form NewForm(EDServices services, string searchMemoryKey = "") =>
        new() { CacheClass = "ALVA.A", ServicesOverride = services, SearchMemoryKey = searchMemoryKey };

    /// <summary>Jedna instance služeb = jeden běh aplikace; formuláře tak sdílí paměť procesu.</summary>
    private static EDServices NewServices()
    {
        var mock = MLRestMocks.Create();
        mock.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>()).Returns(_ => Schema());
        return new EDServices(mock, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());
    }

    private static CVar Schema()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "font", "Font", "TEXT", "Arial", "0", "0");
        data["2"] = new CList("FIELD", "2", "velikost", "Velikost", "NUMBER", "9", "0", "0");
        return data;
    }

    // EDInputForm.xaml čte StaticResource EDIcons/EDStrings → Application se slovníky appky.
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
