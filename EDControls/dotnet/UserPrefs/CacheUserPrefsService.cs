using System.Diagnostics;
using System.Text.Json.Nodes;
using ALWA.SYS;

namespace EDControls.UserPrefs;

/// <summary>
/// Caché-backed implementace <see cref="IUserPrefsService"/> — volá ALVA.EDControls.UserData
/// přes MLRest. IdFa + userID si Caché class bere automaticky z $$$FillParams (sys kontextu MLRest).
///
/// MLRest.QueryOrThrow je sync (HTTP); wrap v Task.Run uvolní UI thread.
/// Při chybě Load vrací null, Save / Delete tiše loguje (UI by nemělo padat kvůli persistence).
/// </summary>
public class CacheUserPrefsService : IUserPrefsService
{
    public const string CacheClass = "ALVA.EDControls.UserData";

    private readonly IMLRest _mlRest;

    public CacheUserPrefsService(IMLRest mlRest) => _mlRest = mlRest;

    public Task<JsonNode?> LoadAsync(string scope, string key)
        => Task.Run(() => LoadSync(scope, key));

    private JsonNode? LoadSync(string scope, string key)
    {
        ValidateArgs(scope, key);
        try
        {
            var inVar = new CVar();
            inVar["scope"] = new CVar(scope);
            inVar["key"]   = new CVar(key);

            CVar result = _mlRest.QueryOrThrow(CacheClass, "Load", inVar);
            string? json = result["data"]?.Value?.ToString();
            return string.IsNullOrEmpty(json) ? null : JsonNode.Parse(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CacheUserPrefs] Load exception ({scope}/{key}): {ex.Message}");
            return null;
        }
    }

    public Task SaveAsync(string scope, string key, JsonNode data)
        => Task.Run(() => SaveSync(scope, key, data));

    private void SaveSync(string scope, string key, JsonNode data)
    {
        ValidateArgs(scope, key);
        try
        {
            var inVar = new CVar();
            inVar["scope"] = new CVar(scope);
            inVar["key"]   = new CVar(key);
            inVar["data"]  = new CVar(data.ToJsonString());

            _mlRest.QueryOrThrow(CacheClass, "Save", inVar);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CacheUserPrefs] Save exception ({scope}/{key}): {ex.Message}");
        }
    }

    public Task DeleteAsync(string scope, string key)
        => Task.Run(() => DeleteSync(scope, key));

    private void DeleteSync(string scope, string key)
    {
        ValidateArgs(scope, key);
        try
        {
            var inVar = new CVar();
            inVar["scope"] = new CVar(scope);
            inVar["key"]   = new CVar(key);

            _mlRest.QueryOrThrow(CacheClass, "Delete", inVar);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CacheUserPrefs] Delete exception ({scope}/{key}): {ex.Message}");
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
