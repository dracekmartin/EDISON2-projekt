using System.Text.Json;
using System.Threading.Tasks;
using ALWA.SYS;

namespace EDControls;

/// <summary>
/// Helper pro volání Caché metod s JSON envelope wire formátem:
///   IN  → CVar["payload"] = serializovaný request JSON
///   OUT → CVar["payload"] obsahuje response JSON
/// Žádná změna v MLRest.dll - postaveno nad existujícím <see cref="IMLRest.QueryOrThrow(string, string, CVar)"/>.
/// </summary>
public static class MLRestJson
{
    /// <summary>
    /// Zavolá Caché metodu s JSON request/response payloadem a vrátí silně typovaný výsledek.
    /// </summary>
    /// <param name="mlRest">MLRest abstrakce (typicky <c>EDViewModel.MLRest</c>).</param>
    /// <param name="cacheClass">FQN Caché třídy (např. <c>"ALVA.TemplateDesigner.API.InstancesList"</c>).</param>
    /// <param name="method">Název metody (např. <c>"GetForm"</c>).</param>
    /// <param name="request">Request objekt - bude serializován do JSON.</param>
    public static async Task<TResponse> CallAsync<TRequest, TResponse>(
        IMLRest mlRest, string cacheClass, string method, TRequest request)
        where TRequest : class
        where TResponse : class
    {
        var input = new CVar();
        input["payload"] = new CVar(JsonSerializer.Serialize(request));

        CVar output = await Task.Run(() => mlRest.QueryOrThrow(cacheClass, method, input));

        var json = output["payload"]?.Value?.ToString() ?? "{}";
        return JsonSerializer.Deserialize<TResponse>(json)
            ?? throw new System.InvalidOperationException(
                $"MLRestJson: prázdný response z {cacheClass}.{method}");
    }
}
