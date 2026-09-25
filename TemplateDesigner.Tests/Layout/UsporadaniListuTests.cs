using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Nakreslené uspořádání stránek dokladu na list (`B186`). Režim „víc dokladů": další výskyt téže
/// stránky na straně PDF = další doklad; režim „jeden doklad": stejná stránka = kopie.
/// </summary>
public class UsporadaniListuTests
{
    private static readonly PageSize A5 = new() { Width = 148, Height = 210 };

    private static UsporadaniListu Plan(int?[] strana1, int?[] strana2, bool jedenDoklad) => new UsporadaniListu
    {
        Format = "A4_LANDSCAPE", Radku = 1, Sloupcu = 2, JedenDoklad = jedenDoklad,
        Predni = [.. strana1], Zadni = [.. strana2],
    }.Normalizuj();

    /// <summary>Doklad = stránka 1 (index 0) a stránka 2 (index 1).</summary>
    private static List<IReadOnlyList<(Page Page, int Stranka)>> Doklady(int pocet, bool dveStranky = true) =>
        [.. Enumerable.Range(0, pocet).Select(_ => (IReadOnlyList<(Page, int)>)
            (dveStranky ? [(new Page(), 0), (new Page(), 1)] : [(new Page(), 0)]))];

    private static SheetLayoutOptions Volby(UsporadaniListu plan) => SheetComposer.ZPlanu(plan, A5)!;

    [Fact]
    public void VicDokladu_PopisyNesouPoradiDokladu()
    {
        var plan = Plan([0, 0], [1, 1], jedenDoklad: false);

        Assert.Equal("Stránka 1 · 1. doklad", plan.PopisPole(false, 0));
        Assert.Equal("Stránka 1 · 2. doklad", plan.PopisPole(false, 1));
        Assert.Equal("Stránka 2 · 2. doklad", plan.PopisPole(true, 1));
        Assert.True(plan.DveStrany);
    }

    [Fact]
    public void JedenDoklad_StrankyVedleSebeBezPoradi()
    {
        var plan = Plan([0, 1], [null, null], jedenDoklad: true);

        Assert.Equal(1, plan.DokladuNaList);
        Assert.Equal(["Stránka 1", "Stránka 2"], [plan.PopisPole(false, 0), plan.PopisPole(false, 1)]);
        Assert.False(plan.DveStrany);
    }

    /// <summary>⚠️ Jádro #12854: dvě stazky - na 1. straně PDF stránky 1, na 2. straně stránky 2 na týchž místech.</summary>
    [Fact]
    public void DveStazky_DveStranyPdf()
    {
        var doklady = Doklady(2);
        var listy = SheetComposer.ComposeDle(doklady, Volby(Plan([0, 0], [1, 1], jedenDoklad: false)));

        Assert.Equal(2, listy.Count);
        Assert.Same(doklady[0][0].Page, listy[0].Pages[0].Page);
        Assert.Same(doklady[1][0].Page, listy[0].Pages[1].Page);
        Assert.Same(doklady[0][1].Page, listy[1].Pages[0].Page);
        Assert.Equal(listy[0].Pages[0].OffsetX, listy[1].Pages[0].OffsetX);
    }

    [Fact]
    public void JedenDoklad_StrankyVedleSebe_ListNaDoklad()
    {
        var doklady = Doklady(2);
        var listy = SheetComposer.ComposeDle(doklady, Volby(Plan([0, 1], [null, null], jedenDoklad: true)));

        Assert.Equal(2, listy.Count);
        Assert.Equal([doklady[0][0].Page, doklady[0][1].Page], listy[0].Pages.Select(p => p.Page));
    }

    /// <summary>Jeden doklad a stejná stránka ve dvou polích = kopie pro řezání.</summary>
    [Fact]
    public void JedenDoklad_StejnaStranka_Kopie()
    {
        var doklady = Doklady(2, dveStranky: false);
        var listy = SheetComposer.ComposeDle(doklady, Volby(Plan([0, 0], [null, null], jedenDoklad: true)));

        Assert.Equal(2, listy.Count);
        Assert.All(listy[0].Pages, p => Assert.Same(doklady[0][0].Page, p.Page));
    }

    [Fact]
    public void VicDokladu_LichyPocet_PrazdnePole()
    {
        var listy = SheetComposer.ComposeDle(Doklady(3, dveStranky: false), Volby(Plan([0, 0], [null, null], jedenDoklad: false)));

        Assert.Equal(2, listy.Count);
        Assert.Single(listy[1].Pages);
    }

    /// <summary>Doklad bez 2. stránky (skrytá podmínkou): jeho pole zůstane prázdné, další sedí.</summary>
    [Fact]
    public void ChybejiciStranka_PrazdnePoleNicNeposune()
    {
        var doklady = Doklady(2);
        doklady[0] = [doklady[0][0]];
        var listy = SheetComposer.ComposeDle(doklady, Volby(Plan([0, 0], [1, 1], jedenDoklad: false)));

        Assert.Same(doklady[1][1].Page, Assert.Single(listy[1].Pages).Page);
    }

    [Theory]
    [InlineData(1, false, new int[] { 0, 0 }, new int[] { })]         // vývěsky: víc dokladů, jen 1. strana
    [InlineData(2, false, new int[] { 0, 0 }, new int[] { 1, 1 })]    // stazky: 1. strana stránky 1, 2. strana stránky 2
    [InlineData(2, true, new int[] { 0, 1 }, new int[] { })]          // jeden doklad: stránky vedle sebe
    public void Vychozi_PodleRezimu(int stranek, bool jedenDoklad, int[] strana1, int[] strana2)
    {
        var plan = UsporadaniListu.Vychozi(stranek, jedenDoklad, "A4_LANDSCAPE", 1, 2);

        Assert.Equal(strana1, plan.Predni.Select(p => p!.Value));
        Assert.Equal(strana2, plan.Zadni.Where(p => p is not null).Select(p => p!.Value));
    }

    [Fact]
    public void Json_ZpetStejne()
    {
        var plan = Plan([0, 1], [null, 1], jedenDoklad: false);

        var zpet = UsporadaniListu.FromJson(plan.ToJson())!;

        Assert.Equal(plan.Predni, zpet.Predni);
        Assert.Equal(plan.Zadni, zpet.Zadni);
        Assert.False(zpet.JedenDoklad);
    }

    [Fact]
    public void FromParamValues_PlanMaPrednost()
    {
        var values = new Dictionary<string, object?>
        {
            [SheetLayoutKeys.Sheet] = "NONE",
            [SheetLayoutKeys.Plan] = Plan([0, 0], [1, 1], jedenDoklad: false).ToJson(),
        };

        Assert.NotNull(SheetComposer.FromParamValues(values, A5)!.Plan);
    }
}
