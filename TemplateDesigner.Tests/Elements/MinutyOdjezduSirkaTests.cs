using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// `B194a` C12: Minuty odjezdů hlídají šířku jako výšku. Do 26. 10. se šířka skupiny počítala jen
/// z počtu minut - skupina s hodně značkami (zvláštní dny kolem Vánoc) vyšla užší, než co se do ní
/// nakreslilo, a minuty se uřízly; čtyři skupiny se do prvku nevešly a nic se nezmenšilo ani nehlásilo
/// (KV_DP linka 17, výchozí vývěska MHD). Teď: chybějící místo se vezme skupinám s rezervou, když to
/// nestačí, zmenší se celá mřížka (a řekne se to), a pod mezí zmenšení se ohlásí ořez.
/// </summary>
public class MinutyOdjezduSirkaTests
{
    /// <summary>Skupina 1 má v hodině dvě minuty bez značek, skupina 2 šest minut se dvěma značkami.</summary>
    private static DataContext Data()
    {
        var odjezdy = new List<DataContext>
        {
            RenderSnapshot.Row(("SKUPINA_DNU_ID", "1"), ("HODINA", "6"), ("MINUTA", "15")),
            RenderSnapshot.Row(("SKUPINA_DNU_ID", "1"), ("HODINA", "6"), ("MINUTA", "45")),
        };
        foreach (int minuta in new[] { 0, 10, 20, 30, 40, 50 })
            odjezdy.Add(RenderSnapshot.Row(("SKUPINA_DNU_ID", "2"), ("HODINA", "6"), ("MINUTA", minuta.ToString()),
                ("ANOTACE", RenderSnapshot.Nested([("TYP", "nz"), ("ZNAK", "10")], [("TYP", "nz"), ("ZNAK", "12")]))));

        return RenderSnapshot.Data(
            ("SKUPINY_DNU",
            [
                RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "2"), ("NADPIS", "Svátky"), ("PORADI", "2")),
            ]),
            ("ODJEZDY", odjezdy));
    }

    private static DepartureGridElement Prvek(float sirka) => new()
    {
        X = 0, Y = 0, Width = sirka, Height = 60,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 7 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 8 },
        ShowSkupinaDnuHeader = true,
    };

    private static Template Sablona(TemplateElement prvek)
    {
        var sekce = new Section { Name = "Obsah" };
        sekce.Elements.Add(prvek);
        return new Template { Sections = [sekce] };
    }

    /// <summary>Šířka tak akorát: celek se vejde, ale rozdělení podle počtu minut (do 26. 10.) by
    /// skupině se značkami dalo méně, než potřebuje - místo se musí vzít skupině s rezervou.</summary>
    [Fact]
    public void SkupinaSeZnackami_DostaneMistoNaSveMinuty()
    {
        var data = Data();
        var (sirkyVolne, potreby, _) = Prvek(400).RozvrzeniSirek(data);
        float hodinyAOddelovace = 400f - sirkyVolne.Sum();
        var prvek = Prvek(hodinyAOddelovace + potreby.Sum() + 1f);

        var (sirky, potrebyTesne, meritko) = prvek.RozvrzeniSirek(data);

        Assert.Equal(1f, meritko);
        Assert.True(sirky[1] >= potrebyTesne[1] - 0.01f, $"skupina se značkami {sirky[1]:0.0} mm, potřebuje {potrebyTesne[1]:0.0} mm");
        Assert.True(sirky[0] >= potrebyTesne[0] - 0.01f, "ani skupina, která místo dala, nesmí jít pod svou potřebu");
    }

    /// <summary>Vejde-li se všechno, nic se nezmenší ani nehlásí. (Že se šířky skupin takové vývěsky
    /// nehnou, doložilo porovnání pixelů CB_DP 11350 před opravou a po ní - changelog 2026-10-26.)</summary>
    [Fact]
    public void KdyzSeVejde_NicSeNezmensi()
    {
        var data = RenderSnapshot.Data(
            ("SKUPINY_DNU",
            [
                RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "2"), ("NADPIS", "Sobota"), ("PORADI", "2")),
            ]),
            ("ODJEZDY",
            [
                RenderSnapshot.Row(("SKUPINA_DNU_ID", "1"), ("HODINA", "6"), ("MINUTA", "15")),
                RenderSnapshot.Row(("SKUPINA_DNU_ID", "1"), ("HODINA", "6"), ("MINUTA", "45")),
                RenderSnapshot.Row(("SKUPINA_DNU_ID", "2"), ("HODINA", "6"), ("MINUTA", "40")),
            ]));

        var prvek = Prvek(120);
        var (sirky, potreby, meritko) = prvek.RozvrzeniSirek(data);

        Assert.Equal(1f, meritko);
        Assert.True(sirky.Zip(potreby).All(p => p.First >= p.Second - 0.01f));
        Assert.Empty(ContentShrinkWarnings.Seznam(Sablona(prvek), [data]));
    }

    [Fact]
    public void KdyzSeNevejde_ZmensiSeVseAHlasiSe()
    {
        var prvek = Prvek(60);
        var data = Data();
        var (sirky, potreby, meritko) = prvek.RozvrzeniSirek(data);

        Assert.InRange(meritko, 0.5f, 0.99f);
        Assert.True(sirky.Zip(potreby).All(p => p.First >= p.Second - 0.01f), "každá skupina má po zmenšení místo na své minuty");
        Assert.Equal(meritko, prvek.ZmenseniPisma(data), 3);
        var hlaska = Assert.Single(ContentShrinkWarnings.Seznam(Sablona(prvek), [data]));
        Assert.Equal(ZavaznostHlaseni.Informace, hlaska.Zavaznost);
        Assert.Contains("písmo zmenšeno", hlaska.Co);
    }

    [Fact]
    public void AniPoZmenseniSeNevejde_HlasiOrez()
    {
        var prvek = Prvek(25);
        var data = Data();

        Assert.True(prvek.NevejdeSeNaSirku(data));
        var hlaska = Assert.Single(ContentShrinkWarnings.Seznam(Sablona(prvek), [data], vcetneOrezuTextu: false));
        Assert.Equal(ZavaznostHlaseni.Chyba, hlaska.Zavaznost);
        Assert.Contains("nevejdou", hlaska.Co);
    }

    [Fact]
    public void DynamickaVyska_SeZmensiSeSirkou()
    {
        var siroky = Prvek(150);
        var uzky = Prvek(60);
        siroky.Height = null;
        uzky.Height = null;
        var data = Data();

        Assert.True(uzky.CalculateHeightFromData(data) < siroky.CalculateHeightFromData(data));
        Assert.Contains(ContentShrinkWarnings.Seznam(Sablona(uzky), [data]), h => h.Co.Contains("písmo zmenšeno"));
    }

    /// <summary>Dvě skupiny dnů se stejným ID (ukázková data šablony MHD) - výpočet šířek z 26. 10. na nich
    /// spadl při otevření šablony, dosavadní kód je snášel.</summary>
    [Fact]
    public void SkupinySeStejnymId_Nespadne()
    {
        var data = RenderSnapshot.Data(
            ("SKUPINY_DNU",
            [
                RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "2")),
            ]),
            ("ODJEZDY", [RenderSnapshot.Row(("SKUPINA_DNU_ID", "1"), ("HODINA", "6"), ("MINUTA", "15"))]));

        Assert.Null(Record.Exception(() => Prvek(120).RozvrzeniSirek(data)));
        Assert.Null(Record.Exception(() => RenderSnapshot.Capture(Prvek(120), data)));
    }

    [Fact]
    public void Kresleni_Nespadne_AniVZuzenemPrvku()
    {
        foreach (float sirka in new[] { 150f, 60f, 25f })
            Assert.Null(Record.Exception(() => RenderSnapshot.Capture(Prvek(sirka), Data())));
    }
}
