namespace EDControls.ErrorReporting;

/// <summary>
/// Adresa, klíč a limit dávky ingest endpointu <c>podpora.m-line.cz/api/telemetry/ingest</c>. Posílá na něj
/// <see cref="ErrorReportingPipeline.Send(ErrorReport)"/> na pozadí, neodeslané dávky zachytí <see cref="ErrorFallbackLog"/>.
/// </summary>
internal static class ErrorReporter
{
    /// <summary>Ingest endpoint telemetrie (přijímá jeden záznam i dávku do 100).</summary>
    public static readonly string IngestUrl = "https://podpora.m-line.cz/api/telemetry/ingest";

    /// <summary>
    /// Klíč telemetrie. Server drží **jediný** klíč (nastavení <c>telemetry_api_key</c>),
    /// nerozlišuje podle zdroje - sdílíme ho tedy s legacy EDISONem. Vlastní klíč pro
    /// EDISON2 by byl zásah do mlwebu, ne do nás.
    /// </summary>
    public static readonly string ApiKey = "87decc4a4655c0b74cd11d0ffb0716331264a3f3afef65cb0752e56cbfb797ec";

    /// <summary>Ingest odmítne dávku nad 100 záznamů (<c>MAX_BATCH_SIZE</c>).</summary>
    internal const int MaxBatchSize = 100;
}
