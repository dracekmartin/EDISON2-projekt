using System.Text.Json.Nodes;

namespace TemplateDesigner.Services;

/// <summary>
/// Posílání šablon jiným firmám přes sdílení dat ISYCloudu (`B189`, R9). Spojení s ISYCloudem drží server
/// (`API.SdileniSablon`), návrhář jen vybere šablony a firmy - klíč firmy nesmí na počítač uživatele.
/// </summary>
public interface ITemplateSharing
{
    /// <summary>Jestli firma může posílat; bez povoleného sdílení nese důvod pro uživatele.</summary>
    Task<SharingState> GetSharingStateAsync();

    /// <summary>Firmy, kterým jde šablony poslat, bez vlastní.</summary>
    Task<IReadOnlyList<SharingRecipient>> GetRecipientsAsync();

    /// <summary>Pošle vlastní šablony firmy vybraným firmám; server je rozdělí do zásilek po 20.</summary>
    Task<SharingSendResult> SendTemplatesAsync(IReadOnlyCollection<Guid> templateIds,
        IReadOnlyCollection<string> recipientIdFas, string message);
}

/// <summary>Stav sdílení firmy (<c>GetStav</c>); <paramref name="Reason"/> je text pro uživatele, když sdílet nejde.</summary>
public sealed record SharingState(bool Active, string FirmName, string Reason)
{
    public static SharingState Parse(string? json)
    {
        var root = TryParse(json);
        if (root is null) return new SharingState(false, "", "Stav sdílení dat se nepodařilo zjistit.");
        return new SharingState(root["aktivni"]?.ToString() == "true", Text(root, "nazev"), Text(root, "duvod"));
    }

    internal static JsonNode? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonNode.Parse(json); }
        catch (System.Text.Json.JsonException) { return null; }
    }

    internal static string Text(JsonNode node, string key) => node[key]?.ToString() ?? "";
}

/// <summary>Firma, které jde šablonu poslat (<c>GET /prijemci</c> ISYCloudu). <paramref name="Customer"/> = zákazník,
/// kterému firma patří; firmy jednoho zákazníka se podle něj poznají.</summary>
public sealed record SharingRecipient(string IdFa, string Name, string Customer)
{
    public static IReadOnlyList<SharingRecipient> ParseList(string? json)
    {
        if (SharingState.TryParse(json) is not JsonArray arr) return [];
        return [.. arr.OfType<JsonNode>()
            .Select(n => new SharingRecipient(SharingState.Text(n, "idfa"), SharingState.Text(n, "nazev"), SharingState.Text(n, "zakaznik")))
            .Where(r => r.IdFa != "")
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)];
    }
}

public sealed record SharingSendResult(int Templates, int Shipments);
