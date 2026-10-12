using System.Windows;
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
}
