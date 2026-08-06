using System.Net.Http;
using System.Text;

namespace EDControls.ErrorReporting;

/// <summary>
/// Odeslání hlášení na <c>podpora.m-line.cz/api/telemetry/ingest</c>. Best-effort:
/// nikdy nehází a nikdy neblokuje UI vlákno - selhání hlášení nesmí být druhá chyba
/// nad tou první. Uživateli zbývá tlačítko „Uložit do souboru" v chybovém dialogu.
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

    private const int TimeoutSeconds = 15;

    /// <summary>Ingest odmítne dávku nad 100 záznamů (<c>MAX_BATCH_SIZE</c>).</summary>
    internal const int MaxBatchSize = 100;

    private static readonly Lazy<HttpClient> DefaultClient = new(() =>
        new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) });

    /// <summary>Přebití HTTP klienta pro testy (fake handler).</summary>
    internal static HttpClient? ClientOverride { get; set; }

    private static HttpClient Client => ClientOverride ?? DefaultClient.Value;

    /// <summary>Odešle hlášení na pozadí. Vrací se okamžitě, výsledek nikoho nezajímá.</summary>
    public static void Send(ErrorReport report)
    {
        if (report is null) return;
        _ = Task.Run(() => SendAsync([report]));
    }

    /// <summary>Odešle dávku na pozadí. Delší než limit se rozdělí na víc requestů.</summary>
    public static void Send(IReadOnlyList<ErrorReport> reports)
    {
        if (reports is null || reports.Count == 0) return;
        _ = Task.Run(() => SendAsync(reports));
    }

    /// <summary>
    /// Vlastní odeslání. <c>internal</c> kvůli testům - produkční kód jde přes
    /// <see cref="Send(ErrorReport)"/>, aby nikdo omylem nečekal na síť na UI vlákně.
    /// </summary>
    internal static async Task<bool> SendAsync(IReadOnlyList<ErrorReport> reports)
    {
        var allOk = true;

        for (var offset = 0; offset < reports.Count; offset += MaxBatchSize)
        {
            var chunk = reports.Skip(offset).Take(MaxBatchSize).ToList();
            try
            {
                // Jeden záznam se posílá jako objekt, víc jako pole - obojí ingest umí.
                var payload = chunk.Count == 1
                    ? chunk[0].ToJson().ToJsonString()
                    : new System.Text.Json.Nodes.JsonArray(
                        [.. chunk.Select(r => (System.Text.Json.Nodes.JsonNode)r.ToJson())]).ToJsonString();

                using var request = new HttpRequestMessage(HttpMethod.Post, IngestUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                };
                request.Headers.Add("X-API-Key", ApiKey);

                using var response = await Client.SendAsync(request).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) allOk = false;
            }
            catch
            {
                // Best-effort - výpadek sítě, VPN ani 500 nesmí nic shodit.
                allOk = false;
            }
        }

        return allOk;
    }
}
