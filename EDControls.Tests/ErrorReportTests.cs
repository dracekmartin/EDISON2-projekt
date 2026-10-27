using System.Text.Json.Nodes;
using EDControls.ErrorReporting;

namespace EDControls.Tests;

/// <summary>
/// Složení payloadu pro <c>/api/telemetry/ingest</c>. Kontrakt serveru je ověřený
/// v <c>edwcore/apps/mlweb/backend</c>, tyhle testy hlídají, že se od něj nerozejdeme.
/// </summary>
public class ErrorReportTests
{
    private static ErrorContext Ctx(bool isInternal = false) => new()
    {
        AppId = "EDISON2",
        IsInternal = isInternal,
        Idfa = "CB_MLINEAS",
        LoginName = "mhubata",
        FullName = "Martin Hubata",
        Mlp = "42",
        AppVersion = "2.1.3",
        WindowNamespace = "EDISONMAIN2.Windows.UserProfileWindow",
        WindowTitle = "Uživatelský profil",
        MachineName = "PC-TEST",
    };

    private static Exception Thrown(string message = "boom")
    {
        try { throw new InvalidOperationException(message); }
        catch (Exception ex) { return ex; }
    }

    [Fact]
    public void Json_HasAllRequiredFields()
    {
        // Server odmítne 400, když chybí kterékoli z těchhle čtyř.
        var json = ErrorReportBuilder.Build(Thrown(), Ctx()).ToJson();

        Assert.False(string.IsNullOrEmpty(json["source"]?.GetValue<string>()));
        Assert.False(string.IsNullOrEmpty(json["type"]?.GetValue<string>()));
        Assert.False(string.IsNullOrEmpty(json["module"]?.GetValue<string>()));
        Assert.False(string.IsNullOrEmpty(json["name"]?.GetValue<string>()));
    }

    [Fact]
    public void Json_RequiredFieldsSurviveEmptyContext()
    {
        // Pád před loginem: bez okna, bez session. Povinná pole musí zůstat vyplněná.
        var ctx = new ErrorContext { AppId = "TD" };

        var json = ErrorReportBuilder.Build(Thrown(), ctx).ToJson();

        Assert.Equal("TD", json["source"]?.GetValue<string>());
        Assert.Equal("TD", json["module"]?.GetValue<string>());
        Assert.Equal("TD", json["name"]?.GetValue<string>());
        Assert.Null(json["partner"]?.GetValue<string>());
    }

    [Fact]
    public void Type_IsAllowedValue()
    {
        // VALID_TYPES na serveru = info | warning | error.
        Assert.Equal("error", ErrorReportBuilder.Build(Thrown(), Ctx()).Type);
        Assert.Equal("info", ErrorReportBuilder.BuildFollowUp("x123", "klikal jsem na Uložit", Ctx()).Type);
    }

    [Fact]
    public void Build_TypeParameterOverridesDefaultError()
    {
        Assert.Equal("warning", ErrorReportBuilder.Build(Thrown(), Ctx(), type: ErrorReportBuilder.TypeWarning).Type);
        Assert.Equal("info", ErrorReportBuilder.Build(Thrown(), Ctx(), type: ErrorReportBuilder.TypeInfo).Type);
    }

    [Theory]
    [InlineData(ErrorReportType.Info, "info")]
    [InlineData(ErrorReportType.Warning, "warning")]
    [InlineData(ErrorReportType.Error, "error")]
    public void TypeName_MapsToServerValues(ErrorReportType type, string expected) =>
        Assert.Equal(expected, ErrorReportBuilder.TypeName(type));

    [Fact]
    public void BuildEvent_CarriesModuleNameMessageAndDetails()
    {
        var report = ErrorReportBuilder.BuildEvent("warning", "EDUPDATE", "Aktualizace stanice", "Aktualizace selhala: zámek",
            new JsonObject { ["exitCode"] = 100, ["failure"] = "zámek" }, Ctx());

        Assert.Equal("warning", report.Type);
        Assert.Equal("EDUPDATE", report.Module);
        Assert.Equal("Aktualizace stanice", report.Name);
        Assert.Equal("CB_MLINEAS", report.Partner);
        Assert.Equal($"{report.RefId} | Aktualizace selhala: zámek", report.Description);
        Assert.Equal("Aktualizace selhala: zámek", report.Data["message"]!.GetValue<string>());
        Assert.Equal(100, report.Data["exitCode"]!.GetValue<int>());
        Assert.Empty(report.Attachments);
    }

