using System.Collections.Concurrent;
using ALWA.SYS;

namespace EDControls.Tests;

/// <summary>
/// Thread-safe fake sdílené instance pro zápisy z pool threadu (NSubstitute thread-safe není). <c>With()</c> vrací
/// klony. Vyjmenované metody čekají na <see cref="Gate"/> jako server bez spojení, volání se zapíše, až projde.
/// </summary>
internal sealed class GatedMLRest : IMLRest
{
    /// <summary>Pojistka: test, který bránu neotevře, nesmí zablokovat celou sadu.</summary>
    private static readonly TimeSpan GateLimit = TimeSpan.FromSeconds(5);

    private readonly GatedMLRest _root;
    private readonly HashSet<string> _gated;

    public GatedMLRest(params string[] gatedMethods)
    {
        _root = this;
        _gated = [.. gatedMethods];
    }

    private GatedMLRest(GatedMLRest root, string idfa, string dataNamespace, int cacheLocks)
    {
        _root = root;
        _gated = root._gated;
        Idfa = idfa;
        DataNamespace = dataNamespace;
        CacheLocks = cacheLocks;
    }

    public ManualResetEventSlim Gate { get; } = new();

    public ConcurrentQueue<(string Method, bool OnClone, string Idfa, int CacheLocks)> Calls { get; } = new();

    /// <summary>Odpověď <c>LoadParams</c> bez ERR slotu.</summary>
    public string[] SavedState { get; init; } = [];

    public bool Received(string method, bool onClone) =>
        _root.Calls.Any(c => c.Method == method && c.OnClone == onClone);

    public object[] QueryOrThrow(string cacheClass, string method, object[] input)
    {
        Record(method);
        return method == "LoadParams" ? ["", .. _root.SavedState] : [""];
    }

    public CVar QueryOrThrow(string cacheClass, string method, CVar input)
    {
        Record(method);
        return new CVar();
    }

    private void Record(string method)
    {
        if (_gated.Contains(method)) _root.Gate.Wait(GateLimit);
        _root.Calls.Enqueue((method, !ReferenceEquals(this, _root), Idfa, CacheLocks));
    }

    public IMLRest With(int? cacheLocks = null, string? idfa = null, string? dataNamespace = null) =>
        new GatedMLRest(_root, idfa ?? Idfa, dataNamespace ?? DataNamespace, cacheLocks ?? CacheLocks);

    public string DataNamespace { get; set; } = "NS";
    public string Idfa { get; set; } = "IDFA";
    public int CacheLocks { get; set; } = 1;
    public string Server => "srv";
    public int Port => 1;
    public string LoginName => "login";
    public string FullName => "Full Name";
    public string UserID => "user";
    public string Mlp => "1";
    public string Guid => "guid";
    public char Delimiter => '~';
}
