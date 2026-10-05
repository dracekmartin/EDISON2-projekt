namespace EDControls.ErrorReporting;

/// <summary>
/// Odeslání hlášení na <c>podpora.m-line.cz/api/telemetry/ingest</c>. Best-effort:
/// nikdy nehází a nikdy neblokuje UI vlákno - selhání hlášení nesmí být druhá chyba
/// nad tou první. Neodeslané hlášení zachytí <see cref="ErrorFallbackLog"/>; uživateli
/// navíc zbývá tlačítko „Uložit do souboru" v chybovém dialogu. Odesílá výchozí
/// <see cref="ErrorReportingPipeline"/>.
/// </summary>
public static class ErrorReporter
{
    // Záměrně static readonly, ne const - stejný důvod jako u SupportActions.WidgetKey:
    // const se inlinuje do konzumenta, takže samostatně nasazená EDControls.dll by hodnotu
    // nezměnila exe, které se nepřebuildilo.

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

    /// <summary>Odešle hlášení na pozadí. Vrací se okamžitě, výsledek nikoho nezajímá.</summary>
    public static void Send(ErrorReport report) => ErrorReports.Pipeline.Send(report);

    /// <summary>Odešle dávku na pozadí. Delší než limit se rozdělí na víc requestů.</summary>
    public static void Send(IReadOnlyList<ErrorReport> reports) => ErrorReports.Pipeline.Send(reports);
}
