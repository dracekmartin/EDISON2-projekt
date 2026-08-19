using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using TemplateDesigner.Config;

namespace TemplateDesigner.Services;

/// <summary>
/// Čtecí přístup k úložišti systémových šablon na helpdesku (Podpora). Data tečou **jedním směrem**:
/// úložiště → klient. Nahrávání a mazání sem nepatří - to smíme jen my ve vývoji, ne build, který
/// jde k zákazníkovi (rozhodnutí Jan 19. 8.).
///
/// <para>Selhání je normální stav, ne chyba: bez sítě, bez klíče nebo při výpadku helpdesku se
/// vrátí prázdno a aplikace pokračuje s tím, co má v databázi. Kanál nikdy nesmí zdržet start
/// ani otevření přehledu šablon.</para>
/// </summary>
public class TemplateChannelClient(HttpClient? http = null)
{
    private const string ListPath = "/api/external/print-templates";
    private const string ApiKeyHeader = "X-API-Key";

    // Sdílený klient (socket exhaustion) - stejný vzor jako u AI služeb.
    private static readonly HttpClient Shared = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly HttpClient _http = http ?? Shared;

    private static TemplateChannelSettings? Settings => AppSettings.Current?.TemplateChannel;

    /// <summary>Je kanál nakonfigurovaný? Bez klíče nebo vypnutý se mlčky přeskakuje.</summary>
    public static bool IsConfigured =>
        Settings is { Enabled: true } s
        && !string.IsNullOrWhiteSpace(s.BaseUrl)
        && !string.IsNullOrWhiteSpace(s.ApiKey);

    /// <summary>Seznam šablon v úložišti (bez obsahu). Prázdno = kanál nedostupný nebo nemá co nabídnout.</summary>
    public async Task<IReadOnlyList<ChannelTemplateInfo>> ListAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return [];
        try
        {
            using var response = await SendAsync(ListPath, ct);
            if (response is null) return [];
            var envelope = JsonSerializer.Deserialize<Envelope<List<ChannelTemplateInfo>>>(response, JsonOptions);
            return envelope?.Data ?? [];
        }
        catch (Exception) { return []; }
    }

    /// <summary>Obsah jedné šablony (JSON řetězec), nebo null když ji nejde stáhnout.</summary>
    public async Task<string?> DownloadAsync(string id, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(id)) return null;
        try
        {
            using var response = await SendAsync($"{ListPath}/{Uri.EscapeDataString(id)}", ct);
            if (response is null) return null;
            var envelope = JsonSerializer.Deserialize<Envelope<ChannelTemplateDetail>>(response, JsonOptions);
            return envelope?.Data?.Content;
        }
        catch (Exception) { return null; }
    }

    private async Task<Stream?> SendAsync(string path, CancellationToken ct)
    {
        var s = Settings;
        if (s is null) return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, s.BaseUrl.TrimEnd('/') + path);
        request.Headers.Add(ApiKeyHeader, s.ApiKey);

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;   // 401/429/5xx = tiše nic, ne výjimka
        return await response.Content.ReadAsStreamAsync(ct);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private class Envelope<T>
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("data")]    public T? Data      { get; set; }
    }
}

/// <summary>Položka seznamu z úložiště - popisné údaje bez obsahu šablony.</summary>
public class ChannelTemplateInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Agenda { get; set; }
    public string? Varianta { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Jedna šablona i s obsahem. Obsah je JSON šablony jako řetězec, bajt v bajt.</summary>
public class ChannelTemplateDetail : ChannelTemplateInfo
{
    public string? Content { get; set; }
}
