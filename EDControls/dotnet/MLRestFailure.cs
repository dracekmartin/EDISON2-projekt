using System.Text.RegularExpressions;

namespace EDControls;

/// <summary>
/// Pozná z textu chyby legacy MLRestu výpadek spojení a konec session ze serveru. Text skládá
/// <c>MLRest.MakeRequest</c> v repu EDISON, formát je kontrakt mezi repy a hlídají ho testy švu.
/// </summary>
public static class MLRestFailure
{
    private const string StatusMarker = "[WebException.Status]";
    private const string ResponseMarker = "[WebException.Response";
    private const string HttpStatusMarker = "[WebException.Response.HttpStatusCode]";
    private const string ServiceUnavailableMarker = "ServiceUnavailable";
    private const string IisPageMarker = "[IIS.HtmlErrorPage]";
    private const string LockLostMarker = "ACTIVITY: Lock exist failed!";
    private const string VersionMismatchMarker = "EdisonVersionFailed!";
    private const int MaxDepth = 10;

    private static readonly Regex EndpointPattern = new(@"\(([A-Za-z0-9.\-]+:\d+)\)", RegexOptions.Compiled);

    /// <summary>
    /// Spojení se serverem selhalo (DNS, odmítnuté nebo přerušené spojení, timeout), nebo server nedostupný
    /// za IIS (503, chybová stránka IIS). Odpověď Caché s chybou výpadek spojení není.
    /// </summary>
    public static bool IsTransport(Exception? ex) => Messages(ex, 0).Any(IsTransportText);

    // Legacy MLRest bere 503 jako výpadek spojení (EdisonConnectionFailed), stránku IIS v telemetrii zahazuje.
    private static bool IsTransportText(string m) =>
        m.Contains(StatusMarker, StringComparison.Ordinal)
        && (!m.Contains(ResponseMarker, StringComparison.Ordinal)
            || m.Contains(IisPageMarker, StringComparison.Ordinal)
            || (m.Contains(HttpStatusMarker, StringComparison.Ordinal) && m.Contains(ServiceUnavailableMarker, StringComparison.Ordinal)));

    /// <summary>Server, se kterým spojení selhalo, z textu chyby .NET „(host:port)", jinak null.</summary>
    internal static string? FailedEndpoint(Exception? ex) =>
        Messages(ex, 0).Select(m => EndpointPattern.Match(m)).FirstOrDefault(match => match.Success)?.Groups[1].Value;

    /// <summary>Server session ukončil: zámek ACTIVITY zmizel, nebo nesedí verze programu a Caché.</summary>
    internal static bool IsSessionEnd(Exception? ex) =>
        Messages(ex, 0).Any(m => m.Contains(LockLostMarker, StringComparison.Ordinal)
                                 || m.Contains(VersionMismatchMarker, StringComparison.Ordinal));

    private static IEnumerable<string> Messages(Exception? ex, int depth)
    {
        if (ex is null || depth > MaxDepth) yield break;
        yield return ex.Message ?? "";

        // Legacy volání obalují text MLRestu do vlastní výjimky, neodchycené tasky do AggregateException.
        IEnumerable<Exception?> inners = ex is AggregateException aggregate
            ? aggregate.InnerExceptions
            : new[] { ex.InnerException };
        foreach (var inner in inners)
            foreach (var message in Messages(inner, depth + 1))
                yield return message;
    }
}
