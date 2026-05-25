using System.Windows;
using EDControls.EDViewModel;

namespace EDControls.Tests;

/// <summary>
/// Testy registračních invariantů — pouze ty co nepotřebují vytvořit real Window (STA thread).
/// Faktická factory invocation s reálným oknem se ověřuje až v produkci přes
/// AppBootstrapper.RegisterShellWindows + smoke test.
/// </summary>
public class WindowRegistryTests
{
    [Fact]
    public void Register_DuplicateKey_Throws()
    {
        var registry = new WindowRegistry();
        registry.Register("Key", _ => null!);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.Register("Key", _ => null!));
        Assert.Contains("Key", ex.Message);
    }

    [Fact]
    public void Create_UnknownKey_Throws()
    {
        var registry = new WindowRegistry();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.Create("Missing", input: null));
        Assert.Contains("Missing", ex.Message);
        Assert.Contains("Register", ex.Message);
    }

    [Fact]
    public void Register_DifferentKeys_Coexist()
    {
        var registry = new WindowRegistry();
        registry.Register("A", _ => null!);
        registry.Register("B", _ => null!);

        // Pokud by druhá registrace přepsala první, dostali bychom InvalidOperationException —
        // ale jiný klíč musí jít. Bez výjimky = success.
        var ex = Record.Exception(() => registry.Register("C", _ => null!));
        Assert.Null(ex);
    }
}
