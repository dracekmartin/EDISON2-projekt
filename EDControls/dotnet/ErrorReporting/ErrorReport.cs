using System.Text.Json.Nodes;

namespace EDControls.ErrorReporting;

/// <summary>Příloha hlášení (snímek okna). Base64 v <see cref="Data"/> - tak to chce ingest API.</summary>
internal sealed record ErrorAttachment(string FileName, string MimeType, string Data);

/// <summary>
/// Jedno hlášení chyby pro <c>podpora.m-line.cz/api/telemetry/ingest</c>. Čistý překlad
/// výjimky na payload - bez WPF a bez HTTP, aby šel testovat přímo.
/// </summary>
internal sealed class ErrorReport
{
    /// <summary>Fáze hlášení: automatický záznam vs. dobrovolné doplnění od uživatele.</summary>
    public const string StageAuto = "auto";
    public const string StageUser = "user";

    /// <summary>Referenční číslo - uživatel ho hlásí na podporu, doplnění se páruje přes něj.</summary>
    public required string RefId { get; init; }

    /// <summary>Zdroj = identita aplikace, u interních běhů se suffixem (viz <see cref="ResolveSource"/>).</summary>
    public required string Source { get; init; }

    public required string Type { get; init; }
    public required string Module { get; init; }
    public required string Name { get; init; }
    public string? Partner { get; init; }
    public required string Description { get; init; }
    public JsonObject Data { get; init; } = [];
    public IReadOnlyList<ErrorAttachment> Attachments { get; init; } = [];

    /// <summary>
    /// Suffix zdroje pro běhy na m-line doméně. Interní pády **nezahazujeme** (na rozdíl od
    /// legacy `IsDomainMline`), jen je odlišíme - Přehled telemetrie filtruje podle sloupce
    /// Zdroj a volby si bere z dat, takže se nová hodnota objeví sama.
    /// </summary>
    public const string InternalSuffix = "-internal";

    public static string ResolveSource(string appId, bool isInternal) =>
        isInternal ? appId + InternalSuffix : appId;

    /// <summary>
    /// Referenční číslo. Delší než legacy `x`+5 znaků - to se při větším objemu začne
    /// opakovat a dvě různé chyby pod jedním číslem podporu zmatou víc, než pomůže.
    /// </summary>
    public static string NewRefId() => "x" + Guid.NewGuid().ToString("N")[..10];

    /// <summary>
    /// Serializuje na tvar, který čeká <c>ingestTelemetryHandler</c>: povinné
    /// <c>source/type/module/name</c>, volitelné <c>partner/description/data/attachments</c>.
    /// </summary>
    public JsonObject ToJson()
    {
        var json = new JsonObject
        {
            ["source"] = Source,
            ["type"] = Type,
            ["module"] = Module,
            ["name"] = Name,
            ["partner"] = Partner,
            ["description"] = Description,
            ["data"] = Data.DeepClone(),
        };

        if (Attachments.Count > 0)
        {
            var arr = new JsonArray();
            foreach (var a in Attachments)
                arr.Add(new JsonObject
                {
                    ["filename"] = a.FileName,
                    ["mimeType"] = a.MimeType,
                    ["data"] = a.Data,
                });
            json["attachments"] = arr;
        }

        return json;
    }
}
