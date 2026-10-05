using EDControls.MiniApps;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow;
using NSubstitute;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;
using static EDISONMAIN2.Tests.MiniAppTestDoubles;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Miniaplikace shellu bez ViewModelu: obnova layoutu per IDFa (výchozí sada, Plocha, poškozený
/// layout), rozměry panelů a ukládání pod firmu, jejíž layout dock drží. Návaznost na přepnutí
/// firmy a uživatele testuje VM.
/// </summary>
public class MiniAppSessionControllerTests
{
    private const string Idfa = "CB_DP";

    private static MiniAppSessionController Controller(ILocalPrefs? prefs = null, MiniAppCatalog? catalog = null) =>
        new(prefs ?? Substitute.For<ILocalPrefs>(), catalog ?? MiniAppCatalog.Build([]));

    [Fact]
    public void CloseClosable_WithoutPinned_ClosesAll()
    {
        var services = MockServices();
        var descriptor = new MiniAppDescriptor("A", "Title A", () => new StubMiniApp(services));
        var controller = Controller(catalog: MiniAppCatalog.Build([new SingleProvider(descriptor)]));
        var host = new RecordingHost();
        controller.AttachHost(host, Idfa);
        controller.Open(descriptor);
        controller.Open(descriptor);
        Assert.Equal(2, host.Panes.Count);

        controller.CloseClosable();

        // Katalog nemá pinned, takže „zavřít ostatní" tu zavře všechno.
        Assert.All(host.Panes, p => Assert.True(p.IsClosed));
    }

    [Fact]
    public void Reset_ClosesEverything_AndOpensDefaultSet()
    {
        var services = MockServices();
        var desktop = new MiniAppDescriptor(DesktopMiniAppProvider.DesktopId, "Plocha",
            () => new StubMiniApp(services));
        var extra = new MiniAppDescriptor("Extra", "Extra", () => new StubMiniApp(services));
        var controller = Controller(catalog: MiniAppCatalog.Build([new SingleProvider(desktop, extra)]));
        var host = new RecordingHost();
        controller.AttachHost(host, Idfa);   // bez uloženého layoutu otevře výchozí sadu (Plocha)
        controller.Open(extra);
        var beforeReset = host.Panes.ToList();
        Assert.Equal(2, beforeReset.Count);

        controller.Reset();

        Assert.All(beforeReset, p => Assert.True(p.IsClosed));
        Assert.Equal(2, host.SkeletonResets);   // attach + reset
        var reopened = host.Panes.Where(p => !p.IsClosed).Select(p => p.ContentId).ToList();
        Assert.StartsWith(DesktopMiniAppProvider.DesktopId + "#", Assert.Single(reopened));
    }

    [Fact]
    public void DefaultLayout_OpensDesktop_UserCannotCloseIt()
    {
        var controller = Controller(catalog: PinnedDesktopCatalog(MockServices()));
        var host = new RecordingHost();

        controller.AttachHost(host, Idfa);   // bez uloženého layoutu → výchozí sada

        var desktop = Assert.Single(host.Panes);
        Assert.StartsWith(DesktopMiniAppProvider.DesktopId + "#", desktop.ContentId);
        Assert.False(desktop.CanClose);
    }

    [Fact]
    public void RestoredLayoutWithoutDesktop_AddsIt()
    {
        // Layout uložený verzí, kde Plocha ještě šla zavřít. RecordingHost.RestoreLayout je no-op,
        // takže po obnově není otevřené nic a Plocha se musí doplnit.
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(new Dictionary<string, string> { [Idfa] = "x" });
        var controller = Controller(prefs, PinnedDesktopCatalog(MockServices()));
        var host = new RecordingHost();

        controller.AttachHost(host, Idfa);

        Assert.StartsWith(DesktopMiniAppProvider.DesktopId + "#", Assert.Single(host.Panes).ContentId);
    }

    [Fact]
    public void Restore_CorruptLayout_DoesNotThrow_AndDiscardsEntry()
    {
        // Poškozený uložený layout (kolegův pád: AvalonDock Deserialize hodí výjimku) nesmí
        // shodit start a má se zahodit, ať se to neopakuje.
        var layouts = new Dictionary<string, string> { [Idfa] = "bad-xml" };
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(layouts);
        var controller = Controller(prefs);

        var ex = Record.Exception(() => controller.AttachHost(new ThrowingHost(), Idfa));

        Assert.Null(ex);
        Assert.False(layouts.ContainsKey(Idfa));
        prefs.Received().Save();
    }

    [Fact]
    public void Restore_CorruptLayout_DefaultFallbackAlsoThrows_StillDoesNotThrow()
    {
        // Po zahození rozbitého layoutu se staví výchozí rozložení; když spadne i to
        // (ThrowingHost.AddPane), start shellu to shodit nesmí.
        var services = MockServices();
        var desktop = new MiniAppDescriptor(DesktopMiniAppProvider.DesktopId, "Plocha",
            () => new StubMiniApp(services));
        var layouts = new Dictionary<string, string> { [Idfa] = "bad-xml" };
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(layouts);
        var controller = Controller(prefs, MiniAppCatalog.Build([new SingleProvider(desktop)]));

        var ex = Record.Exception(() => controller.AttachHost(new ThrowingHost(), Idfa));

        Assert.Null(ex);
        Assert.False(layouts.ContainsKey(Idfa));
    }

    [Fact]
    public void Restore_AppliesSavedTreeWidth()
    {
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.TreeWidths.Returns(new Dictionary<string, double> { [Idfa] = 320 });
        var controller = Controller(prefs);

        double? applied = null;
        controller.AttachTreeLayout(() => 0, w => applied = w);
        controller.AttachHost(new NullHost(), Idfa);

        Assert.Equal(320, applied);
    }

    [Fact]
    public void Restore_NoSavedTreeWidth_LeavesDefault()
    {
        var controller = Controller();   // ILocalPrefs substitute → TreeWidths null

        bool applied = false;
        controller.AttachTreeLayout(() => 0, _ => applied = true);
        controller.AttachHost(new NullHost(), Idfa);

        Assert.False(applied);
    }

    [Fact]
    public void Persist_SavesTreeWidthPerIdfa()
    {
        var widths = new Dictionary<string, double>();
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.TreeWidths.Returns(widths);
        var controller = Controller(prefs);
        controller.AttachTreeLayout(() => 275, _ => { });
        controller.AttachHost(new NullHost(), Idfa);

        controller.Persist();

        Assert.Equal(275, widths[Idfa]);
        prefs.Received().Save();
    }

    [Fact]
    public void Restore_AppliesSavedSupportHeight()
    {
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.SupportStripHeights.Returns(new Dictionary<string, double> { [Idfa] = 520 });
        var controller = Controller(prefs);

        double? applied = null;
        controller.AttachSupportLayout(() => 0, h => applied = h);
        controller.AttachHost(new NullHost(), Idfa);

        Assert.Equal(520, applied);
    }

    [Fact]
    public void Restore_NoSavedSupportHeight_LeavesDefault()
    {
        var controller = Controller();   // ILocalPrefs substitute → SupportStripHeights null

        bool applied = false;
        controller.AttachSupportLayout(() => 0, _ => applied = true);
        controller.AttachHost(new NullHost(), Idfa);

        Assert.False(applied);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Restore_NonPositiveSavedSupportHeight_Ignored(double saved)
    {
        // Neplatná hodnota (poškozený soubor) nesmí pruh srazit na nulu - platí XAML default.
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.SupportStripHeights.Returns(new Dictionary<string, double> { [Idfa] = saved });
        var controller = Controller(prefs);

        bool applied = false;
        controller.AttachSupportLayout(() => 0, _ => applied = true);
        controller.AttachHost(new NullHost(), Idfa);

        Assert.False(applied);
    }

    [Fact]
    public void Persist_SavesSupportHeightPerIdfa()
    {
        var heights = new Dictionary<string, double>();
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.SupportStripHeights.Returns(heights);
        var controller = Controller(prefs);
        controller.AttachSupportLayout(() => 520, _ => { });
        controller.AttachHost(new NullHost(), Idfa);

        controller.Persist();

        Assert.Equal(520, heights[Idfa]);
        prefs.Received().Save();
    }

    [Fact]
    public void Persist_WithoutAttachSupportLayout_PersistsNothingAndDoesNotThrow()
    {
        // View nemusí rozměry napojit (testy, jiný hostitel) - save cesta musí přežít bez delegatů.
        var heights = new Dictionary<string, double>();
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.SupportStripHeights.Returns(heights);
        var controller = Controller(prefs);
        controller.AttachHost(new NullHost(), Idfa);

        controller.Persist();

        Assert.Empty(heights);
    }

    [Fact]
    public void CloseSession_SavesUnderDockCompany_ThenEmptyDockIsNotSaved_UntilRestore()
    {
        // Mezi výměnou docku a obnovou nové firmy je dock prázdný; jeho uložení by přepsalo layout.
        var layouts = new Dictionary<string, string>();
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(layouts);
        var controller = Controller(prefs);
        controller.AttachHost(new NullHost(), Idfa);

        controller.CloseSession();
        prefs.ClearReceivedCalls();
        controller.Persist();

        Assert.Equal([Idfa], layouts.Keys);
        prefs.DidNotReceive().Save();

        controller.Restore("JI_DP");
        controller.Persist();

        Assert.Contains("JI_DP", layouts.Keys);
    }
}
