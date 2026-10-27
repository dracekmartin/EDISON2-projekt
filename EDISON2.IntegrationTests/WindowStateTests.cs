using System.Windows;
using ALWA.SYS;
using EDControls;

namespace EDISON2.IntegrationTests;

/// <summary>Geometrie oken přes legacy <c>ALVA.LIBRARY.Form</c>: poziční řádky <c>param</c>, čísla v invariant culture.</summary>
[Trait("Category", "Integration")]
public class WindowStateTests
{
    [DevServerFact]
    public void SaveThenLoad_RealServer_RoundTripsGeometry()
    {
        // Unikátní formID: souběžný běh pod toutéž testovací identitou nesdílí klíč.
        var rest = DevServer.Connect(DevServer.WriteIdfa, DevServer.WriteMlp);
        var formId = $"EDISON2.IT.{Guid.NewGuid():N}";
        try
        {
            var store = new CacheWindowStateStore(formId, () => rest);

            // Záporná souřadnice = monitor vlevo; zlomky se při uložení zaokrouhlují.
            store.Save(new Rect(-1280, 40, 1024.4, 768.6), maximized: true);
            Assert.True(BackgroundWrites.WaitPending(TimeSpan.FromSeconds(10)));   // Save jde na pozadí

            Assert.Equal(new WindowGeometry(-1280, 40, 1024, 769, true), store.Load());
        }
        finally
        {
            var del = rest.Delimiter;
            rest.QueryOrThrow("ALVA.LIBRARY.Form", "SaveParams", new object[] { $"formID{del}{formId}", $"clear{del}1" });
        }
    }

    [DevServerFact]
    public void ResetUserStates_RealServer_ClearsOnlyGivenPrefix()
    {
        // Vlastní prefix místo EDISON2/TD: reset pod testovací identitou nesmí smazat geometrii souběžného testu.
        var rest = DevServer.Connect(DevServer.WriteIdfa, DevServer.WriteMlp);
        var prefix = $"IT{Guid.NewGuid():N}";
        var reset = new CacheWindowStateStore($"{prefix}.Win", () => rest);
        var kept = new CacheWindowStateStore($"{prefix}X.Win", () => rest);
        try
        {
            reset.Save(new Rect(-1280, 40, 800, 600), maximized: false);
            kept.Save(new Rect(100, 100, 800, 600), maximized: false);
            Assert.True(BackgroundWrites.WaitPending(TimeSpan.FromSeconds(10)));

            var input = new CVar();
            input["prefixes"] = new CList(prefix);
            var output = rest.QueryOrThrow("ALVA.EDControls.WindowState", "ResetUserStates", input);

            Assert.Equal("1", output["deleted"]?.Value?.ToString());
            Assert.Equal(WindowGeometry.None, reset.Load());
            Assert.Equal(new WindowGeometry(100, 100, 800, 600, false), kept.Load());
        }
        finally
        {
            var del = rest.Delimiter;
            foreach (var formId in new[] { $"{prefix}.Win", $"{prefix}X.Win" })
                rest.QueryOrThrow("ALVA.LIBRARY.Form", "SaveParams", new object[] { $"formID{del}{formId}", $"clear{del}1" });
        }
    }
}
