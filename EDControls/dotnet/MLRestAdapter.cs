using System.Reflection;
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
    public string UserID    => Inner.UserID;
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
        // Klon téže session (RestCallRunner) dědí CacheLocks, server mu hlídá zámek i verzi, ale ukončení
        // aplikace visí na událostech sdílené instance. Klony s přepsanými parametry jsou jiný režim nebo
        // jiná firma (poll, logout, update check) a ukončení spouštět nemají.
        if (cacheLocks is null && idfa is null && dataNamespace is null)
        {
            clone.EdisonActivityFailed += (_, e) => ForwardSessionEnd(clone, ActivityFailedField, e);
            clone.EdisonVersionFailed += (_, e) => ForwardSessionEnd(clone, VersionFailedField, e);
        }
        return new MLRestAdapter(clone);
    }

    private int _sessionEndForwarded;

    // Rozhoduje se až při chybě: dávka na klonu přežije přepnutí firmy (sdílená instance má po dobu switche
    // CacheLocks=0, pak jinou firmu) a hlásí zámek, který jsme zrušili sami. Jen jednou - souběžné klony
    // (SessionDataLoader) by jinak vrstvily dialog „aplikace bude ukončena".
    private void ForwardSessionEnd(MLRest clone, FieldInfo? field, EventArgs e)
    {
        if (Inner.Idfa != clone.Idfa || Inner.DataNamespace != clone.DataNamespace || Inner.CacheLocks != clone.CacheLocks)
            return;
        if (Interlocked.Exchange(ref _sessionEndForwarded, 1) == 1) return;
        (field?.GetValue(Inner) as EventHandler)?.Invoke(Inner, e);
    }

    // MLRest (repo EDISON) události zvenku vyvolat nedovolí, odběratele čteme z backing fieldu.
    // Chybějící field (jiná deklarace v nové verzi DLL) = bez přeposílání, ne pád; hlídá MLRestAdapterWithTests.
    private static readonly FieldInfo? ActivityFailedField = EventField(nameof(MLRest.EdisonActivityFailed));
    private static readonly FieldInfo? VersionFailedField = EventField(nameof(MLRest.EdisonVersionFailed));

    private static FieldInfo? EventField(string name) =>
        typeof(MLRest).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
}
