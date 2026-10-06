using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy wire tvaru zástupce pro 14. argument hosta. Kontrakt je sdílený s legacy
/// <c>ALVA.LIBRARY.Remote.SerializableShortcut</c> (host ho deserializuje toutéž třídou),
/// takže jména polí a číselné hodnoty druhů jsou zmrazené.
/// </summary>
public class ShortcutLaunchPayloadTests
{
    private static DesktopShortcut Report(string id, ShortcutKind kind = ShortcutKind.PrintReport) => new()
    {
        Kind = kind,
        Id = id,
        ElementId = "145008",
        Name = "Výpis typů vozidel",
        ExecutablePath = "VOZIDLA2.exe",
        FormName = "ALVA.VOZIDLA2.formVOZIDLA2List",
    };

    [Fact]
    public void Encode_IsArgvSafe()
    {
        // EdisonLaunchArgs.Format hodnoty jen obaluje uvozovkami a uvozovky z nich odstraní - payload
        // proto nesmí obsahovat uvozovku, mezeru ani konec řádku.
        var encoded = ShortcutLaunchPayloadCodec.Encode(Report("1003"));

        Assert.DoesNotContain('"', encoded);
        Assert.DoesNotContain(' ', encoded);
        Assert.DoesNotContain('\n', encoded);
        Assert.DoesNotContain('\r', encoded);
    }

    [Fact]
    public void Encode_KeepsReportIdVerbatim()
    {
        // Legacy převádí ID sestavy přes Conversion.Val, takže nečíselná hodnota
        // tiše spadne na 0 a tisk mlčky neudělá nic. ID musí projít beze změny až do payloadu.
        var payload = ShortcutLaunchPayloadCodec.Decode(ShortcutLaunchPayloadCodec.Encode(Report("1003")));

        Assert.Equal("1003", payload.ID);
    }

    [Fact]
    public void Encode_PrintReport_IsProcedureCall()
    {
        var payload = ShortcutLaunchPayloadCodec.Decode(ShortcutLaunchPayloadCodec.Encode(Report("1003")));

        Assert.Equal(1, payload.CallType);   // Procedura
        Assert.Equal(4, payload.ItemType);   // SestavaTiskova
        Assert.Equal(145008, payload.Form);  // hostitelská forma
    }

    [Theory]
    [InlineData(ShortcutKind.MenuModule, 0, 0)]
    [InlineData(ShortcutKind.PackageForm, 0, 1)]
    [InlineData(ShortcutKind.DwhView, 0, 2)]
    [InlineData(ShortcutKind.SelectReport, 0, 3)]
    [InlineData(ShortcutKind.PrintReport, 1, 4)]
    public void Encode_MapsKindToLegacyEnums(ShortcutKind kind, int callType, int itemType)
    {
        var payload = ShortcutLaunchPayloadCodec.Decode(
            ShortcutLaunchPayloadCodec.Encode(Report("1003", kind)));

        Assert.Equal(callType, payload.CallType);
        Assert.Equal(itemType, payload.ItemType);
    }

    [Fact]
    public void Encode_CarriesReportPayload()
    {
        var shortcut = new DesktopShortcut
        {
            Kind = ShortcutKind.SelectReport,
            Id = "1003",
            ElementId = "145008",
            Name = "Sestava",
            Report = new ReportPayload("ES", "EkonomickySystem", "1", "S12", "P7", JeVerejny: true),
        };

        var payload = ShortcutLaunchPayloadCodec.Decode(ShortcutLaunchPayloadCodec.Encode(shortcut));

        Assert.True(payload.JdeOReportShortcut);   // host podle toho volí ReportShortcut
        Assert.Equal("ES", payload.Agenda);
        Assert.Equal("EkonomickySystem", payload.Oblast);
        Assert.Equal("1", payload.TypSestavy);
        Assert.Equal("S12", payload.Sestava);
        Assert.Equal("P7", payload.Predvolba);
        Assert.True(payload.JeVerejny);
    }

    [Fact]
    public void Encode_WithoutReport_IsNotReportShortcut()
    {
        var payload = ShortcutLaunchPayloadCodec.Decode(ShortcutLaunchPayloadCodec.Encode(Report("1003")));

        Assert.False(payload.JdeOReportShortcut);
    }

    [Fact]
    public void Encode_CarriesInputParamAsInputProFrm()
    {
        // Legacy EDParamIn - formulář z něj čte předvolby; ztráta by byla tichá.
        var shortcut = new DesktopShortcut
        {
            Kind = ShortcutKind.PackageForm, Id = "9", ElementId = "660",
            Name = "F", ExecutablePath = "x.exe", FormName = "ALVA.X.frm", InputParam = "APM=1",
        };

        var payload = ShortcutLaunchPayloadCodec.Decode(ShortcutLaunchPayloadCodec.Encode(shortcut));

        Assert.Equal("APM=1", payload.InputProFrm);
    }
}
