using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Prázdný přípojný řádek nesmí zabírat místo (tiket `#11300`, KORID).
/// </summary>
/// <remarks>
/// Zákazník má uzly s celotýdenním provozem, ale přípoji jen v pracovní dny; ve starém tisku
/// zůstal ve víkendové tabulce prázdný přípojný řádek a bral místo. V tiketu má od nás slíbeno,
/// že se to vyřeší s novým tiskovým modulem.
///
/// <para>U nás to **nemůže nastat konstrukcí**: přípojný řádek se negeneruje pro zastávku, ale
/// pro **každý přípoj nesených spojů**. Kde přípoj není, není co vykreslit. Testy to drží jako
/// doklad k tiketu - je to vlastnost, kterou by šlo příští úpravou výpočtu výšky nevědomky
/// zrušit (např. „rezervovat místo na přípoje u každé zastávky").</para>
/// </remarks>
public class PrazdnyPripojnyRadekTests
{
    private const float VyskaRadku = 5f;

    /// <summary>Tři zastávky, dva spoje; `sPripojem` = spoj, který nese přípoj u prostřední z nich.</summary>
    private static DataContext Linka(bool sPripojem)
    {
        var prvni = new List<(string, object?)> { ("CISLO_SPOJE", "2"), ("SKUPINA", "1") };
        if (sPripojem)
            prvni.Add(("PRIPOJE", RenderSnapshot.Nested(
                [[("TYP", "vycka"), ("PORADI", "2"), ("RADEK", "1"),
                  ("CAS", "10:25"), ("LINKA", "330520"), ("TEXT", "Kaplice")]])));

        return RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "Český Krumlov"), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "Kaplice, rozcestí"), ("PORADI", "2")),
                RenderSnapshot.Row(("ID", "3"), ("NAZEV_ZASTAVKY", "České Budějovice"), ("PORADI", "3")),
            ]),
            ("SPOJE",
            [
                RenderSnapshot.Row([.. prvni]),
                RenderSnapshot.Row(("CISLO_SPOJE", "4"), ("SKUPINA", "1")),
            ]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]));
    }

    private static LineTableElement Prvek() => new()
    {
        X = 0, Y = 0, Width = 190,
        DataRowHeight = VyskaRadku,
        ShowPripojeRows = true,
        ShowNzRow = false,
        ShowPlatnostRow = false,
        ShowGroupHeaders = false,
    };

    [Fact]
    public void BezPripoju_ZadnyRadekNevznikne()
    {
        // ⚠ Měří se **rozdíl výšky**, ne absolutní číslo: výška nese i záhlaví a ta se může
        // legitimně změnit. Rozdíl musí být přesně jeden přípojný řádek.
        float sPripojem = Prvek().CalculateHeightFromData(Linka(sPripojem: true));
        float bez       = Prvek().CalculateHeightFromData(Linka(sPripojem: false));

        Assert.Equal(VyskaRadku, sPripojem - bez, precision: 2);
    }

    [Fact]
    public void PripojnyRadekPatriSpoji_NeZastavce()
    {
        // Kdyby se místo rezervovalo per zastávka, přibyly by řádky tři (máme tři zastávky),
        // ne jeden - to je přesně chování, kvůli kterému tiket vznikl.
        float sPripojem = Prvek().CalculateHeightFromData(Linka(sPripojem: true));
        float bez       = Prvek().CalculateHeightFromData(Linka(sPripojem: false));

        Assert.True(sPripojem - bez < VyskaRadku * 2,
            $"přibylo víc než jeden přípojný řádek: {sPripojem - bez:0.##} mm");
    }

    [Fact]
    public void VypnutePripojneRadky_VyskuNemeni()
    {
        // Doklad, že přepínač na výpočet opravdu dosáhne - jinak by první dva testy měřily něco jiného.
        var prvek = Prvek();
        prvek.ShowPripojeRows = false;

        Assert.Equal(prvek.CalculateHeightFromData(Linka(sPripojem: false)),
                     prvek.CalculateHeightFromData(Linka(sPripojem: true)), precision: 2);
    }
}
