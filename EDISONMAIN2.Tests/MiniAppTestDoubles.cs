using System.Windows.Media;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDISONMAIN2.MiniApps;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>Dvojníci docking hostu a katalogu pro testy miniaplikací shellu (controller i VM).</summary>
internal static class MiniAppTestDoubles
{
    internal static MiniAppCatalog PinnedDesktopCatalog(EDServices services) =>
        MiniAppCatalog.Build([new SingleProvider(
            new MiniAppDescriptor(DesktopMiniAppProvider.DesktopId, "Plocha",
                () => new StubMiniApp(services), Pinned: true))]);

    internal sealed class StubMiniApp(EDServices services) : EDMiniAppViewModel(services)
    {
        public override string Title => "stub";
    }

    internal sealed class SingleProvider(params MiniAppDescriptor[] descriptors) : IMiniAppProvider
    {
        public string GroupTitle => "Test";
        public IEnumerable<MiniAppDescriptor> GetMiniApps() => descriptors;
    }

    internal sealed class RecordingHost : IMiniAppHost
    {
        public List<RecordingPane> Panes { get; } = [];
        public int SkeletonResets { get; private set; }

        public IMiniAppPane AddPane(string contentId, string title, ImageSource? icon, object content, Action onClosed,
                                    MiniAppPlacement? placement = null, bool canClose = true)
        {
            var pane = new RecordingPane(contentId, onClosed) { Placement = placement, CanClose = canClose };
            Panes.Add(pane);
            return pane;
        }

        public IReadOnlyList<MiniAppTarget> GetTargets() =>
            [.. Panes.Where(p => !p.IsClosed).Select(p => new MiniAppTarget(p, [p.ContentId], false))];

        public void ResetToDefaultSkeleton() => SkeletonResets++;
        public string SaveLayout() => "";
        public void RestoreLayout(string layoutXml, Func<string, IMiniAppPane, RestoredContent?> resolve) { }
    }

    internal sealed class RecordingPane(string contentId, Action onClosed) : IMiniAppPane
    {
        public string ContentId => contentId;
        public MiniAppPlacement? Placement { get; set; }
        public bool CanClose { get; set; } = true;
        public bool IsClosed { get; private set; }

        public void Close()
        {
            if (IsClosed) return;
            IsClosed = true;
            onClosed();
        }
    }

    internal sealed class ThrowingHost : IMiniAppHost
    {
        public IMiniAppPane AddPane(string contentId, string title, ImageSource? icon, object content, Action onClosed,
                                    MiniAppPlacement? placement = null, bool canClose = true)
            => throw new NotSupportedException();
        public IReadOnlyList<MiniAppTarget> GetTargets() => [];
        public void ResetToDefaultSkeleton() { }
        public string SaveLayout() => "";
        public void RestoreLayout(string layoutXml, Func<string, IMiniAppPane, RestoredContent?> resolve)
            => throw new InvalidOperationException("simulovaný rozbitý layout");
    }

    // Neškodný host - prázdný katalog v testech znamená, že AddPane se nikdy nezavolá.
    internal sealed class NullHost : IMiniAppHost
    {
        public IMiniAppPane AddPane(string contentId, string title, ImageSource? icon, object content, Action onClosed,
                                    MiniAppPlacement? placement = null, bool canClose = true)
            => Substitute.For<IMiniAppPane>();
        public IReadOnlyList<MiniAppTarget> GetTargets() => [];
        public void ResetToDefaultSkeleton() { }
        public string SaveLayout() => "";
        public void RestoreLayout(string layoutXml, Func<string, IMiniAppPane, RestoredContent?> resolve) { }
    }
}
