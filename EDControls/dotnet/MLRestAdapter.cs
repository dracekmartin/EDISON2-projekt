using ALWA.SYS;

namespace EDControls;

/// <summary>
/// Wraps a concrete <see cref="MLRest"/> instance behind <see cref="IMLRest"/>
/// so that ViewModels and controls can be tested with a substitute.
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
}
