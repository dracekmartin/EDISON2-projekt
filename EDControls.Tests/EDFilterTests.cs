using EDControls.EDFilter;
using EDTesting;

namespace EDControls.Tests;

/// <summary>
/// Wire kontrakt filtru (^||ML("IN","filter",Key)): prázdný klíč ani prázdná hodnota
/// se neposílají. Dosud hlídáno jen z Caché strany.
/// </summary>
public class EDFilterTests
{
    private sealed class FakeFilterField : EDFilterFieldBase
    {
        public string Value { get; set; } = "";
        public override string GetValue() => Value;
    }

    [Fact]
    public void BuildFilterDict_SkipsEmptyKeysAndEmptyValues()
    {
        Sta.Run(() =>
        {
            var filter = new EDFilter.EDFilter();
            filter.Fields.Add(new FakeFilterField { Key = "agendaCode", Value = "MHD" });
            filter.Fields.Add(new FakeFilterField { Key = "", Value = "bez klíče" });
            filter.Fields.Add(new FakeFilterField { Key = "obdobi", Value = "" });

            var dict = filter.BuildFilterDict();

            Assert.Equal(new Dictionary<string, string> { ["agendaCode"] = "MHD" }, dict);
        });
    }

    [Fact]
    public void BuildFilterDict_AllEmpty_ReturnsEmptyDict()
    {
        Sta.Run(() =>
        {
            var filter = new EDFilter.EDFilter();
            filter.Fields.Add(new FakeFilterField { Key = "agendaCode", Value = "" });

            Assert.Empty(filter.BuildFilterDict());
        });
    }

    [Fact]
    public void RefreshAsync_WithoutTarget_NoOpWithoutFilterChanged()
    {
        // Bez Targetu se nesmí ani střílet FilterChanged - host by reagoval na reload,
        // který neproběhne. Celé na STA - DP Target se čte na vlastnícím vlákně;
        // s null Targetem se RefreshAsync vrací synchronně, GetResult tu neuvázne.
        Sta.Run(() =>
        {
            var filter = new EDFilter.EDFilter();
            bool raised = false;
            filter.FilterChanged += (_, _) => raised = true;

            filter.RefreshAsync().GetAwaiter().GetResult();
            filter.Refresh();

            Assert.False(raised);
        });
    }
}
