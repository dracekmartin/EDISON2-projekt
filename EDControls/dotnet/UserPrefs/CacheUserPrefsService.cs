using System.Text.Json.Nodes;
using ALWA.SYS;
using EDControls.ErrorReporting;

namespace EDControls.UserPrefs;

/// <summary>
/// Caché-backed implementace <see cref="IUserPrefsService"/> - volá ALVA.EDControls.UserData
/// přes MLRest. IdFa + userID si Caché class bere automaticky z $$$FillParams (sys kontextu MLRest).
///
/// Každý request jde přes <see cref="IRestCallRunner"/>, tedy mimo UI thread a na **klonu**
/// sdílené instance: preference ukládá kdokoli kdykoli (debouncer layoutu, zavření okna, plocha)
/// souběžně s requesty UI threadu, a <c>RestStatus</c> je per-instance (root CLAUDE.md).
/// Load při selhání transportu **hází** - null znamená jen „klíč neexistuje" (bez toho volající
/// nerozliší výpadek od prázdna a přepíše uživatelova data prázdnem). Save / Delete selhání
/// hlásí do telemetrie a polykají (persistence nesmí shodit UI).
/// </summary>
public class CacheUserPrefsService : IUserPrefsService
{
    public const string CacheClass = "ALVA.EDControls.UserData";

    private readonly IRestCallRunner _runner;
    private readonly Action<Exception> _reportFailure;

    public CacheUserPrefsService(IRestCallRunner runner, Action<Exception>? reportFailure = null)
    {
        _runner = runner;
        _reportFailure = reportFailure ?? ErrorReports.ReportBackground;
    }

    /// <summary>Factory se čte per request - po přepnutí session (switch user) služba míří na novou.</summary>
    public CacheUserPrefsService(Func<IMLRest> rest, Action<Exception>? reportFailure = null)
        : this(new RestCallRunner(rest), reportFailure) { }

    public CacheUserPrefsService(IMLRest mlRest) : this(() => mlRest) { }

    public Task<JsonNode?> LoadAsync(string scope, string key)
    {
        ValidateArgs(scope, key);
        return _runner.RunAsync(rest => Load(rest, scope, key));
    }

    private JsonNode? Load(IMLRest rest, string scope, string key)
    {
        var inVar = new CVar();
        inVar["scope"] = new CVar(scope);
        inVar["key"]   = new CVar(key);

        CVar result;
        try
        {
            result = rest.QueryOrThrow(CacheClass, "Load", inVar);
        }
        catch (Exception ex)
        {
            _reportFailure(ex);
            throw;
        }

        string? json = result["data"]?.Value?.ToString();
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonNode.Parse(json);
        }
        catch (Exception ex)
        {
            // Poškozený blob = data nejsou k ničemu; null ho nechá při příštím Save přepsat.
            _reportFailure(ex);
            return null;
        }
    }

    public Task SaveAsync(string scope, string key, JsonNode data)
    {
        ValidateArgs(scope, key);
        return _runner.RunAsync(rest =>
        {
            var inVar = new CVar();
            inVar["scope"] = new CVar(scope);
            inVar["key"]   = new CVar(key);
            inVar["data"]  = new CVar(data.ToJsonString());
            return TryQuery(rest, "Save", inVar);
        });
    }

    public Task DeleteAsync(string scope, string key)
    {
        ValidateArgs(scope, key);
        return _runner.RunAsync(rest =>
        {
            var inVar = new CVar();
            inVar["scope"] = new CVar(scope);
            inVar["key"]   = new CVar(key);
            return TryQuery(rest, "Delete", inVar);
        });
    }

    private bool TryQuery(IMLRest rest, string method, CVar inVar)
    {
        try
        {
            rest.QueryOrThrow(CacheClass, method, inVar);
            return true;
        }
        catch (Exception ex)
        {
            _reportFailure(ex);
            return false;
        }
    }

    private static void ValidateArgs(string scope, string key)
    {
        if (string.IsNullOrWhiteSpace(scope))
            throw new ArgumentException("scope is required", nameof(scope));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("key is required", nameof(key));
    }
}
