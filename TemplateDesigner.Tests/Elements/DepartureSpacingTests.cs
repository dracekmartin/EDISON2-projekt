using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Rozestup odjezdů v mřížce minut (#12843, TRANSDEV). Ve výchozím režimu `ByContent` je slot široký
/// podle svého obsahu, takže minuta se značkou odsune odjezdy za sebou a mřížka se roztřepí.
/// V `Even` se jede podle jednoho rastru, takže značka nerozhodí zarovnání ostatních.
///
/// Testy drží hlavně to, co je na tom netriviální: rastr se počítá **za celou skupinu dnů**, ne za
/// řádek hodiny - kdyby se bralo maximum jen za řádek, hodina se značkami by měla jiný rozestup než
/// hodina bez nich a čísla by nestála pod sebou (což je přesně ta vada, kterou zákazník hlásil).
/// </summary>
public class DepartureSpacingTests
{
    // ── Rastr (maximum přes skupinu, nominální šířka jako spodní mez) ──────────

    [Fact]
    public void Rastr_JeNejsirsiOdjezdSkupiny()
    {
        Assert.Equal(8.5f, DepartureGridElement.UniformSlotWidth([5f, 8.5f, 6f], nominalMm: 5f));
    }

    /// <summary>Nominální šířka minuty je spodní mez - bez značek se rastr nesmí zúžit pod ni.</summary>
    [Fact]
    public void Rastr_NikdyNeniUzsiNezNominalniSirka()
    {
        Assert.Equal(5f, DepartureGridElement.UniformSlotWidth([4f, 3.2f], nominalMm: 5f));
        Assert.Equal(5f, DepartureGridElement.UniformSlotWidth([], nominalMm: 5f));
    }

    /// <summary>Skupina s hodinou bez značek i hodinou se značkami má **jeden** rastr - to je ten
    /// důvod, proč se maximum bere přes celou skupinu.</summary>
    [Fact]
    public void Rastr_MichaVsechnyHodinySkupiny()
    {
        float[] hodinaBezZnacek = [5f, 5f, 5f];
        float[] hodinaSeZnackami = [5f, 6.8f, 5f];

        float rastr = DepartureGridElement.UniformSlotWidth(
            [.. hodinaBezZnacek, .. hodinaSeZnackami], nominalMm: 5f);

        Assert.Equal(6.8f, rastr);
    }

    // ── Uplatnění rastru na řádek ─────────────────────────────────────────────

    [Fact]
    public void Rovnomerny_SrovnaVsechnySlotyNaRastr()
    {
        var slots = DepartureGridElement.ApplySpacingMode(
            [5f, 6.8f, 5f], DepartureSpacing.Even, uniformSlotMm: 6.8f);

        Assert.Equal([6.8f, 6.8f, 6.8f], slots);
    }

    /// <summary>Hodina bez značek dostane týž rastr jako hodina se značkami - jinak by čísla
    /// nestála pod sebou.</summary>
    [Fact]
    public void Rovnomerny_PlatiIProRadekBezZnacek()
    {
        var slots = DepartureGridElement.ApplySpacingMode(
            [5f, 5f, 5f], DepartureSpacing.Even, uniformSlotMm: 6.8f);

        Assert.Equal([6.8f, 6.8f, 6.8f], slots);
    }

    [Fact]
    public void PodleObsahu_NechaSlotyBezeZmeny()
    {
        var puvodni = new List<float> { 5f, 6.8f, 5f };

        var slots = DepartureGridElement.ApplySpacingMode(puvodni, DepartureSpacing.ByContent, 6.8f);

        Assert.Equal(puvodni, slots);
    }

    /// <summary>Bez rastru (nespočítaný / nulový) se nic nesrovnává - pojistka proti tomu, aby
    /// chybějící předpočet zúžil všechny slotry na nulu.</summary>
    [Fact]
    public void Rovnomerny_BezRastru_NechaSlotyBezeZmeny()
    {
        var puvodni = new List<float> { 5f, 6.8f };

        Assert.Equal(puvodni, DepartureGridElement.ApplySpacingMode(puvodni, DepartureSpacing.Even, 0f));
    }

    // ── Pevný rastr (`B160`) ──────────────────────────────────────────────────
    // Rozteč je nastavená šířka minuty a značky do ní nemluví; co se za minutu nevejde, se zmenší.

    [Fact]
    public void PevnyRastr_SrovnaSlotyNaNastavenouSirku()
    {
        var slots = DepartureGridElement.ApplySpacingMode(
            [5f, 6.8f, 5f], DepartureSpacing.Fixed, uniformSlotMm: 5f);

        Assert.Equal([5f, 5f, 5f], slots);
    }

    /// <summary>V rozteči je místo na jednu značku, i když je nastavená Šířka minuty menší -
    /// jinak na značku zbyde jen to, co nedoberou číslice, a zmenšení ji srazí na mez čitelnosti.</summary>
    [Fact]
    public void PevnyRastr_RezervujeMistoNaJednuZnacku()
    {
        float rastr = DepartureGridElement.PevnyRastr(
            nominalMm: 5f, minutaMm: 3.2f, mezeraMm: 0.3f, jednaZnackaMm: 1.8f);

        Assert.Equal(5.3f, rastr, precision: 3);
    }

    /// <summary>Nastavená šířka je spodní mez - širší rozteč si uživatel může vynutit.</summary>
    [Fact]
    public void PevnyRastr_NastavenaSirkaJeSpodniMez()
    {
        float rastr = DepartureGridElement.PevnyRastr(
            nominalMm: 10f, minutaMm: 3.2f, mezeraMm: 0.3f, jednaZnackaMm: 1.8f);

        Assert.Equal(10f, rastr, precision: 3);
    }

    /// <summary>Bez značek se nic nerezervuje - vývěska bez nich zůstane tak hustá, jak je nastavená.</summary>
    [Fact]
    public void PevnyRastr_BezZnacek_JeNastavenaSirka()
    {
        Assert.Equal(5f, DepartureGridElement.PevnyRastr(5f, 3.2f, 0.3f, 0f), precision: 3);
    }

    [Fact]
    public void ZnackyKtereSeVejdou_SeNezmensuji()
    {
        float scale = DepartureGridElement.ZmenseniZnacek(
            rastrMm: 5f, minutaMm: 3f, znackyMm: 1.5f, dataScale: 1f, Styl(8f));

        Assert.Equal(1f, scale);
    }

    [Fact]
    public void ZnackyKtereSeNevejdou_SeZmensiNaVolneMisto()
    {
        // Za minutou zbývají 2 mm, značky chtějí 4 → poloviční velikost.
        float scale = DepartureGridElement.ZmenseniZnacek(
            rastrMm: 5f, minutaMm: 3f, znackyMm: 4f, dataScale: 1f, Styl(8f));

        Assert.Equal(0.5f, scale, precision: 3);
    }

    /// <summary>Zmenšování má tutéž mez jako jinde v aplikaci (`B162`) - pod ní by ze značky byla tečka.</summary>
    [Fact]
    public void ZnackyNezmenseneDoNecitelna()
    {
        float scale = DepartureGridElement.ZmenseniZnacek(
            rastrMm: 5f, minutaMm: 4.5f, znackyMm: 20f, dataScale: 1f, Styl(8f));

        Assert.Equal(TemplateDesigner.Rendering.CellRenderer.MinShrinkScale(Styl(8f), 1f), scale, precision: 3);
        Assert.True(8f * scale >= TemplateDesigner.Rendering.CellRenderer.MinFontPt, "značka nesmí klesnout pod 4 body");
    }

    /// <summary>Prvek, který už zmenšil písmo, počítá poměr ze své škály. Polovina z 6,4 bodu by
    /// byla 3,2, takže rozhodne absolutní dno a vyjdou 4 body.</summary>
    [Fact]
    public void JizZmensenyPrvek_PocitaZeSveSkaly()
    {
        float scale = DepartureGridElement.ZmenseniZnacek(
            rastrMm: 5f, minutaMm: 3f, znackyMm: 4f, dataScale: 0.8f, Styl(8f));

        Assert.Equal(TemplateDesigner.Rendering.CellRenderer.MinFontPt, 8f * scale, precision: 3);
    }

    private static ResolvedTextStyle Styl(float velikostPt) =>
        TemplateDesigner.Rendering.CellDefaults.Default with { FontSize = velikostPt };

    [Fact]
    public void Klonovani_NeseRezimDal()
    {
        var klon = (DepartureGridElement)new DepartureGridElement
        {
            MinuteSpacing = DepartureSpacing.Even,
        }.Clone();

        Assert.Equal(DepartureSpacing.Even, klon.MinuteSpacing);
    }
}
