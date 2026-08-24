using System.Windows.Media;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDISONMAIN2.MiniApps;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy <see cref="MiniAppManager"/> přes fake host - manager je tak testovatelný mimo STA
/// vlákno (AvalonDock-specifika jsou v <c>AvalonDockHost</c>, ten se netestuje unit testy).
/// </summary>
public class MiniAppManagerTests
{
    private static EDServices MockServices() =>
        new(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

    /// <summary>Sdílený katalog + descriptor „X" (factory dělá <see cref="TestMiniApp"/>).</summary>
    private sealed class Fixture
    {
        public MiniAppDescriptor Descriptor { get; }
        public MiniAppCatalog Catalog { get; }

        public Fixture()
        {
            var services = MockServices();
            Descriptor = new("X", "test", () => new TestMiniApp(services));
            Catalog = MiniAppCatalog.Build([new SingleProvider(Descriptor)]);
        }

        public (MiniAppManager mgr, FakeHost host) NewManager()
        {
            var host = new FakeHost();
            return (new MiniAppManager(host, Catalog), host);
        }
    }

    [Fact]
    public void Open_TracksInstance_AndCallsOnOpened()
    {
        var fx = new Fixture();
        var (mgr, host) = fx.NewManager();

        var inst = mgr.Open(fx.Descriptor);

        Assert.Single(mgr.OpenInstances);
        Assert.Single(host.Panes);
        Assert.Equal(1, ((TestMiniApp)inst.ViewModel).Opened);
    }

    [Fact]
    public void Open_MultiInstance_UniqueContentIds()
    {
        var fx = new Fixture();
        var (mgr, _) = fx.NewManager();

        mgr.Open(fx.Descriptor);
        mgr.Open(fx.Descriptor);

        Assert.Equal(["X#1", "X#2"], mgr.OpenInstances.Select(i => i.ContentId));
    }

    [Fact]
    public void Open_PassesVmAsContent_WithTitleAndContentId()
    {
        var fx = new Fixture();
        var (mgr, host) = fx.NewManager();

        var inst = mgr.Open(fx.Descriptor);

        Assert.Same(inst.ViewModel, host.Panes[0].Content);
        Assert.Equal("test", host.Panes[0].Title);
        Assert.Equal("X#1", host.Panes[0].ContentId);
    }

    [Fact]
    public void CloseSelf_ClosesPane_Untracks_AndCallsOnClosed()
    {
        var fx = new Fixture();
        var (mgr, host) = fx.NewManager();
        var vm = (TestMiniApp)mgr.Open(fx.Descriptor).ViewModel;

        vm.TriggerCloseSelf();

        Assert.Empty(mgr.OpenInstances);
        Assert.True(host.Panes[0].IsClosed);
        Assert.Equal(1, vm.Closed);
    }

    [Fact]
    public void PaneClosedByUser_Untracks_AndCallsOnClosed()
    {
        var fx = new Fixture();
        var (mgr, host) = fx.NewManager();
        var vm = (TestMiniApp)mgr.Open(fx.Descriptor).ViewModel;

        host.Panes[0].Close();   // simuluje uživatelské zavření (křížek)

        Assert.Empty(mgr.OpenInstances);
        Assert.Equal(1, vm.Closed);
    }

    [Fact]
    public void CloseAll_ClosesAndUntracksEverything()
    {
        var fx = new Fixture();
        var (mgr, host) = fx.NewManager();
        mgr.Open(fx.Descriptor);
        mgr.Open(fx.Descriptor);

        mgr.CloseAll();

        Assert.Empty(mgr.OpenInstances);
        Assert.All(host.Panes, p => Assert.True(p.IsClosed));
    }

    [Fact]
    public void RestoreLayout_ReinstantiatesPanesByContentId()
    {
        var fx = new Fixture();
        var (mgr1, _) = fx.NewManager();
        mgr1.Open(fx.Descriptor);
        mgr1.Open(fx.Descriptor);
        var layout = mgr1.SaveLayout();

        var (mgr2, _) = fx.NewManager();
        mgr2.RestoreLayout(layout);

        Assert.Equal(["X#1", "X#2"], mgr2.OpenInstances.Select(i => i.ContentId));
        Assert.All(mgr2.OpenInstances, i => Assert.Equal(1, ((TestMiniApp)i.ViewModel).Opened));
    }

    [Fact]
    public void RestoreLayout_BumpsCounter_SoNewOpenDoesNotCollide()
    {
        var fx = new Fixture();
        var (mgr, _) = fx.NewManager();
        mgr.RestoreLayout("X#1,X#2");

        var inst = mgr.Open(fx.Descriptor);

        Assert.Equal("X#3", inst.ContentId);
    }

    [Fact]
    public void RestoreLayout_DropsUnknownDescriptor()
    {
        var fx = new Fixture();
        var (mgr, _) = fx.NewManager();

        mgr.RestoreLayout("Unknown#1,X#1");

        Assert.Equal("X#1", Assert.Single(mgr.OpenInstances).ContentId);
    }

    [Fact]
    public void OpenColumns_OpensFirstInPlace_RestInNewColumns()
    {
        var fx = new Fixture();
        var (mgr, host) = fx.NewManager();

        mgr.OpenColumns([fx.Descriptor, fx.Descriptor, fx.Descriptor]);

        Assert.Equal(3, mgr.OpenInstances.Count);
        Assert.Equal([false, true, true], host.Panes.Select(p => p.NewColumn));
    }

    [Fact]
    public void OpenDefaultLayout_ResetsSkeleton_ColumnsThenBottomRow()
    {
        var fx = new Fixture();
        var (mgr, host) = fx.NewManager();

        mgr.OpenDefaultLayout([fx.Descriptor, fx.Descriptor, fx.Descriptor], fx.Descriptor);

        Assert.Equal(1, host.SkeletonResets);
        Assert.Equal(4, mgr.OpenInstances.Count);
        // 3 sloupce (první in-place, další 2 nové sloupce) + 1 řada dole.
        Assert.Equal([false, true, true, false], host.Panes.Select(p => p.NewColumn));
        Assert.Equal([false, false, false, true], host.Panes.Select(p => p.NewRow));
    }

    [Fact]
    public void OpenDefaultLayout_NullBottomRow_OpensOnlyColumns()
    {
        var fx = new Fixture();
        var (mgr, host) = fx.NewManager();

        mgr.OpenDefaultLayout([fx.Descriptor], null);

        Assert.Single(mgr.OpenInstances);
        Assert.DoesNotContain(host.Panes, p => p.NewRow);
    }

    [Fact]
    public void InstancesChanged_FiresOnOpenAndClose()
    {
        var fx = new Fixture();
        var (mgr, host) = fx.NewManager();
        int fired = 0;
        mgr.InstancesChanged += () => fired++;

        mgr.Open(fx.Descriptor);
        host.Panes[0].Close();

        Assert.Equal(2, fired);
    }

    [Fact]
    public void RefreshAll_RefreshesOnlyRefreshableInstances()
    {
        var services = MockServices();
        var plain = new MiniAppDescriptor("X", "plain", () => new TestMiniApp(services));
        var refreshable = new MiniAppDescriptor("R", "refreshable", () => new RefreshableMiniApp(services));
        var catalog = MiniAppCatalog.Build([new SingleProvider(plain), new SingleProvider(refreshable)]);
        var mgr = new MiniAppManager(new FakeHost(), catalog);

        mgr.Open(plain);
        var r1 = (RefreshableMiniApp)mgr.Open(refreshable).ViewModel;
        var r2 = (RefreshableMiniApp)mgr.Open(refreshable).ViewModel;

        var refreshed = mgr.RefreshAll();
        Assert.Equal(["refreshable", "refreshable"], refreshed);
        Assert.Equal(1, r1.Refreshed);
        Assert.Equal(1, r2.Refreshed);
    }

    private sealed class TestMiniApp(EDServices services) : EDMiniAppViewModel(services)
    {
        public override string Title => "test";
        public int Opened { get; private set; }
        public int Closed { get; private set; }
        public override void OnOpened() => Opened++;
        public override void OnClosed() => Closed++;
        public void TriggerCloseSelf() => CloseSelf();
    }

    private sealed class RefreshableMiniApp(EDServices services) : EDMiniAppViewModel(services), IRefreshableMiniApp
    {
        public override string Title => "refreshable";
        public int Refreshed { get; private set; }
        public void Refresh() => Refreshed++;
    }

    private sealed class SingleProvider(MiniAppDescriptor descriptor) : IMiniAppProvider
    {
        public string GroupTitle => "Test";
        public IEnumerable<MiniAppDescriptor> GetMiniApps() => [descriptor];
    }

    private sealed class FakeHost : IMiniAppHost
    {
        public List<FakePane> Panes { get; } = [];
        public int SkeletonResets { get; private set; }

        public IMiniAppPane AddPane(string contentId, string title, ImageSource? icon, object content, Action onClosed,
                                    bool newColumn = false, bool newRow = false)
        {
            var pane = new FakePane(contentId) { Title = title, Content = content, OnClosed = onClosed, NewColumn = newColumn, NewRow = newRow };
            Panes.Add(pane);
            return pane;
        }

        public void ResetToDefaultSkeleton() => SkeletonResets++;

        public string SaveLayout() =>
            string.Join(",", Panes.Where(p => !p.IsClosed).Select(p => p.ContentId));

        public void RestoreLayout(string layoutXml, Func<string, IMiniAppPane, RestoredContent?> resolve)
        {
            foreach (var contentId in layoutXml.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var pane = new FakePane(contentId);
                if (resolve(contentId, pane) is not { } rc) continue;
                pane.Content = rc.Content;
                pane.OnClosed = rc.OnClosed;
                Panes.Add(pane);
            }
        }
    }

    private sealed class FakePane(string contentId) : IMiniAppPane
    {
        public string ContentId => contentId;
        public string? Title { get; set; }
        public object? Content { get; set; }
        public Action? OnClosed { get; set; }
        public bool NewColumn { get; set; }
        public bool NewRow { get; set; }
        public bool IsClosed { get; private set; }

        public void Close()
        {
            if (IsClosed) return;
            IsClosed = true;
            OnClosed?.Invoke();
        }
    }
}
