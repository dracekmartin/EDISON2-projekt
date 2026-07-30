using ALWA.SYS;

namespace EDControls;

/// <summary>
/// <c>QueryOrThrow</c> pro legacy kód, který drží konkrétní <see cref="MLRest"/> místo
/// <see cref="IMLRest"/>. Deleguje na <see cref="MLRestAdapter"/> - jedno místo pravdy
/// pro překlad chybového stavu na výjimku.
/// </summary>
public static class MLRestExtensions
{
    public static object[] QueryOrThrow(this MLRest ml, string cacheClass, string method, object[] input) =>
        new MLRestAdapter(ml).QueryOrThrow(cacheClass, method, input);

    public static CVar QueryOrThrow(this MLRest ml, string cacheClass, string method, CVar input) =>
        new MLRestAdapter(ml).QueryOrThrow(cacheClass, method, input);
}