    [Fact]
    public void BuildEvent_InstallEventPair_IsRenamed()
    {
        // Modul a jméno tu určuje volající - dvojici instalační události builder nepustí.
        var report = ErrorReportBuilder.BuildEvent("info", ErrorReportBuilder.InstallEventModule,
            ErrorReportBuilder.InstallEventName, "x", null, Ctx());

        Assert.False(report.Module == ErrorReportBuilder.InstallEventModule &&
                     report.Name == ErrorReportBuilder.InstallEventName);
    }

    [Fact]
    public void BuildEvent_DetailsCannotOverrideBaseDataOrCarryUnitMetricKeys()
    {
        var details = new JsonObject { ["version"] = "podvrh", ["firma"] = "JINA", ["pocty"] = 5, ["zam_pocetZam"] = 1 };

        var report = ErrorReportBuilder.BuildEvent("error", "EDUPDATE", "Aktualizace stanice", "x", details, Ctx());

        Assert.Equal("2.1.3", report.Data["version"]!.GetValue<string>());
        Assert.Equal("CB_MLINEAS", report.Data["firma"]!.GetValue<string>());
        foreach (var forbidden in ErrorReportBuilder.ForbiddenDataKeys)
            Assert.False(report.Data.ContainsKey(forbidden), $"data nesmí nést klíč '{forbidden}'");
    }

    [Fact]
    public void BuildEvent_LongMessage_DescriptionIsShortenedMessageKeepsFullText()
    {
        var message = new string('a', 1000);

        var report = ErrorReportBuilder.BuildEvent("error", "EDUPDATE", "Aktualizace stanice", message, null, Ctx());

        Assert.True(report.Description.Length < 400);
        Assert.Equal(message, report.Data["message"]!.GetValue<string>());
    }

    [Fact]
    public void Never_LooksLikeInstallEvent()
    {
        // module="EDISON" + name="Instalace verze" spustí na serveru applyEdisonVersions
        // a přepsalo by firmě odznak verze legacy EDISONu podle našeho data.version.
        var report = ErrorReportBuilder.Build(Thrown(), Ctx());

        Assert.False(report.Module == ErrorReportBuilder.InstallEventModule &&
                     report.Name == ErrorReportBuilder.InstallEventName);
    }

    [Fact]
    public void Never_CarriesUnitMetricKeys()
    {
        // Druhá půlka téže pasti: hasUnitMetrics() bere kterýkoli z těchhle klíčů jako
        // „heartbeat s počty" a zase sáhne na data.version.
        var report = ErrorReportBuilder.Build(Thrown(), Ctx());

        foreach (var forbidden in ErrorReportBuilder.ForbiddenDataKeys)
            Assert.False(report.Data.ContainsKey(forbidden), $"data nesmí nést klíč '{forbidden}'");
    }

    [Fact]
    public void Version_IsSent()
    {
        // Verze je pro diagnostiku nezbytná - past výše se obchází vyhnutím se vzorům, ne
        // vypuštěním verze.
        var report = ErrorReportBuilder.Build(Thrown(), Ctx());

        Assert.Equal("2.1.3", report.Data["version"]?.GetValue<string>());
    }

    [Fact]
    public void InternalRun_GetsSuffixedSource()
    {
        // Přehled telemetrie filtruje podle sloupce Zdroj a volby bere z dat, takže nová
        // hodnota se ve filtru objeví sama.
        Assert.Equal("EDISON2", ErrorReportBuilder.Build(Thrown(), Ctx(isInternal: false)).Source);
        Assert.Equal("EDISON2-internal", ErrorReportBuilder.Build(Thrown(), Ctx(isInternal: true)).Source);
    }

    [Fact]
    public void InternalRun_IsNotDropped()
    {
        // Na rozdíl od legacy IsDomainMline se interní běh pořád posílá.
        var report = ErrorReportBuilder.Build(Thrown(), Ctx(isInternal: true));

        Assert.True(report.Data["internal"]?.GetValue<bool>());
    }

