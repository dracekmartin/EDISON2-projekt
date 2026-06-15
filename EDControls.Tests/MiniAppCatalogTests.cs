using EDControls.MiniApps;

namespace EDControls.Tests;

/// <summary>
/// Testy agregace <see cref="MiniAppCatalog"/>. Katalog továrnu (<c>CreateViewModel</c>) nikdy
/// nevolá — proto stačí descriptory s no-op factory.
/// </summary>
public class MiniAppCatalogTests
{
    private static MiniAppDescriptor Descriptor(string id) => new(id, () => null!);

    private sealed class FakeProvider(params MiniAppDescriptor[] apps) : IMiniAppProvider
    {
        public IEnumerable<MiniAppDescriptor> GetMiniApps() => apps;
    }

    [Fact]
    public void Build_AggregatesDescriptorsFromAllProviders()
    {
        var catalog = MiniAppCatalog.Build([
            new FakeProvider(Descriptor("A"), Descriptor("B")),
            new FakeProvider(Descriptor("C")),
        ]);

        Assert.Equal(3, catalog.All.Count);
    }

    [Fact]
    public void Build_EmptyProviders_YieldsEmptyCatalog()
    {
        var catalog = MiniAppCatalog.Build([]);

        Assert.Empty(catalog.All);
    }

    [Fact]
    public void Find_ReturnsDescriptorById()
    {
        var b = Descriptor("B");
        var catalog = MiniAppCatalog.Build([new FakeProvider(Descriptor("A"), b)]);

        Assert.Same(b, catalog.Find("B"));
    }

    [Fact]
    public void Find_UnknownId_ReturnsNull()
    {
        var catalog = MiniAppCatalog.Build([new FakeProvider(Descriptor("A"))]);

        Assert.Null(catalog.Find("X"));
    }

    [Fact]
    public void Build_DuplicateId_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MiniAppCatalog.Build([
                new FakeProvider(Descriptor("A")),
                new FakeProvider(Descriptor("A")),
            ]));

        Assert.Contains("A", ex.Message);
    }
}
