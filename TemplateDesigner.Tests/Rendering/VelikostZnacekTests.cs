using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Pás značek platnosti a negativních značek nad časy má vlastní, větší velikost písma - starý
/// tisk má v programu `Plascale 9.0` proti `Spojscale 6.0` u časů, tedy jedenapůlkrát.
/// </summary>
public class VelikostZnacekTests
{
    private static LineTableElement Prvek(float pomer)
    {
        var el = ElementRenderFixtures.LineTable();
        el.PomerZnacek = pomer;
        el.DataRowHeight = 3f;   // pevná výška, ať se dá počítat s čísly
        return el;
    }

    /// <summary>Spoj se značkou platnosti i s negativní značkou, aby vznikly oba pásy.</summary>
    private static DataContext Data() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"),   ("PORADI", "2")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"), ("NZ", "23"),
                ("PLATNOST", new JsonArray("X")),
                ("CASY", new JsonArray("16:15", "16:40"))),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1"))]));

    /// <summary>
    /// ⚠️ Podstata: větší značky potřebují vyšší řádek. Kdyby se zvětšilo jen písmo, značka by se
    /// do řádku nevešla a pojistka v <c>NegativniZnackaRenderer</c> by ji zase zdrobnila - volba
    /// by tedy nedělala nic.
    /// </summary>
    [Fact]
    public void VetsiZnackyZvysiPrvek()
    {
        var data = Data();

        float bez = Prvek(1f).CalculateHeightFromData(data);
        float s   = Prvek(1.5f).CalculateHeightFromData(data);

        Assert.True(s > bez,
            $"prvek se nezvýšil ({bez:0.00} → {s:0.00} mm) - řádek značek zůstal stejný");
        // Dva pásy (platnost + negativní značky) po 3 mm se mají zvětšit o polovinu, tedy o 3 mm.
        Assert.Equal(3f, s - bez, 2);
    }

    [Fact]
    public void PomerJednaJeDnesniChovani()
    {
        var data = Data();

        float jedna = Prvek(1f).CalculateHeightFromData(data);
        float pod = Prvek(0.2f).CalculateHeightFromData(data);   // pod mezí - ořízne se na 1

        Assert.Equal(jedna, pod, 3);
    }

    /// <summary>Poměr nad mezí se ořízne, ať volba nevyrobí pás přes půl stránky.</summary>
    [Fact]
    public void PomerSeOrizneShora()
    {
        var data = Data();

        float tri = Prvek(3f).CalculateHeightFromData(data);
        float deset = Prvek(10f).CalculateHeightFromData(data);

        Assert.Equal(tri, deset, 3);
    }
}
