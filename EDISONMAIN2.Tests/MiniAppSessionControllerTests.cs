using EDControls.MiniApps;
using EDISONMAIN2.Desktop;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow;
using NSubstitute;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;
using static EDISONMAIN2.Tests.MiniAppTestDoubles;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Miniaplikace shellu bez ViewModelu: obnova layoutu per session (výchozí sada, Plocha, poškozený
/// layout), rozměry panelů a ukládání pod firmu, jejíž layout dock drží. Návaznost na přepnutí
/// firmy a uživatele testuje VM.
/// </summary>
public class MiniAppSessionControllerTests
{
    private const string Idfa = "CB_DP";
    private static readonly DockScope Scope = new("Srv", 57770, Idfa, "Novak");

    private static MiniAppSessionController Controller(ILocalPrefs? prefs = null, MiniAppCatalog? catalog = null) =>
        new(prefs ?? Substitute.For<ILocalPrefs>(), catalog ?? MiniAppCatalog.Build([]));

    [Fact]
    public void CloseClosable_WithoutPinned_ClosesAll()
    {
        var services = MockServices();
        var descriptor = new MiniAppDescriptor("A", "Title A", () => new StubMiniApp(services));
        var controller = Controller(catalog: MiniAppCatalog.Build([new SingleProvider(descriptor)]));
        var host = new RecordingHost();
        controller.AttachHost(host, Scope);
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
            () => new StubMiniApp(services), Pinned: true);
        var extra = new MiniAppDescriptor("Extra", "Extra", () => new StubMiniApp(services));
        var controller = Controller(catalog: MiniAppCatalog.Build([new SingleProvider(desktop, extra)]));
        var host = new RecordingHost();
        controller.AttachHost(host, Scope);   // bez uloženého layoutu otevře výchozí sadu (Plocha)
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

        controller.AttachHost(host, Scope);   // bez uloženého layoutu → výchozí sada

        var desktop = Assert.Single(host.Panes);
        Assert.StartsWith(DesktopMiniAppProvider.DesktopId + "#", desktop.ContentId);
        Assert.False(desktop.CanClose);
    }

    // Controller Plochu pozná podle Pinned, id zná jen provider, který ji registruje.
    [Fact]
    public void DefaultLayout_OpensPinnedOnly_WhateverTheirId()
    {
        var services = MockServices();
        var pinned = new MiniAppDescriptor("Jine.Pripnute", "Připnutá", () => new StubMiniApp(services), Pinned: true);
        var other = new MiniAppDescriptor("Bezna", "Běžná", () => new StubMiniApp(services));
        var controller = Controller(catalog: MiniAppCatalog.Build([new SingleProvider(other, pinned)]));
        var host = new RecordingHost();

        controller.AttachHost(host, Scope);

        Assert.StartsWith("Jine.Pripnute#", Assert.Single(host.Panes).ContentId);
    }

    [Fact]
    public void RestoredLayoutWithoutDesktop_AddsIt()
    {
        // Layout uložený verzí, kde Plocha ještě šla zavřít. RecordingHost.RestoreLayout je no-op,
        // takže po obnově není otevřené nic a Plocha se musí doplnit.
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(new Dictionary<string, string> { [Scope.Key] = "x" });
        var controller = Controller(prefs, PinnedDesktopCatalog(MockServices()));
        var host = new RecordingHost();

        controller.AttachHost(host, Scope);

        Assert.StartsWith(DesktopMiniAppProvider.DesktopId + "#", Assert.Single(host.Panes).ContentId);
    }

    [Fact]
    public void Restore_CorruptLayout_DoesNotThrow_AndDiscardsEntry()
    {
        // Poškozený uložený layout (kolegův pád: AvalonDock Deserialize hodí výjimku) nesmí
        // shodit start a má se zahodit, ať se to neopakuje.
        var layouts = new Dictionary<string, string> { [Scope.Key] = "bad-xml" };
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(layouts);
        var controller = Controller(prefs);

        var ex = Record.Exception(() => controller.AttachHost(new ThrowingHost(), Scope));

        Assert.Null(ex);
        Assert.False(layouts.ContainsKey(Scope.Key));
        prefs.Received().Save();
    }

    [Fact]
    public void Restore_CorruptLayout_DefaultFallbackAlsoThrows_StillDoesNotThrow()
    {
        // Po zahození rozbitého layoutu se staví výchozí rozložení; když spadne i to
        // (ThrowingHost.AddPane), start shellu to shodit nesmí.
        var services = MockServices();
        var desktop = new MiniAppDescriptor(DesktopMiniAppProvider.DesktopId, "Plocha",
            () => new StubMiniApp(services), Pinned: true);
        var layouts = new Dictionary<string, string> { [Scope.Key] = "bad-xml" };
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(layouts);
        var controller = Controller(prefs, MiniAppCatalog.Build([new SingleProvider(desktop)]));

        var ex = Record.Exception(() => controller.AttachHost(new ThrowingHost(), Scope));

        Assert.Null(ex);
        Assert.False(layouts.ContainsKey(Scope.Key));
    }

    [Fact]
    public void Restore_AppliesSavedTreeWidth()
    {
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.TreeWidths.Returns(new Dictionary<string, double> { [Scope.Key] = 320 });
        var controller = Controller(prefs);

        double? applied = null;
        controller.AttachHost(new NullHost(), Scope, treeWidth: new(() => 0, w => applied = w));

        Assert.Equal(320, applied);
    }

    [Fact]
    public void Restore_NoSavedTreeWidth_LeavesDefault()
    {
        var controller = Controller();   // ILocalPrefs substitute → TreeWidths null

        bool applied = false;
        controller.AttachHost(new NullHost(), Scope, treeWidth: new(() => 0, _ => applied = true));

        Assert.False(applied);
    }

    [Fact]
    public void Persist_SavesTreeWidthPerSession()
    {
        var widths = new Dictionary<string, double>();
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.TreeWidths.Returns(widths);
        var controller = Controller(prefs);
        controller.AttachHost(new NullHost(), Scope, treeWidth: new(() => 275, _ => { }));

        controller.Persist();

        Assert.Equal(275, widths[Scope.Key]);
        prefs.Received().Save();
    }

    [Fact]
    public void Restore_AppliesSavedSupportHeight()
    {
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.SupportStripHeights.Returns(new Dictionary<string, double> { [Scope.Key] = 520 });
        var controller = Controller(prefs);

        double? applied = null;
        controller.AttachHost(new NullHost(), Scope, supportHeight: new(() => 0, h => applied = h));

        Assert.Equal(520, applied);
    }

    [Fact]
    public void Restore_NoSavedSupportHeight_LeavesDefault()
    {
        var controller = Controller();   // ILocalPrefs substitute → SupportStripHeights null

        bool applied = false;
        controller.AttachHost(new NullHost(), Scope, supportHeight: new(() => 0, _ => applied = true));

        Assert.False(applied);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Restore_NonPositiveSavedSupportHeight_Ignored(double saved)
    {
        // Neplatná hodnota (poškozený soubor) nesmí pruh srazit na nulu - platí XAML default.
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.SupportStripHeights.Returns(new Dictionary<string, double> { [Scope.Key] = saved });
        var controller = Controller(prefs);

        bool applied = false;
        controller.AttachHost(new NullHost(), Scope, supportHeight: new(() => 0, _ => applied = true));

        Assert.False(applied);
    }

    [Fact]
    public void Persist_SavesSupportHeightPerSession()
    {
        var heights = new Dictionary<string, double>();
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.SupportStripHeights.Returns(heights);
        var controller = Controller(prefs);
        controller.AttachHost(new NullHost(), Scope, supportHeight: new(() => 520, _ => { }));

        controller.Persist();

        Assert.Equal(520, heights[Scope.Key]);
        prefs.Received().Save();
    }

    [Fact]
    public void Persist_WithoutSupportHeight_PersistsNothingAndDoesNotThrow()
    {
        // View nemusí rozměry napojit (testy, jiný hostitel) - save cesta musí přežít bez delegatů.
        var heights = new Dictionary<string, double>();
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.SupportStripHeights.Returns(heights);
        var controller = Controller(prefs);
        controller.AttachHost(new NullHost(), Scope);

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
        controller.AttachHost(new NullHost(), Scope);

        controller.CloseSession();
        prefs.ClearReceivedCalls();
        controller.Persist();

        Assert.Equal([Scope.Key], layouts.Keys);
        prefs.DidNotReceive().Save();

        controller.Restore(Scope with { Idfa = "JI_DP" });
        controller.Persist();

        Assert.Contains((Scope with { Idfa = "JI_DP" }).Key, layouts.Keys);
    }

    // --- klíč session (A37): server × firma × uživatel, starý klíč jen IDFa jako fallback ---

    [Fact]
    public void Restore_OnlyLegacyIdfaEntries_AreUsedAsFallback()
    {
        // local.json z doby, kdy byl stav docku jen per IDFa - po upgradu uživatel o rozložení nepřijde.
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(new Dictionary<string, string> { [Idfa] = "stary-layout" });
        prefs.TreeWidths.Returns(new Dictionary<string, double> { [Idfa] = 320 });
        prefs.SupportStripHeights.Returns(new Dictionary<string, double> { [Idfa] = 520 });
        var controller = Controller(prefs);
        var host = Substitute.For<IMiniAppHost>();
        double? width = null, height = null;

        controller.AttachHost(host, Scope, treeWidth: new(() => 0, w => width = w), supportHeight: new(() => 0, h => height = h));

        host.Received().RestoreLayout("stary-layout", Arg.Any<Func<string, IMiniAppPane, RestoredContent?>>());
        Assert.Equal((320d, 520d), (width, height));
    }

    [Fact]
    public void Restore_SessionKeyWinsOverLegacy()
    {
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(new Dictionary<string, string> { [Idfa] = "stary", [Scope.Key] = "novy" });
        var controller = Controller(prefs);
        var host = Substitute.For<IMiniAppHost>();

        controller.AttachHost(host, Scope);

        host.Received().RestoreLayout("novy", Arg.Any<Func<string, IMiniAppPane, RestoredContent?>>());
        host.DidNotReceive().RestoreLayout("stary", Arg.Any<Func<string, IMiniAppPane, RestoredContent?>>());
    }

    [Fact]
    public void Persist_WritesSessionKeyOnly_LegacyEntryStaysForOthers()
    {
        // Starý záznam čtou jako fallback i ostatní uživatelé stanice, než si uloží vlastní.
        var layouts = new Dictionary<string, string> { [Idfa] = "stary" };
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(layouts);
        var controller = Controller(prefs);
        controller.AttachHost(new NullHost(), Scope);

        controller.Persist();

        Assert.Equal("stary", layouts[Idfa]);
        Assert.Contains(Scope.Key, layouts.Keys);
    }

    [Fact]
    public void Restore_SameCompanyAnotherUser_DoesNotGetFirstUsersLayout()
    {
        // Sdílený terminál: uživatel B ve stejné firmě nesmí dostat (a pak přepsat) dock uživatele A.
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(new Dictionary<string, string> { [Scope.Key] = "layout-novaka" });
        var controller = Controller(prefs);
        var host = Substitute.For<IMiniAppHost>();

        controller.AttachHost(host, Scope with { Login = "Svoboda" });

        host.DidNotReceive().RestoreLayout(Arg.Any<string>(), Arg.Any<Func<string, IMiniAppPane, RestoredContent?>>());
    }

    [Fact]
    public void Restore_CorruptLegacyLayout_DiscardsTheLegacyEntry()
    {
        var layouts = new Dictionary<string, string> { [Idfa] = "bad-xml" };
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(layouts);
        var controller = Controller(prefs);

        controller.AttachHost(new ThrowingHost(), Scope);

        Assert.Empty(layouts);
    }

    [Fact]
    public void DockScope_Key_IgnoresCaseOfHostAndLogin()
    {
        Assert.Equal(new DockScope("SRV", 57770, "CB_DP", "Novak").Key, new DockScope("srv", 57770, "CB_DP", "novak").Key);
        Assert.NotEqual(Scope.Key, (Scope with { Port = 57772 }).Key);
    }
}
