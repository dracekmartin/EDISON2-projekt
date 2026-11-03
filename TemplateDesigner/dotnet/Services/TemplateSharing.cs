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

    // ── Příjem: zásilka se převezme nebo odmítne celá (Jan 3. 11.) ──

    /// <summary>Kolik zásilek šablon od jiných firem firma ještě nevyřídila a od koho je nejnovější.</summary>
    Task<ReceivedSummary> GetReceivedSummaryAsync();

    /// <summary>Šablony zásilky před převzetím: agenda, formát a šablona téhož původu, kterou firma už převzala.</summary>
    Task<IReadOnlyList<ReceivedTemplate>> GetShipmentTemplatesAsync(string shipmentId);

    /// <summary>Převezme celou zásilku jako vlastní šablony firmy. <paramref name="overwriteFileIds"/> = šablony, jejichž
    /// dřív převzatou verzi nová přepíše; ostatní se uloží jako nové.</summary>
    Task<IReadOnlyList<TakenTemplate>> AcceptShipmentAsync(string shipmentId, IReadOnlyCollection<string> overwriteFileIds);

    /// <summary>Odmítne celou zásilku; odesílatel to uvidí v portálu ISYCloudu.</summary>
    Task RejectShipmentAsync(string shipmentId);
}

/// <summary>Nevyřízené zásilky šablon (<c>GetSouhrnPrijatych</c>): počet a nejnovější z nich.</summary>
/// <param name="Shipments">Počet zásilek.</param>
/// <param name="Sender">Firma, která poslala nejnovější zásilku.</param>
/// <param name="Name">Název nejnovější zásilky: název šablony, nebo „3 šablony".</param>
/// <param name="Templates">Šablon v nejnovější zásilce.</param>
public sealed record ReceivedSummary(int Shipments, string Sender, string Name, int Templates)
{
    public static readonly ReceivedSummary None = new(0, "", "", 0);

    public static ReceivedSummary Parse(string? json)
    {
        if (SharingState.TryParse(json) is not { } root) return None;
        return new ReceivedSummary(Number(root, "pocet"), SharingState.Text(root, "odesilatel"),
            SharingState.Text(root, "nazev"), Number(root, "sablon"));
    }

    /// <summary>Věta pruhu v hlavním okně; null, když firmě nic nepřišlo.</summary>
    public string? Notice => Shipments switch
    {
        <= 0 => null,
        1 when Templates == 1 => $"Firma {Sender} vám poslala šablonu „{Name}“.",
        1 => $"Firma {Sender} vám poslala {Pocet(Templates, "šablonu", "šablony", "šablon")}.",
        _ => $"Jiné firmy vám poslaly šablony: {Pocet(Shipments, "zásilka", "zásilky", "zásilek")} k převzetí.",
    };

    private static string Pocet(int n, string jedna, string dve, string vic) =>
        $"{n} {(n == 1 ? jedna : n is >= 2 and <= 4 ? dve : vic)}";

    internal static int Number(System.Text.Json.Nodes.JsonNode node, string key) =>
        int.TryParse(SharingState.Text(node, key), out var n) ? n : 0;
}

/// <summary>Šablona přijaté zásilky před převzetím (<c>GetObsahZasilky</c>).</summary>
/// <param name="FileId">Soubor zásilky, který šablonu nese.</param>
/// <param name="AgendaTitle">Agenda a varianta ve tvaru stromu („Linkový JŘ · AD").</param>
/// <param name="Version">Verze formátu šablony; novější, než návrhář umí, převzít nejde.</param>
/// <param name="AgendaKnown">Agendu má návrhář této firmy.</param>
/// <param name="Existing">Šablona téhož původu, kterou firma už převzala; null u nové.</param>
public sealed record ReceivedTemplate(string FileId, string Name, string AgendaCode, string AgendaTitle, string Version,
    bool AgendaKnown, ExistingTakenTemplate? Existing)
{
    public static IReadOnlyList<ReceivedTemplate> ParseList(string? json)
    {
        if (SharingState.TryParse(json)?["sablony"] is not System.Text.Json.Nodes.JsonArray arr) return [];
        return [.. arr.OfType<System.Text.Json.Nodes.JsonNode>().Select(n => new ReceivedTemplate(
            SharingState.Text(n, "souborId"), SharingState.Text(n, "name"), SharingState.Text(n, "agendaCode"),
            SharingState.Text(n, "agenda"), SharingState.Text(n, "version"), SharingState.Text(n, "agendaZnama") == "true",
            n["existujici"] is { } e && Guid.TryParse(SharingState.Text(e, "templateId"), out var id)
                ? new ExistingTakenTemplate(id, SharingState.Text(e, "name"), SharingState.Text(e, "upravena") == "true")
                : null))];
    }
}

/// <summary>Šablona, kterou firma od téže firmy už převzala; <paramref name="Modified"/> = firma ji od převzetí změnila.</summary>
public sealed record ExistingTakenTemplate(Guid TemplateId, string Name, bool Modified);

/// <summary>Šablona uložená převzetím zásilky.</summary>
public sealed record TakenTemplate(Guid TemplateId, string Name, string AgendaCode)
{
    public static IReadOnlyList<TakenTemplate> ParseList(string? json)
    {
        if (SharingState.TryParse(json) is not System.Text.Json.Nodes.JsonArray arr) return [];
        return [.. arr.OfType<System.Text.Json.Nodes.JsonNode>()
            .Where(n => Guid.TryParse(SharingState.Text(n, "templateId"), out _))
            .Select(n => new TakenTemplate(Guid.Parse(SharingState.Text(n, "templateId")), SharingState.Text(n, "name"),
                SharingState.Text(n, "agendaCode")))];
    }
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
