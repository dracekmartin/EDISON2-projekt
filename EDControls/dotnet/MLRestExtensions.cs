using ALWA.SYS;

namespace EDControls;

public static class MLRestExtensions
{
    public static object[] QueryOrThrow(this MLRest ml, string cacheClass, string method, object[] input)
    {
        var result = ml.Query(cacheClass, method, input);
        ml.RestStatus.ThrowIfError();
        return result;
    }

    public static CVar QueryOrThrow(this MLRest ml, string cacheClass, string method, CVar input)
    {
        var result = ml.Query(cacheClass, method, input);
        ml.RestStatus.ThrowIfError();
        return result;
    }

}
