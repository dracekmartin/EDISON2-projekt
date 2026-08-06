using System.Text.Json.Nodes;

namespace EDControls.ErrorReporting;

/// <summary>Kontext hlášení - prosté hodnoty, ať jde jádro testovat bez MLRestu a bez WPF.</summary>
public sealed record ErrorContext
{
    /// <summary>Identita aplikace (<c>EDServices.FormIdPrefix</c>): <c>EDISON2</c> / <c>TD</c>.</summary>
    public required string AppId { get; init; }

    /// <summary>Běh na m-line doméně - záznam se označí, nezahodí.</summary>
    public bool IsInternal { get; init; }

    public string? Idfa { get; init; }
    public string? LoginName { get; init; }
    public string? FullName { get; init; }
    public string? Mlp { get; init; }
    public string? AppVersion { get; init; }

    /// <summary>Jmenný prostor okna - jde do <c>module</c>, tedy do seskupení v Přehledu.</summary>
    public string? WindowNamespace { get; init; }

    /// <summary>Titulek okna - jde do <c>name</c>, tedy do popisku řádku v Přehledu.</summary>
    public string? WindowTitle { get; init; }

    public string? MachineName { get; init; }
}

/// <summary>Překlad výjimky a kontextu na <see cref="ErrorReport"/>. Čistá funkce, bez I/O.</summary>
public static class ErrorReportBuilder
{
    /// <summary>
    /// Klíče v <c>data</c>, kterými heartbeat hlásí počty jednotek. Server podle nich pozná
    /// „záznam nese aktuální verzi EDISONu" a přepíše firmě odznak verze podle
    /// <c>data.version</c> (<c>telemetryIngestController.hasUnitMetrics</c>). My verzi posílat
    /// potřebujeme, takže se těmhle klíčům musíme vyhnout - jinak by každá chyba z EDISON2
    /// přepsala zákazníkovi verzi legacy EDISONu.
    /// </summary>
    internal static readonly string[] ForbiddenDataKeys =
        ["pocty", "ad_pocetRZ", "mhd_pocetRZ", "zam_pocetZam"];

    /// <summary>Druhá půlka téže pasti: instalační událost se pozná podle module+name.</summary>
    internal const string InstallEventModule = "EDISON";
    internal const string InstallEventName = "Instalace verze";

    private const string TypeError = "error";
    private const string TypeInfo = "info";

    /// <summary>Automatický záznam v okamžiku chyby.</summary>
    public static ErrorReport Build(Exception ex, ErrorContext ctx,
                                    IReadOnlyList<ErrorAttachment>? attachments = null,
                                    int repeatCount = 1)
    {
        ArgumentNullException.ThrowIfNull(ex);
        ArgumentNullException.ThrowIfNull(ctx);

        var refId = ErrorReport.NewRefId();
        var data = BaseData(ctx, refId);
        data["exceptionType"] = ex.GetType().FullName;
        data["message"] = ex.Message;
        data["stack"] = ex.ToString();   // včetně inner exceptions - tam bývá skutečná příčina
        data["stage"] = ErrorReport.StageAuto;
        if (repeatCount > 1) data["repeatCount"] = repeatCount;

        return new ErrorReport
        {
            RefId = refId,
            Source = ErrorReport.ResolveSource(ctx.AppId, ctx.IsInternal),
            Type = TypeError,
            Module = ResolveModule(ctx),
            Name = ResolveName(ctx),
            Partner = Blank(ctx.Idfa),
            Description = $"{refId} | {ex.GetType().Name}: {OneLine(ex.Message)}",
            Data = data,
            Attachments = attachments ?? [],
        };
    }

    /// <summary>
    /// Dobrovolné doplnění od uživatele. Samostatný záznam typu <c>info</c> - páruje se přes
    /// <c>refId</c> v description, takže na serveru není potřeba nic měnit.
    /// </summary>
    public static ErrorReport BuildFollowUp(string refId, string userText, ErrorContext ctx)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refId);

        var data = BaseData(ctx, refId);
        data["stage"] = ErrorReport.StageUser;
        data["userText"] = userText;

        return new ErrorReport
        {
            RefId = refId,
            Source = ErrorReport.ResolveSource(ctx.AppId, ctx.IsInternal),
            Type = TypeInfo,
            Module = ResolveModule(ctx),
            Name = ResolveName(ctx),
            Partner = Blank(ctx.Idfa),
            Description = $"{refId} | doplnění uživatele",
            Data = data,
        };
    }

    private static JsonObject BaseData(ErrorContext ctx, string refId) => new()
    {
        ["refId"] = refId,
        ["version"] = ctx.AppVersion,
        ["form"] = ctx.WindowNamespace,
        ["formTitle"] = ctx.WindowTitle,
        ["firma"] = Blank(ctx.Idfa),
        ["login"] = Blank(ctx.LoginName),
        ["fullName"] = Blank(ctx.FullName),
        ["userId"] = Blank(ctx.Mlp),
        ["machine"] = Blank(ctx.MachineName),
        ["framework"] = Environment.Version.ToString(),
        ["internal"] = ctx.IsInternal,
        ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
    };

    // module=EDISON by ve dvojici s name="Instalace verze" spustil přepis verze u firmy;
    // fallback na AppId to drží mimo ten vzor i pro okno bez jmenného prostoru.
    private static string ResolveModule(ErrorContext ctx) =>
        Blank(ctx.WindowNamespace) ?? ctx.AppId;

    private static string ResolveName(ErrorContext ctx) =>
        Blank(ctx.WindowTitle) ?? Blank(ctx.WindowNamespace) ?? ctx.AppId;

    private static string OneLine(string? s) =>
        (s ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
