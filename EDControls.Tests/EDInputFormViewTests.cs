using System.Windows;
using ALWA.SYS;
using EDControls.EDInputForm;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;
using Form = EDControls.EDInputForm.EDInputForm;

namespace EDControls.Tests;

/// <summary>
/// Code-behind EDInputFormu, který nejde pokrýt bez controlu: hledání (filtrování položek
/// a obnova rozbalení skupin) a lifecycle Init/CacheClass (přes ServicesOverride, bez statik).
/// </summary>
public class EDInputFormViewTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void Search_ExpandsMatchedGroup_ClearRestoresExpansion()
    {
        RunSta(() =>
        {
            var g1 = new Group { Number = 1, Key = "g1", GroupName = "Linky", IsExpanded = false };
            var g2 = new Group { Number = 2, Key = "g2", GroupName = "Vozy", IsExpanded = true };
            var f1 = new Field { Number = 3, Key = "linka", Label = "Číslo linky", ParentGroup = g1 };
            var f2 = new Field { Number = 4, Key = "vuz", Label = "RZ vozu", ParentGroup = g2 };
            var form = new Form { Items = new List<Item> { g1, f1, g2, f2 } };
            RaiseLoaded(form);   // nadrátuje toolbar search

            form.Toolbar.SearchText = "linky";

            Assert.True(g1.IsExpanded);              // nalezená skupina se rozbalí
            Assert.True(g2.IsFilteredBySearch);      // nematchující zmizí
            Assert.True(f2.IsFilteredBySearch);

            form.Toolbar.SearchText = "";

            Assert.False(g1.IsExpanded);             // obnova stavu z doby před hledáním
            Assert.True(g2.IsExpanded);
            Assert.False(f2.IsFilteredBySearch);
        });
    }

    [Fact]
    public void Init_FiresSchemaLoadedAndDeprecatedAlias_CacheClassChangeResetsController()
    {
        RunSta(() =>
        {
            var mock = MLRestMocks.Create();
            mock.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>()).Returns(new CVar());
            var form = new Form
            {
                CacheClass = "ALVA.A",
                ServicesOverride = new EDServices(mock,
                    Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()),
            };
            int schemaLoaded = 0, deprecatedAlias = 0;
            form.SchemaLoaded += () => schemaLoaded++;
#pragma warning disable CS0618 // dokud deprekovaný alias existuje, musí fajrovat spolu se SchemaLoaded
            form.Initialized += () => deprecatedAlias++;
#pragma warning restore CS0618

            form.Init();

            Assert.Equal(1, schemaLoaded);
            Assert.Equal(1, deprecatedAlias);
            mock.Received(1).QueryOrThrow("ALVA.A", "Init", Arg.Any<CVar>());

            form.CacheClass = "ALVA.B";   // dokumentovaná past: setter zahodí controller
            form.Init();

            mock.Received(1).QueryOrThrow("ALVA.B", "Init", Arg.Any<CVar>());
        });
    }

    [Fact]
    public void Loaded_InitsOncePerCacheClass_ExplicitInitAlwaysRuns()
    {
        RunSta(() =>
        {
            var mock = MLRestMocks.Create();
            mock.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>()).Returns(new CVar());
            var form = new Form
            {
                CacheClass = "ALVA.A",
                ServicesOverride = new EDServices(mock,
                    Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()),
            };

            RaiseLoaded(form);
            RaiseLoaded(form);   // návrat do vizuálního stromu (přepnutí TabItem) Init neopakuje

            mock.Received(1).QueryOrThrow("ALVA.A", "Init", Arg.Any<CVar>());

            form.CacheClass = "ALVA.B";   // nová třída → další Loaded inicializuje znovu
            RaiseLoaded(form);
            RaiseLoaded(form);

            mock.Received(1).QueryOrThrow("ALVA.B", "Init", Arg.Any<CVar>());

            form.Init();   // explicitní volání hosta guard neobchází, běží vždy

            mock.Received(2).QueryOrThrow("ALVA.B", "Init", Arg.Any<CVar>());
        });
    }

    [Fact]
    public void Loaded_WithoutCacheClass_DoesNotInit_ToolbarSearchWorksFromConstruction()
    {
        RunSta(() =>
        {
            var mock = MLRestMocks.Create();
            var match = new Field { Number = 1, Key = "f", Label = "Pole" };
            var other = new Field { Number = 2, Key = "j", Label = "Jiné" };
            var form = new Form
            {
                Items = new List<Item> { match, other },
                ServicesOverride = new EDServices(mock,
                    Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()),
            };

            // Search je nadrátovaný v konstruktoru, ne na Loaded - jinak by se handlery hromadily.
            form.Toolbar.SearchText = "pole";
            Assert.False(match.IsFilteredBySearch);
            Assert.True(other.IsFilteredBySearch);

            RaiseLoaded(form);

            mock.DidNotReceiveWithAnyArgs().QueryOrThrow(default!, default!, default(CVar)!);
        });
    }

    private static void RaiseLoaded(Form form) =>
        form.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, form));

    // EDInputForm.xaml čte StaticResource EDIcons/EDStrings → Application se slovníky appky;
    // trvalé app vlákno viz EDComponentToolbarTests.RunSta.
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
