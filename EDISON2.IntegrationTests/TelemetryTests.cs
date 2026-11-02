using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;

namespace EDISON2.IntegrationTests;

/// <summary>
/// Usage telemetrie: řádky dávky skládá <see cref="EDUsageTelemetry"/> s oddělovačem MLRest, Caché kontrakt
/// (<c>TelemetryContractTest</c>) je posílá s <c>~</c>. Rozjetý oddělovač server tiše přeskočí a <c>Flush</c> nic nehlásí.
/// </summary>
[Trait("Category", "Integration")]
public class TelemetryTests
{
    private const string Fixture = "ALVA.EDTesting.ITTelemetry";
    private const string Control = "BtnTest";

    [DevServerFact]
    public void Flush_TwoClicks_ServerCountsBothUnderSessionUser()
    {
        // Záznam bez firmy jde na session z Flush; nastavené EDServices by ho poslalo pod svou firmu.
        Assert.Null(EDServices.CurrentOrNull);
        var rest = DevServer.Connect(DevServer.WriteIdfa, DevServer.WriteMlp);
        // Čítač nemá GUID: souběžný běh jiného vývojáře pod stejnou firmou a uživatelem má vlastní formulář.
        var form = $"EDISON2.IntegrationTests.Telemetry.{Guid.NewGuid():N}";
        var input = new CVar();
        input["form"] = new CVar(form);
        input["control"] = new CVar(Control);
        try
        {
            EDUsageTelemetry.ResetForTests();
            var before = DateTime.Now.AddSeconds(-1);
            EDUsageTelemetry.Track(form, Control);
            EDUsageTelemetry.Track(form, Control);

            EDUsageTelemetry.Flush(rest);

            var stored = rest.QueryOrThrow(Fixture, "Read", input);
            Assert.Equal("2", stored["count"].Value?.ToString());
            var last = DateTime.ParseExact(stored["last"].Value?.ToString() ?? "", "yyyy-MM-dd HH:mm:ss", null);
            Assert.InRange(last, before, DateTime.Now.AddSeconds(1));
        }
        finally
        {
            EDUsageTelemetry.ResetForTests();
            rest.QueryOrThrow(Fixture, "Clean", input);
        }
    }
}
