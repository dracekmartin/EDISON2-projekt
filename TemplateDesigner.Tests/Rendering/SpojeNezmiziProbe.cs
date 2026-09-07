using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>Dočasná sonda: pokryjí bloky všechny spoje, nebo se některé nevykreslí?</summary>
public class SpojeNezmiziProbe
{
    /// <summary>Pořadí spojů, jaké posílá server na CB_GWBUS 2 / 0320144 směr Tam.</summary>
    private static readonly string[] PoradiZeServeru =
    [
        "61", "1", "31", "33", "5", "73", "37", "63", "501", "7", "65", "67", "69", "503", "505",
        "43", "11", "45", "13", "77", "49", "507", "71", "509", "51",
        "35", "511", "39", "513", "41", "515", "47", "517", "51",
        "35", "511", "39", "513", "41", "515", "47", "517", "51",
    ];

    [Fact]
    public void BlokyPokryjiVsechnySpoje()
    {
        var spoje = PoradiZeServeru
            .Select((c, idx) => RenderSnapshot.Row(
                ("CISLO_SPOJE", c),
                ("SKUPINA", idx < 25 ? "1" : idx < 34 ? "2" : "3"),
                ("CASY", new System.Text.Json.Nodes.JsonArray("8:00", "8:10", "8:20"))))
            .ToArray();

        var data = RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "České Budějovice,,aut.nádr."), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "Hosín"), ("PORADI", "2")),
                RenderSnapshot.Row(("ID", "3"), ("NAZEV_ZASTAVKY", "Hluboká n.Vlt.,,žel.st."), ("PORADI", "3")),
            ]),
            ("SPOJE", spoje),
            ("SKUPINY_DNU",
            [
                RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "2"), ("NADPIS", "Soboty"), ("PORADI", "2")),
                RenderSnapshot.Row(("ID", "3"), ("NADPIS", "Neděle a svátky"), ("PORADI", "3")),
            ]));

        var prvek = new LineTableElement { X = 0, Y = 0, Width = 190, DataRowHeight = 5f };
        var rozvrh = prvek.Rozvrzeni(data);

        var rozpis = string.Join(Environment.NewLine, rozvrh.Select(b =>
            $"[{b.Od}+{b.Pocet}] {string.Join(",", PoradiZeServeru.Skip(b.Od).Take(b.Pocet))}"));
        Assert.True(PoradiZeServeru.Length == rozvrh.Sum(b => b.Pocet),
            $"bloku={rozvrh.Count} pokryto={rozvrh.Sum(b => b.Pocet)} z {PoradiZeServeru.Length}"
            + Environment.NewLine + rozpis);
    }
}
