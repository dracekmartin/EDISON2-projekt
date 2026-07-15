using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ALWA.SYS;
using TemplateDesigner.Models;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Services;

/// <summary>
/// Klient pro Caché class ALVA.TemplateDesigner.API.Sablony.
/// Async wrapper nad MLRest sync voláními — MLRest.Query je sync (HTTP),
/// Task.Run uvolní UI thread.
/// </summary>
public class CacheTemplateService
{
    public const string CacheClass = "ALVA.TemplateDesigner.API.Sablony";

    /// <summary>Speciální AgendaCode pro šablony bez vazby na konkrétní agendu.</summary>
    public const string GeneralAgendaCode = "GENERAL";

    /// <summary>IdFa systémové (M-line) zásuvky — sdílené read-only šablony, cíl seedu.</summary>
    public const string MlineIdFa = "MLINE";

    private readonly MLRest _mlRest;

    public CacheTemplateService(MLRest mlRest) => _mlRest = mlRest;

    // ─── ListTemplates ────────────────────────────────────────────────────────

    /// <summary>
    /// Vrátí seznam metadat všech šablon (volitelně filtrované podle agendaCode).
    /// </summary>
    public Task<IReadOnlyList<CacheTemplateMetadata>> ListAsync(string? agendaCode = null)
        => Task.Run(() => ListSync(agendaCode));

    private IReadOnlyList<CacheTemplateMetadata> ListSync(string? agendaCode)
    {
        var inVar = new CVar();
        inVar["agendaCode"] = new CVar(agendaCode ?? "");

        CVar result = Query("ListTemplates", inVar);

        string? json = result["1"]?.Value?.ToString();
        if (string.IsNullOrEmpty(json)) return [];

        try
        {
            var arr = JsonNode.Parse(json) as JsonArray ?? [];
            var list = new List<CacheTemplateMetadata>(arr.Count);
            foreach (var item in arr)
            {
                if (item is null) continue;
                var meta = ParseMetadata(item);
                if (meta is not null) list.Add(meta);
            }
            return list;
        }
        catch (JsonException ex)
        {
            throw new CacheTemplateException("Neplatný formát JSON ze seznamu šablon.", ex);
        }
    }

    // ─── LoadTemplate ─────────────────────────────────────────────────────────

    /// <summary>
    /// Načte šablonu (metadata + obsah) podle ID.
    /// </summary>
    public Task<(CacheTemplateMetadata Metadata, Template Template)> LoadAsync(Guid templateId, string? sourceIdFa = null)
        => Task.Run(() => LoadSync(templateId, sourceIdFa));

    private (CacheTemplateMetadata, Template) LoadSync(Guid templateId, string? sourceIdFa)
    {
        var inVar = new CVar();
        inVar["templateId"] = new CVar(templateId.ToString());
        // Prázdné = session firma; "MLINE" pro otevření systémové šablony.
        inVar["sourceIdFa"] = new CVar(sourceIdFa ?? "");

        CVar result = Query("LoadTemplate", inVar);

        string? metaJson = result["metadata"]?.Value?.ToString();
        string? dataJson = result["data"]?.Value?.ToString();
        if (string.IsNullOrEmpty(metaJson) || string.IsNullOrEmpty(dataJson))
            throw new CacheTemplateException("Šablona nebyla nalezena nebo je porušená.");

        var metaNode = JsonNode.Parse(metaJson);
        var meta = ParseMetadata(metaNode)
                   ?? throw new CacheTemplateException("Metadata šablony nelze přečíst.");

        Template template;
        try
        {
            template = TemplateSerializer.Deserialize(dataJson);
        }
        catch (TemplateDeserializationException ex)
        {
            throw new CacheTemplateException(
                $"Obsah šablony '{meta.Name}' nelze načíst: {ex.Message}", ex);
        }
        return (meta, template);
    }

    // ─── SaveTemplate ─────────────────────────────────────────────────────────

    /// <summary>
    /// Uloží šablonu (insert nebo update podle Template.Id).
    /// Před uloženim aktualizuje Template.ModifiedAt na UTC now.
    /// </summary>
    public Task SaveAsync(Template template, string? targetIdFa = null, bool distribution = false)
        => Task.Run(() => SaveSync(template, targetIdFa, distribution));

    private void SaveSync(Template template, string? targetIdFa, bool distribution)
    {
        if (template.Id == Guid.Empty)
            template.Id = Guid.NewGuid();

        var agendaCode = string.IsNullOrWhiteSpace(template.AgendaCode)
            ? GeneralAgendaCode
            : template.AgendaCode!;

        // Serialize updates ModifiedAt — kanonická JSON reprezentace stejná jako file save
        var dataJson = TemplateSerializer.Serialize(template);

        var inVar = new CVar();
        inVar["templateId"] = new CVar(template.Id.ToString());
        inVar["name"]       = new CVar(template.Name);
        inVar["agendaCode"] = new CVar(agendaCode);
        inVar["version"]    = new CVar(template.Version);
        inVar["data"]       = new CVar(dataJson);
        // Prázdné = session firma; "MLINE" pro seed systémové šablony.
        inVar["targetIdFa"] = new CVar(targetIdFa ?? "");
        // Distribuční zápis (seed) — jen tak server povolí zápis do read-only MLINE větve.
        inVar["distribution"] = new CVar(distribution ? "1" : "");

        Query("SaveTemplate", inVar);
    }