    [Fact]
    public void Description_StartsWithRefId()
    {
        var report = ErrorReportBuilder.Build(Thrown("něco se pokazilo"), Ctx());

        Assert.StartsWith(report.RefId, report.Description);
        Assert.Contains("InvalidOperationException", report.Description);
    }

    [Fact]
    public void Description_IsSingleLine()
    {
        // Description jde do předmětu tiketu - víceřádkový by ho rozbil.
        var report = ErrorReportBuilder.Build(Thrown("první řádek\r\ndruhý řádek"), Ctx());

        Assert.DoesNotContain('\n', report.Description);
        Assert.DoesNotContain('\r', report.Description);
    }

    [Fact]
    public void Stack_IncludesInnerException()
    {
        // ex.ToString() nese i inner exceptions - tam bývá skutečná příčina.
        Exception captured;
        try { throw new InvalidOperationException("vnější", new TimeoutException("vnitřní")); }
        catch (Exception ex) { captured = ex; }

        var stack = ErrorReportBuilder.Build(captured, Ctx()).Data["stack"]?.GetValue<string>();

        Assert.Contains("TimeoutException", stack);
        Assert.Contains("vnitřní", stack);
    }

    [Fact]
    public void RefId_IsUniqueEnough()
    {
        var ids = Enumerable.Range(0, 500).Select(_ => ErrorReport.NewRefId()).ToHashSet();

        Assert.Equal(500, ids.Count);
    }

    [Fact]
    public void Attachments_UseServerFieldNames()
    {
        var att = new ErrorAttachment("okno1_MainWindow.png", "image/png", "AAAA");

        var json = ErrorReportBuilder.Build(Thrown(), Ctx(), [att]).ToJson();

        var first = json["attachments"]!.AsArray()[0]!;
        Assert.Equal("okno1_MainWindow.png", first["filename"]?.GetValue<string>());
        Assert.Equal("image/png", first["mimeType"]?.GetValue<string>());
        Assert.Equal("AAAA", first["data"]?.GetValue<string>());
    }

    [Fact]
    public void NoAttachments_OmitsKeyEntirely()
    {
        // Prázdné pole by validací prošlo, ale nemá smysl ho posílat.
        var json = ErrorReportBuilder.Build(Thrown(), Ctx()).ToJson();

        Assert.False(json.ContainsKey("attachments"));
    }

    [Fact]
    public void FollowUp_KeepsSameRefIdAndCarriesText()
    {
        var follow = ErrorReportBuilder.BuildFollowUp("xabc123", "ukládal jsem šablonu", Ctx());

        Assert.Equal("xabc123", follow.RefId);
        Assert.Contains("xabc123", follow.Description);
        Assert.Equal("ukládal jsem šablonu", follow.Data["userText"]?.GetValue<string>());
        Assert.Equal(ErrorReport.StageUser, follow.Data["stage"]?.GetValue<string>());
    }

    [Fact]
    public void AutoReport_IsMarkedAsAutoStage()
    {
        var report = ErrorReportBuilder.Build(Thrown(), Ctx());

        Assert.Equal(ErrorReport.StageAuto, report.Data["stage"]?.GetValue<string>());
    }

    [Fact]
    public void RepeatCount_OnlyWhenRepeated()
    {
        Assert.False(ErrorReportBuilder.Build(Thrown(), Ctx(), null, 1).Data.ContainsKey("repeatCount"));
        Assert.Equal(7, ErrorReportBuilder.Build(Thrown(), Ctx(), null, 7).Data["repeatCount"]?.GetValue<int>());
    }

    [Fact]
    public void ToJson_DoesNotShareDataInstance()
    {
        // Payload se skládá na pozadí; sdílená instance by šla změnit pod rukama odesílání.
        var report = ErrorReportBuilder.Build(Thrown(), Ctx());

        var json = report.ToJson();
        ((JsonObject)json["data"]!)["message"] = "přepsáno";

        Assert.NotEqual("přepsáno", report.Data["message"]?.GetValue<string>());
    }
}
