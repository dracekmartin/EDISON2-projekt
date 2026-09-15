using System.Windows;
using ALWA.SYS;
using EDControls.EDInputForm;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;
using Form = EDControls.EDInputForm.EDInputForm;

namespace EDControls.Tests;

/// <summary>
/// Code-behind EDInputFormu, který nejde pokrýt bez controlu: hledání (obnova rozbalení,
/// počítadla, wraparound) a lifecycle Init/CacheClass (přes ServicesOverride, bez statik).
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
            Assert.Equal(1, form.Toolbar.TotalMatches);
            Assert.Equal(1, form.Toolbar.CurrentMatchIndex);

            form.Toolbar.SearchText = "";

            Assert.False(g1.IsExpanded);             // obnova stavu z doby před hledáním
            Assert.True(g2.IsExpanded);
            Assert.False(f2.IsFilteredBySearch);
            Assert.Equal(0, form.Toolbar.TotalMatches);
            Assert.Equal(0, form.Toolbar.CurrentMatchIndex);
        });
    }

    [Fact]
    public void FindNextAndPrevious_WrapAround()
    {
        RunSta(() =>
        {
            var fields = Enumerable.Range(1, 3)
                .Select(i => new Field { Number = i, Key = $"f{i}", Label = $"Pole {i}" })
                .ToList();
            var form = new Form { Items = fields.Cast<Item>().ToList() };
            RaiseLoaded(form);

            form.Toolbar.SearchText = "pole";
            Assert.Equal(3, form.Toolbar.TotalMatches);
            Assert.Equal(1, form.Toolbar.CurrentMatchIndex);

            form.Toolbar.FindNextCommand!.Execute(null);
            Assert.Equal(2, form.Toolbar.CurrentMatchIndex);
            form.Toolbar.FindNextCommand.Execute(null);
            Assert.Equal(3, form.Toolbar.CurrentMatchIndex);
            form.Toolbar.FindNextCommand.Execute(null);
            Assert.Equal(1, form.Toolbar.CurrentMatchIndex);   // z posledního na první

            form.Toolbar.FindPreviousCommand!.Execute(null);
            Assert.Equal(3, form.Toolbar.CurrentMatchIndex);   // z prvního na poslední
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
