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
