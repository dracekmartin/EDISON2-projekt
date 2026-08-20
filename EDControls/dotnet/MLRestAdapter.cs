using ALWA.SYS;

namespace EDControls;

/// <summary>
/// Obaluje konkrétní <see cref="MLRest"/> pod rozhraní <see cref="IMLRest"/> aby šly
/// VM a controls testovat s mock substitucí.
/// </summary>
public class MLRestAdapter(MLRest inner) : IMLRest
{
    public MLRest Inner { get; } = inner;

    public CVar QueryOrThrow(string cacheClass, string method, CVar input) =>
        Checked(Inner.Query(cacheClass, method, input));

    public object[] QueryOrThrow(string cacheClass, string method, object[] input) =>
        Checked(Inner.Query(cacheClass, method, input));

    // Jediné místo, kde se stav requestu překlápí na výjimku - MLRestExtensions sem deleguje.
    private T Checked<T>(T result)
    {
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

    public int CacheLocks
    {
        get => Inner.CacheLocks;
        set => Inner.CacheLocks = value;
    }

    public string Server    => Inner.Server;
    public int    Port      => Inner.Port;
    public string LoginName => Inner.LoginName;
    public string FullName  => Inner.FullName;
    public string Mlp       => Inner.Mlp;
    public string Guid      => Inner.GUID;
    public char   Delimiter => Inner.Delimiter;

    public IMLRest With(int? cacheLocks = null, string? idfa = null, string? dataNamespace = null)
    {
        // Věrný klon identity přes konstruktor (ClientIP + SysNamespace se dopočítají) + zkopírování
        // ostatních polí; přepíšeme jen požadované. Žádná mutace Inner → bez race se sdílenou instancí.
        var clone = new MLRest(Inner.Server, Inner.Port, dataNamespace ?? Inner.DataNamespace, idfa ?? Inner.Idfa)
        {
            ClassNamespace = Inner.ClassNamespace,
            UserID = Inner.UserID,
            Passwd = Inner.Passwd,
            Mlp = Inner.Mlp,
            Delimiter = Inner.Delimiter,
            LoginName = Inner.LoginName,
            FullName = Inner.FullName,
            ApplicationFullName = Inner.ApplicationFullName,
            Locale = Inner.Locale,
            EdisonVersion = Inner.EdisonVersion,
            GUID = Inner.GUID,
            CacheLocks = cacheLocks ?? Inner.CacheLocks,
            // Secured-mode web credentials - legacy je při klonech kopíruje taky (EETMonitor, frmNoMDI)
            CacheWebLoginAndPassword = Inner.CacheWebLoginAndPassword,
        };
        return new MLRestAdapter(clone);
    }
}
