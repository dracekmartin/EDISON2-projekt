namespace EDTesting;

/// <summary>
/// Texty chyb ve tvaru, který skládá legacy <c>MLRest.MakeRequest</c> a <c>Query</c> (repo EDISON):
/// blok <c>[WebException.*]</c>, za ním <c>&lt;EDISON&gt;</c> se stackem a týž blok znovu.
/// Testy klasifikace (<c>MLRestFailure</c>) z nich staví výjimky, šev se skutečným MLRestem tvar ověřuje.
/// </summary>
public static class MLRestErrors
{
    private static readonly string NL = Environment.NewLine;

    /// <summary>Výpadek spojení bez odpovědi serveru.</summary>
    public static string Transport(string status = "UnknownError",
                                   string message = "An error occurred while sending the request.") =>
        Wrapped($"{NL}[WebException.Status]{NL}{status}{NL}[WebException.Message]{NL}{message}{NL}");

    /// <summary>Chyba z Caché (HTTP 500) se shrnutím od serveru.</summary>
    public static string ServerError(string summary, string httpStatus = "InternalServerError") =>
        Wrapped($"{NL}[WebException.Status]{NL}ProtocolError{NL}[WebException.Message]{NL}The remote server returned an error: (500) Internal Server Error.{NL}" +
                $"{NL}[WebException.Response.HttpStatusCode]{NL}{httpStatus}{NL}[WebException.Response]{NL}{summary}{NL}");

    /// <summary>Kód bez těla odpovědi (404, 503, 401).</summary>
    public static string StatusWithoutBody(string httpStatus = "ServiceUnavailable", string message = "(503) Server Unavailable") =>
        Wrapped($"{NL}[WebException.Status]{NL}ProtocolError{NL}[WebException.Message]{NL}The remote server returned an error: {message}.{NL}" +
                $"{NL}[WebException.Response.HttpStatusCode]{NL}{httpStatus}{NL}");

    /// <summary>Chybová stránka IIS místo odpovědi Caché.</summary>
    public static string IisErrorPage() =>
        Wrapped($"{NL}[WebException.Status]{NL}ProtocolError{NL}[WebException.Message]{NL}The remote server returned an error: (502) Bad Gateway.{NL}" +
                $"{NL}[WebException.Response.HttpStatusCode]{NL}BadGateway{NL}[IIS.HtmlErrorPage]{NL}Server returned an IIS error page (HTTP BadGateway){NL}");

    /// <summary>Server už nezná zámek session.</summary>
    public static string SessionEnd() => ServerError(
        "ERROR #5001: ERROR #5001: ACTIVITY: Lock exist failed! IDFA [CB_DP] / IP [10.0.0.1] / GUID [00000000-0000-0000-0000-000000000000]!" +
        "<ZTRAP> 64 zLockExist+5^ALWA.SYS.Activity.1 <ZTRAP> 64 zQuery+23^ALWA.SYS.Rest.1");

    /// <summary>Verze programu nesedí s Caché (instalace FULL za běhu).</summary>
    public static string VersionMismatch() => ServerError(
        "ERROR #5001: ERROR #5001: EdisonVersionFailed!<ZTRAP> 64 zEdisonVersion+15^ALWA.SYS.Rest.1 <ZTRAP> 64 zQuery+19^ALWA.SYS.Rest.1");

    /// <summary>Legacy volání (<c>Database.GetCompanyList</c>) obalí text MLRestu do vlastní výjimky.</summary>
    public static string LegacyWrapped(string inner) =>
        $"<stack>   at ALVA.EDISONAPI.Database.GetCompanyList(String& errState, MLRest mlRest, Boolean checkAccess)<message>{inner}";

    private static string Wrapped(string block) =>
        $"{block}<EDISON>   at ALWA.SYS.MLRest.Query(String calledClass, String calledMethod, CVar varIn){NL}{block}";
}
