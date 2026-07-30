using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Časová osa mřížky odjezdů. Meze „Hodiny od/do" patří **jen** režimu Vlastní rozsah - dřív
/// zužovaly i ostatní režimy, a protože jsou v nich schované (podmínka viditelnosti
/// „CASOVA_OSA=Vlastní rozsah"), zadaný rozsah po přepnutí režimu **tiše přetrvával** a nebylo ho
/// jak zrušit. Výchozí „od" = 4 navíc zahazovalo hodiny 0-3 i bez zásahu uživatele.
/// </summary>
public class DepartureTimeAxisTests
{
    // Provozní hodiny s odjezdy: 3 (noční), 5, 8, 14, 22 + extended 25 (= 01:00 dalšího dne)
    private static HashSet<int> Hours() => [3, 5, 8, 14, 22, 25];

    [Fact]
    public void JenHodinySOdjezdy_NezuzujeSeMezemiVlastnihoRozsahu()
    {
        // Meze 6..10 zůstaly v parametrech po přepnutí režimu - nesmí nic uříznout.
        var hours = DepartureGridElement.GetVisibleHoursFromSet(Hours(), TimeAxisMode.Compact, 6, 10);

        Assert.Equal([3, 5, 8, 14, 22, 25], hours);
    }

    [Fact]
    public void JenHodinySOdjezdy_ZachovaHodinyPod4PriVychozimNastaveni()
    {
        // Výchozí od=4, do=23 (default agendy) - hodina 3 se dřív tiše zahodila.
        var hours = DepartureGridElement.GetVisibleHoursFromSet(Hours(), TimeAxisMode.Compact, 4, 23);

        Assert.Contains(3, hours);
        Assert.Contains(25, hours);   // extended hour (nad 23) taky ne
    }

    [Fact]
    public void SouvislaOdPrvniDoPosledni_NezuzujeSeMezemi()
    {
        var hours = DepartureGridElement.GetVisibleHoursFromSet(Hours(), TimeAxisMode.DataBounded, 6, 10);

        Assert.Equal(3, hours[0]);
        Assert.Equal(25, hours[^1]);
    }

    [Fact]
    public void CelychDvacetCtyri_NezuzujeSeMezemi()
    {
        var hours = DepartureGridElement.GetVisibleHoursFromSet(Hours(), TimeAxisMode.Full, 6, 10);

        Assert.Equal(24, hours.Count);
        Assert.Equal(0, hours[0]);
    }

    // ── Vlastní rozsah - meze platí (beze změny) ─────────────────────────────

    [Fact]
    public void VlastniRozsah_VygenerujePresneZadaneMeze()
    {
        var hours = DepartureGridElement.GetVisibleHoursFromSet(Hours(), TimeAxisMode.Range, 6, 10);

        Assert.Equal([6, 7, 8, 9, 10], hours);
    }

    [Fact]
    public void VlastniRozsah_PresPulnocPokracujeExtendedHodinami()
    {
        // 22 → 3 se reprezentuje jako 22, 23, 24, 25, 26, 27 (raw provozní hodiny)
        var hours = DepartureGridElement.GetVisibleHoursFromSet(Hours(), TimeAxisMode.Range, 22, 3);

        Assert.Equal([22, 23, 24, 25, 26, 27], hours);
    }
}
