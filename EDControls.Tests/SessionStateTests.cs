using EDControls.SessionState;

namespace EDControls.Tests;

/// <summary>Přechodná paměť procesu - scope × key, null zapomíná.</summary>
public class SessionStateTests
{
    [Fact]
    public void UnknownKey_ReturnsNull() =>
        Assert.Null(new InMemorySessionState().Get("scope", "key"));

    [Fact]
    public void Set_IsReadBackUnderSameScopeAndKey()
    {
        var state = new InMemorySessionState();

        state.Set("scope", "key", "font");

        Assert.Equal("font", state.Get("scope", "key"));
    }

    [Fact]
    public void ScopesAndKeysDoNotSeeEachOther()
    {
        var state = new InMemorySessionState();

        state.Set("a", "key", "prvni");
        state.Set("b", "key", "druha");
        state.Set("a", "jiny", "treti");

        Assert.Equal("prvni", state.Get("a", "key"));
        Assert.Equal("druha", state.Get("b", "key"));
        Assert.Equal("treti", state.Get("a", "jiny"));
    }

    [Fact]
    public void SetNull_Forgets()
    {
        var state = new InMemorySessionState();
        state.Set("scope", "key", "font");

        state.Set("scope", "key", null);

        Assert.Null(state.Get("scope", "key"));
    }
}
