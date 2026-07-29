using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ALWA.SYS;
using EDControls.EDViewModel;
using TemplateDesigner.Config;

namespace TemplateDesigner.Services;

/// <summary>Chyba EDISONAI/mlweb integrace - Message je uživatelsky čitelná česká hláška.</summary>
public class EdisonAiException(string message) : Exception(message);

/// <summary>
/// Napojení AI funkcí na mlweb (podpora.m-line.cz) - stejný kontrakt jako EDISONAI v EDISONu:
/// M-line spravuje zákazníkům API klíč a měsíční tokenové limity, aplikace si klíč vyzvedne
/// a spotřebu hlásí zpět.
///
/// Pořadí získání klíče (<see cref="GetApiKeyAsync"/>):
///  1. Lokální AiAssistant.AnthropicApiKey v appsettings → vývojářský bypass
///     (mlweb se nevolá, spotřeba se nehlásí - obdoba DEV režimu EDISONu).
///  2. Caché ALVA.EDISONAI.API.Config.GetMLWebConfig (mlweb X-API-Key, baseUrl, isDev, devApiKey);
///     na DEV serveru se použije devApiKey přímo (spotřeba se nehlásí - DEV sdílí IdFa s produkcí).
///  3. POST {mlweb}/api/mline/agent/resolve {idfa, loginName} → allowed + apiKey.
///     FAIL-CLOSED: bez spojení / bez povolení / vyčerpaný limit AI nefunguje.
///     Výsledek se cachuje 60 s (limit se stejně přepočítá po každém volání přes usage).
///
/// Spotřeba (<see cref="ReportUsage"/>): POST /api/mline/usage {..., source: "templatedesigner"},
/// best-effort na pozadí; odpověď nese aktuální 'allowed' → gate pro další volání bez round-tripu.
///
/// Model zůstává v režii TD (AssistantModel/VisionModel) - resolvovaný 'model' z mlweb je
/// konfigurace EDISONAI chatu; limity jsou tokenové, takže volba modelu limit neobchází.
/// TD podporuje jen provider "claude" - jiný provider = fail-closed s hláškou.
/// </summary>
public static class EdisonAiService
{
    private const string ResolvePath = "/api/mline/agent/resolve";
    private const string UsagePath   = "/api/mline/usage";
    private const string UsageSource = "templatedesigner";
    private const int    ResolveTtlMs = 60_000;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly SemaphoreSlim Gate = new(1, 1);

    // ── mlweb config z Caché (per session) ──
    private static bool    _configLoaded;
    private static string  _mlwebApiKey  = "";
    private static string  _mlwebBaseUrl = "";
    private static bool    _isDev;
    private static string  _devApiKey    = "";

    // ── resolve cache (60 s TTL + allowed gate z usage odpovědí) ──
    private static string?  _cachedApiKey;
    private static DateTime _resolveExpiresUtc = DateTime.MinValue;
    private static bool     _lastKnownAllowed;

    /// <summary>True když poslední GetApiKeyAsync skončil lokálním/DEV klíčem → spotřeba se nehlásí.</summary>
    private static bool _bypassUsageReporting = true;

