using ALWA.SYS;

namespace EDControls;

/// <summary>
/// Obaluje konkrétní <see cref="MLRest"/> pod rozhraní <see cref="IMLRest"/> aby šly
/// VM a controls testovat s mock substitucí.
/// </summary>
public class MLRestAdapter(MLRest inner) : IMLRest
{
    public MLRest Inner { get; } = inner;

    public CVar QueryOrThrow(string cacheClass, string method, CVar input)
    {
        var result = Inner.Query(cacheClass, method, input);
        Inner.RestStatus.ThrowIfError();
        return result;
    }

    public object[] QueryOrThrow(string cacheClass, string method, object[] input)
    {
        var result = Inner.Query(cacheClass, method, input);
        Inner.RestStatus.ThrowIfError();
        return result;
    }

    public string DataNamespace
    {
        get => Inner.DataNamespace;
        set => Inner.DataNamespace = value;
    }

    public string Idfa
    {
        get => Inner.Idfa;
        set => Inner.Idfa = value;
    }

    public string Server    => Inner.Server;
    public int    Port      => Inner.Port;
    public string LoginName => Inner.LoginName;
    public string FullName  => Inner.FullName;
    public string Mlp       => Inner.Mlp;
    public string Guid      => Inner.GUID;
    public char   Delimiter => Inner.Delimiter;

    public int CacheLocks
    {
        get => Inner.CacheLocks;
        set => Inner.CacheLocks = value;
    }
}
