using System.Linq;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Vodorovné přizpůsobení tabulky (L‑1): dokud se počítala jen výšková škála, tabulka širší než
/// element se **oříznula** - a protože varování o přetečení hlídá jen svislý směr, zmizely spoje
/// z pravé strany bez jakéhokoli signálu. Na šířku reagovalo pouze zmenšování textu v jednotlivé
/// buňce, které si každá buňka počítá sama → vedle sebe různé velikosti písma.
///
/// Testy hlídají obojí: že se tabulka po zmenšení do šířky **vejde přesně**, a že se u tabulky,
/// která se vejde, nezmění vůbec nic (behavior-preserving pro uložené šablony).
/// </summary>
public class TableWidthFitTests
{
    private static TableLayout.ColWidth Fixed(float mm) => new(mm);
    private static TableLayout.ColWidth Auto() => new(null);

    // ── Vejde se → nic se nemění ─────────────────────────────────────────────

    [Fact]
    public void VejdeSe_ZadneZmenseni()
    {
        var cols = new[] { Fixed(30f), Fixed(20f), Auto() };

        Assert.Equal(1f, TableLayout.ComputeWidthScale(cols, 100f));
    }

    [Fact]
    public void VejdeSe_AutoSloupceSiDeliZbytek()
    {
        var widths = TableLayout.ResolveWidths([Fixed(40f), Auto(), Auto()], 100f);

        Assert.Equal([40f, 30f, 30f], widths);
    }

    [Fact]
    public void PresneNaSirku_SeNezmensuje()
    {
        // Hranice: součet == šířka elementu je ještě „vejde se"
        Assert.Equal(1f, TableLayout.ComputeWidthScale([Fixed(60f), Fixed(40f)], 100f));
    }

    // ── Nevejde se → zmenší se na šířku elementu ─────────────────────────────

    [Fact]
    public void PevneSirkyPresahuji_ZmensiSeNaSirkuElementu()
    {
        var cols = new[] { Fixed(80f), Fixed(70f) };   // 150 mm do 100 mm

        float scale = TableLayout.ComputeWidthScale(cols, 100f);
        var widths = TableLayout.ResolveWidths(cols, 100f);

        Assert.Equal(100f / 150f, scale, 4);
        Assert.Equal(100f, widths.Sum(), 3);   // dosedne přesně, nic se neoříznulo
    }

    [Fact]
    public void MnohoAutoSloupcuNaMinimu_ZmensiSeNaSirkuElementu()
    {
        // 60 spojů × minimum 3 mm = 180 mm + 60 mm popisových = 240 mm do 190 mm
        var cols = new[] { Fixed(60f) }.Concat(Enumerable.Repeat(Auto(), 60)).ToList();

        float scale = TableLayout.ComputeWidthScale(cols, 190f, minAutoWidthMm: 3f);
        var widths = TableLayout.ResolveWidths(cols, 190f, minAutoWidthMm: 3f);

        Assert.Equal(190f / 240f, scale, 4);
        Assert.Equal(190f, widths.Sum(), 3);
        Assert.All(widths.Skip(1), w => Assert.Equal(3f * scale, w, 4));   // jednotné, ne nulové
    }

    [Fact]
    public void MinimumAutoSloupcu_NezasahujeKdyzJeMisto()
    {
        // 10 auto sloupců na 190 mm = 19 mm each, minimum 3 mm se neuplatní
        var cols = Enumerable.Repeat(Auto(), 10).ToList();

        Assert.Equal(1f, TableLayout.ComputeWidthScale(cols, 190f, minAutoWidthMm: 3f));
        Assert.All(TableLayout.ResolveWidths(cols, 190f, minAutoWidthMm: 3f), w => Assert.Equal(19f, w, 4));
    }

    // ── Skalární varianta (element si šířky počítá sám - LineTable) ───────────

    [Fact]
    public void SkalarniVarianta_PomerPozadovaneAdostupne()
    {
        Assert.Equal(0.5f, TableLayout.ComputeWidthScale(240f, 120f), 4);
        Assert.Equal(1f, TableLayout.ComputeWidthScale(100f, 120f));
    }

    [Fact]
    public void DegenerovaneVstupy_NeshodiSeANezmensuji()
    {
        Assert.Equal(1f, TableLayout.ComputeWidthScale(0f, 100f));
        Assert.Equal(1f, TableLayout.ComputeWidthScale(100f, 0f));
        Assert.Equal(1f, TableLayout.ComputeWidthScale([], 0f));
    }

    // ── Procentní šířky ──────────────────────────────────────────────────────

    [Fact]
    public void ProcentniSirkyPresahuji_ZmensiSeTaky()
    {
        // 80 % + 60 % = 140 % šířky elementu
        var cols = new[]
        {
            new TableLayout.ColWidth(80f, ColumnWidthMode.Percent),
            new TableLayout.ColWidth(60f, ColumnWidthMode.Percent),
        };

        var widths = TableLayout.ResolveWidths(cols, 100f);

        Assert.Equal(100f, widths.Sum(), 3);
    }
}