    /// <summary>
    /// Vrátí Anthropic API klíč pro AI volání, nebo vyhodí <see cref="EdisonAiException"/>
    /// s uživatelskou hláškou (fail-closed). Viz pořadí v class docu.
    /// </summary>
    public static async Task<string> GetApiKeyAsync()
    {
        // 1. Lokální klíč = vývojářský bypass (zachovává dosavadní chování).
        var localKey = AppSettings.Current?.AiAssistant.AnthropicApiKey;
        if (!string.IsNullOrWhiteSpace(localKey))
        {
            _bypassUsageReporting = true;
            return localKey;
        }

        await Gate.WaitAsync();
        try
        {
            // 2. mlweb config z Caché (jednou per session).
            if (!_configLoaded)
                await Task.Run(LoadMLWebConfigSync);

            if (_isDev && !string.IsNullOrEmpty(_devApiKey))
            {
                _bypassUsageReporting = true;
                return _devApiKey;
            }

            if (string.IsNullOrEmpty(_mlwebApiKey))
                throw new EdisonAiException(
                    "AI funkce nejsou nakonfigurovány - chybí lokální API klíč i klíč pro podpora.m-line.cz " +
                    "(Caché ALVA.EDISONAI.Config). Kontaktujte podporu M-line.");

            // 3. Resolve přes mlweb (fail-closed, 60s cache).
            if (_lastKnownAllowed && _cachedApiKey is not null && DateTime.UtcNow < _resolveExpiresUtc)
            {
                _bypassUsageReporting = false;
                return _cachedApiKey;
            }

            var apiKey = await ResolveAsync();
            _cachedApiKey = apiKey;
            _resolveExpiresUtc = DateTime.UtcNow.AddMilliseconds(ResolveTtlMs);
            _lastKnownAllowed = true;
            _bypassUsageReporting = false;
            return apiKey;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Nahlásí spotřebu tokenů do mlweb (statistiky + limity). Best-effort na pozadí -
    /// chyba se nikde nezobrazuje; odpověď aktualizuje 'allowed' gate pro další volání.
    /// V bypass režimu (lokální/DEV klíč) se nehlásí nic.
    /// </summary>
    public static void ReportUsage(int inputTokens, int outputTokens)
    {
        if (_bypassUsageReporting) return;
        if (inputTokens <= 0 && outputTokens <= 0) return;

        var idfa  = EDServices.Current.MLRest?.Idfa ?? "";
        var login = EDServices.Current.MLRest?.LoginName ?? "";
        if (idfa.Length == 0) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var body = new JsonObject
                {
                    ["idfa"] = idfa,
                    ["loginName"] = login,
                    ["inputTokens"] = inputTokens,
                    ["outputTokens"] = outputTokens,
                    ["source"] = UsageSource,
                };
                var resp = await PostAsync(UsagePath, body.ToJsonString());
                using var doc = JsonDocument.Parse(resp);
                if (doc.RootElement.TryGetProperty("success", out var ok) && ok.GetBoolean()
                    && doc.RootElement.TryGetProperty("data", out var data)
                    && data.TryGetProperty("allowed", out var allowed))
                {
                    _lastKnownAllowed = allowed.GetBoolean();
                    if (!_lastKnownAllowed) _resolveExpiresUtc = DateTime.MinValue;
                }
            }
            catch
            {
                // Best-effort - výpadek statistik neblokuje AI funkce.
            }
        });
    }

    /// <summary>Vynutí čerstvý config i resolve (po přepnutí firmy / pro testy).</summary>
    public static void InvalidateCache()
    {
        _configLoaded = false;
        _cachedApiKey = null;
        _resolveExpiresUtc = DateTime.MinValue;
        _lastKnownAllowed = false;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void LoadMLWebConfigSync()
    {
        var mlRest = EDServices.Current.MLRest
            ?? throw new EdisonAiException(
                "AI funkce nejsou dostupné - aplikace není připojena k databázi a chybí lokální API klíč.");

        CVar output;
        try
        {
            output = mlRest.QueryOrThrow("ALVA.EDISONAI.API.Config", "GetMLWebConfig", new CVar());
        }
        catch (Exception ex) when (ex is not EdisonAiException)
        {
            throw new EdisonAiException(
                "AI funkce nejsou dostupné - nepodařilo se načíst konfiguraci AI ze serveru " +
                "(EDISONAI není na serveru nainstalováno?). " + ex.Message);
        }

        _mlwebApiKey  = output["mlwebApiKey"]?.Value?.ToString() ?? "";
        _mlwebBaseUrl = output["mlwebBaseUrl"]?.Value?.ToString() ?? "";
        _isDev        = output["isDev"]?.Value?.ToString() == "1";
        _devApiKey    = output["devApiKey"]?.Value?.ToString() ?? "";
        _configLoaded = true;
    }

    private static async Task<string> ResolveAsync()
    {
        var idfa  = EDServices.Current.MLRest?.Idfa ?? "";
        var login = EDServices.Current.MLRest?.LoginName ?? "";
        if (idfa.Length == 0)
            throw new EdisonAiException("Nepodařilo se zjistit identifikátor firmy (IdFa). AI funkce nelze použít.");

        string responseBody;
        try
        {
            var body = new JsonObject { ["idfa"] = idfa, ["loginName"] = login };
            responseBody = await PostAsync(ResolvePath, body.ToJsonString());
        }
        catch (Exception ex) when (ex is not EdisonAiException)
        {
            _lastKnownAllowed = false;
            throw new EdisonAiException(
                "AI funkce nejsou dostupné - nepodařilo se spojit s podpora.m-line.cz. " + ex.Message);
        }

        JsonElement data;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("success", out var ok) || !ok.GetBoolean()
                || !doc.RootElement.TryGetProperty("data", out var d))
                throw new EdisonAiException("AI funkce nejsou dostupné - neplatná odpověď z podpora.m-line.cz.");
            data = d.Clone();
        }
        catch (JsonException)
        {
            throw new EdisonAiException("AI funkce nejsou dostupné - neplatná odpověď z podpora.m-line.cz.");
        }

        if (!(data.TryGetProperty("allowed", out var allowedProp) && allowedProp.GetBoolean()))
        {
            _lastKnownAllowed = false;
            var reason = data.TryGetProperty("reason", out var r) ? r.GetString() : null;
            throw new EdisonAiException(string.IsNullOrEmpty(reason)
                ? "Byl dosažen měsíční limit tokenů pro AI. Kontaktujte podporu M-line."
                : reason);
        }

        var provider = data.TryGetProperty("provider", out var p) ? (p.GetString() ?? "").Trim() : "";
        if (!provider.Equals("claude", StringComparison.OrdinalIgnoreCase))
            throw new EdisonAiException(
                "AI funkce nejsou nakonfigurovány - pro tuto firmu je v mlwebu nastaven nepodporovaný AI provider " +
                $"(\"{provider}\"). Návrhář šablon podporuje pouze Claude. Kontaktujte administrátora.");

        var hasApiKey = data.TryGetProperty("hasApiKey", out var h) && h.GetBoolean();
        var apiKey = data.TryGetProperty("apiKey", out var k) ? k.GetString() : null;
        if (!hasApiKey || string.IsNullOrEmpty(apiKey))
            throw new EdisonAiException(
                "AI funkce nejsou nakonfigurovány - pro tuto firmu není nastaven API klíč. Kontaktujte administrátora.");

        return apiKey;
    }

    private static async Task<string> PostAsync(string path, string jsonBody)
    {
        var baseUrl = (_mlwebBaseUrl.Length > 0 ? _mlwebBaseUrl : "https://podpora.m-line.cz").TrimEnd('/');

        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + path);
        request.Headers.Add("X-API-Key", _mlwebApiKey);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new EdisonAiException($"HTTP {(int)response.StatusCode} z podpora.m-line.cz: {body}");
        return body;
    }
}
