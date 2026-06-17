using System.Windows.Media;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDISONMAIN2.MiniApps;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy <see cref="MiniAppManager"/> přes fake host — manager je tak testovatelný mimo STA
/// vlákno (AvalonDock-specifika jsou v <c>AvalonDockHost</c>, ten se neтestuje unit testy).
/// </summary>
public class MiniAppManagerTests
{
    private static EDServices MockServices() =>
        new(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

    private static MiniAppDescriptor SampleDescriptor(EDServices services) =>
        new("X", "test", () => new TestMiniApp(services));

    [Fact]
    public void Open_TracksInstance_AndCallsOnOpened()
    {
        var host = new FakeHost();
        var mgr = new MiniAppManager(host);

        var inst = mgr.Open(SampleDescriptor(MockServices()));

        Assert.Single(mgr.OpenInstances);
        Assert.Single(host.Panes);
        Assert.Equal(1, ((TestMiniApp)inst.ViewModel).Opened);
    }

    [Fact]
    public void Open_MultiInstance_UniqueContentIds()
    {
        var host = new FakeHost();
        var mgr = new MiniAppManager(host);
        var d = SampleDescriptor(MockServices());

        mgr.Open(d);
        mgr.Open(d);

        Assert.Equal(2, mgr.OpenInstances.Count);
        Assert.Equal("X#1", mgr.OpenInstances[0].ContentId);
        Assert.Equal("X#2", mgr.OpenInstances[1].ContentId);
    }

    [Fact]
    public void Open_PassesVmAsContent_WithTitleAndContentId()
    {
        var host = new FakeHost();
        var mgr = new MiniAppManager(host);

        var inst = mgr.Open(SampleDescriptor(MockServices()));

        Assert.Same(inst.ViewModel, host.Panes[0].Content);
        Assert.Equal("test", host.Panes[0].Title);
        Assert.Equal("X#1", host.Panes[0].ContentId);
    }

    [Fact]
    public void CloseSelf_ClosesPane_Untracks_AndCallsOnClosed()
    {
        var host = new FakeHost();
        var mgr = new MiniAppManager(host);
        var vm = (TestMiniApp)mgr.Open(SampleDescriptor(MockServices())).ViewModel;

        vm.TriggerCloseSelf();

        Assert.Empty(mgr.OpenInstances);
        Assert.True(host.Panes[0].IsClosed);
        Assert.Equal(1, vm.Closed);
    }

    [Fact]
    public void PaneClosedByUser_Untracks_AndCallsOnClosed()
    {
        var host = new FakeHost();
        var mgr = new MiniAppManager(host);
        var vm = (TestMiniApp)mgr.Open(SampleDescriptor(MockServices())).ViewModel;

        host.Panes[0].Close();   // simuluje uživatelské zavření (křížek)

        Assert.Empty(mgr.OpenInstances);
        Assert.Equal(1, vm.Closed);
    }

    [Fact]
    public void CloseAll_ClosesAndUntracksEverything()
    {
        var host = new FakeHost();
        var mgr = new MiniAppManager(host);
        var d = SampleDescriptor(MockServices());
        mgr.Open(d);
        mgr.Open(d);

        mgr.CloseAll();

        Assert.Empty(mgr.OpenInstances);
        Assert.All(host.Panes, p => Assert.True(p.IsClosed));
    }

    [Fact]
    public void OnSessionReplaced_ClosesAllOpenMiniApps()
    {
        var host = new FakeHost();
        var mgr = new MiniAppManager(host);
        var d = SampleDescriptor(MockServices());
        var vm = (TestMiniApp)mgr.Open(d).ViewModel;
        mgr.Open(d);

        mgr.OnSessionReplaced();

        Assert.Empty(mgr.OpenInstances);
        Assert.Equal(1, vm.Closed);
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

    private sealed class FakeHost : IMiniAppHost
    {
        public List<FakePane> Panes { get; } = [];

        public IMiniAppPane AddPane(string contentId, string title, ImageSource? icon, object content, Action onClosed)
        {
            var pane = new FakePane(contentId, title, content, onClosed);
            Panes.Add(pane);
            return pane;
        }
    }

    private sealed class FakePane(string contentId, string title, object content, Action onClosed) : IMiniAppPane
    {
        public string ContentId => contentId;
        public string Title => title;
        public object Content => content;
        public bool IsClosed { get; private set; }

        public void Close()
        {
            if (IsClosed) return;
            IsClosed = true;
            onClosed();
        }
    }
}
