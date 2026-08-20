using EDControls.MiniApps;

namespace EDControls.Tests;

/// <summary>
/// Testy agregace <see cref="MiniAppCatalog"/>. Katalog továrnu (<c>CreateViewModel</c>) nikdy
/// nevolá - proto stačí descriptory s no-op factory.
/// </summary>
public class MiniAppCatalogTests
{
    private static MiniAppDescriptor Descriptor(string id) => new(id, id, () => null!);

    private sealed class FakeProvider(params MiniAppDescriptor[] apps) : IMiniAppProvider
    {
        public string GroupTitle { get; init; } = "Skupina";
        public IEnumerable<MiniAppDescriptor> GetMiniApps() => apps;
    }

    [Fact]
    public void Build_AggregatesDescriptorsFromAllProviders()
    {
        var catalog = MiniAppCatalog.Build([
            new FakeProvider(Descriptor("A"), Descriptor("B")),
            new FakeProvider(Descriptor("C")),
        ]);

        Assert.Equal(
            new HashSet<string> { "A", "B", "C" },
            catalog.All.Select(d => d.Id).ToHashSet());
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
    public void Build_GroupsFollowProviderRegistrationOrder()
    {
        var catalog = MiniAppCatalog.Build([
            new FakeProvider(Descriptor("A")) { GroupTitle = "První" },
            new FakeProvider(Descriptor("B")) { GroupTitle = "Druhá" },
        ]);

        Assert.Equal(["První", "Druhá"], catalog.Groups.Select(g => g.Title));
    }

    [Fact]
    public void Build_GroupContainsProviderDescriptorsInOrder()
    {
        var a = Descriptor("A");
        var b = Descriptor("B");
        var catalog = MiniAppCatalog.Build([new FakeProvider(a, b)]);

        var group = Assert.Single(catalog.Groups);
        Assert.Equal([a, b], group.Apps);
    }

    [Fact]
    public void Build_EmptyProvider_YieldsNoGroup()
    {
        var catalog = MiniAppCatalog.Build([
            new FakeProvider() { GroupTitle = "Prázdná" },
            new FakeProvider(Descriptor("A")) { GroupTitle = "Plná" },
        ]);

        Assert.Equal("Plná", Assert.Single(catalog.Groups).Title);
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