    // ─── DeleteTemplate ───────────────────────────────────────────────────────

    /// <summary>Smaže šablonu. Vrátí false pokud šablona neexistovala.</summary>
    public Task<bool> DeleteAsync(Guid templateId)
        => Task.Run(() => DeleteSync(templateId));

    private bool DeleteSync(Guid templateId)
    {
        var inVar = new CVar();
        inVar["templateId"] = new CVar(templateId.ToString());

        CVar result = Query("DeleteTemplate", inVar);

        return result["deleted"]?.Value?.ToString() == "1";
    }

    // ─── RenameTemplate ───────────────────────────────────────────────────────

    /// <summary>Přejmenuje šablonu (jen pole Name v metadatech).</summary>
    public Task RenameAsync(Guid templateId, string newName)
        => Task.Run(() => RenameSync(templateId, newName));

    private void RenameSync(Guid templateId, string newName)
    {
        var inVar = new CVar();
        inVar["templateId"] = new CVar(templateId.ToString());
        inVar["newName"]    = new CVar(newName);

        Query("RenameTemplate", inVar);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>MLRest.Query + ThrowOnError v jednom. Syrovou síťovou výjimku (výpadek VPN/serveru,
    /// socket timeout) zabalí do <see cref="CacheTemplateException"/>, aby volající dostali jednotnou
    /// doménovou hlášku „Operace v databázi selhala" místo generického „neočekávaná chyba" z globálního
    /// handleru. RestStatus.Error řeší <see cref="ThrowOnError"/> (stejný typ výjimky).</summary>
    private CVar Query(string operation, CVar inVar)
    {
        CVar result;
        try
        {
            result = _mlRest.Query(CacheClass, operation, inVar);
        }
        catch (CacheTemplateException) { throw; }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CacheTemplateService] {operation} výjimka: {ex.Message}");
            throw new CacheTemplateException($"Operace „{operation}\" v databázi selhala: {ex.Message}", ex);
        }
        ThrowOnError(operation);
        return result;
    }

    private void ThrowOnError(string operation)
    {
        if (_mlRest.RestStatus.State != MLRStatus.EState.Error) return;
        var msg = _mlRest.RestStatus.Message;
        Debug.WriteLine($"[CacheTemplateService] {operation} error: {msg}");
        throw new CacheTemplateException($"Operace „{operation}\" v databázi selhala: {msg}");
    }

    private static CacheTemplateMetadata? ParseMetadata(JsonNode? node)
    {
        if (node is null) return null;
        var idStr = node["templateId"]?.GetValue<string>();
        if (!Guid.TryParse(idStr, out var id)) return null;

        return new CacheTemplateMetadata
        {
            TemplateId = id,
            Name       = node["name"]?.GetValue<string>() ?? "",
            AgendaCode = node["agendaCode"]?.GetValue<string>() ?? "",
            Author     = NullIfEmpty(node["author"]?.GetValue<string>()),
            CreatedAt  = ParseIsoDate(node["createdAt"]?.GetValue<string>()),
            ModifiedAt = ParseIsoDate(node["modifiedAt"]?.GetValue<string>()),
            ModifiedBy = NullIfEmpty(node["modifiedBy"]?.GetValue<string>()),
            Version    = NullIfEmpty(node["version"]?.GetValue<string>()) ?? "1.0",
        };
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>Parsuje "YYYY-MM-DD HH:MM:SS" (Caché $zdt format 3) i ISO 8601.</summary>
    private static DateTime? ParseIsoDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal, out var dt))
            return dt;
        return null;
    }
}

/// <summary>Metadata jedné šablony uložené v Caché (vrácené z ListAsync / LoadAsync).</summary>
public class CacheTemplateMetadata
{
    public Guid     TemplateId { get; set; }
    public string   Name       { get; set; } = "";
    public string   AgendaCode { get; set; } = "";
    public string?  Author     { get; set; }
    public DateTime? CreatedAt  { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string?  ModifiedBy { get; set; }
    public string   Version    { get; set; } = "1.0";
}

/// <summary>Výjimka pro chyby při komunikaci s Caché Templates API.</summary>
public class CacheTemplateException : Exception
{
    public CacheTemplateException(string message) : base(message) { }
    public CacheTemplateException(string message, Exception inner) : base(message, inner) { }
}
