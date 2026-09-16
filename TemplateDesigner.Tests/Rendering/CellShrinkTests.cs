using TemplateDesigner.Models;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// KOVED #13048 bod 2 - vestavěné zmenšení přetékajícího textu buňky
/// (<see cref="CellRenderer.ComputeShrinkScale(ResolvedTextStyle, string[], float, float, float, float, float)"/>).
/// Krátký text se nemění; dlouhý se zmenší tak, aby se vešel do šířky minus rezerva; obsah kreslený
/// za textem (značky zastávky) zmenšuje cílovou šířku dál.
/// </summary>
public class CellShrinkTests
{
    private static readonly ResolvedTextStyle Styl = CellDefaults.Default;
    private const float MmToPx = 96f / 25.4f;

    private static float Scale(string text, float widthMm, float trailingMm = 0f) =>
        CellRenderer.ComputeShrinkScale(
            Styl, [text], widthMm, MmToPx,
            fontScale: 1f, shrinkReserveMm: CellRenderer.DefaultShrinkReserveMm, trailingWidthMm: trailingMm);

    [Fact]
    public void KratkyText_SeNezmensuje()
    {
        Assert.Equal(1f, Scale("Dasný", widthMm: 40f));
    }

    [Fact]
    public void PrazdnyText_SeNezmensuje()
    {
        Assert.Equal(1f, Scale("", widthMm: 5f));
    }

    [Fact]
    public void DlouhyText_SeZmensiNaSirkuMinusRezervu()
    {
        const string text = "České Budějovice,U Zelené ratolesti rozc. 1.0";
        const float widthMm = 45f;
        float scale = Scale(text, widthMm);
        Assert.True(scale < 1f, $"dlouhý text se má zmenšit (scale={scale})");

        // Zmenšený text se skutečně vejde do šířky minus rezerva.
        using var font = Styl.CreateFont(scale, MmToPx);
        float cilPx = (widthMm - CellRenderer.DefaultShrinkReserveMm) * MmToPx;
        Assert.True(font.MeasureText(text) <= cilPx + 0.5f, "zmenšený text se má vejít do cílové šířky");
    }

    [Fact]
    public void ObsahZaTextem_ZmensujeVic()
    {
        const string text = "České Budějovice,U Zelené ratolesti rozc. 1.0";
        float bezZnacek = Scale(text, widthMm: 45f);
        float seZnackami = Scale(text, widthMm: 45f, trailingMm: 8f);
        Assert.True(seZnackami < bezZnacek, "značky za textem mají zmenšit víc (text jim uhýbá)");
    }

    // ── `B162`: dolní mez zmenšování ─────────────────────────────────────────
    // Bez ní se z písmene v úzkém sloupci stala tečka (variantní sloupce jízdních dob, 0330513).

    [Fact]
    public void UzkaBunka_NezmensiPodPolovinu()
    {
        float scale = Scale("České Budějovice,U Zelené ratolesti rozc. 1.0", widthMm: 6f);
        Assert.Equal(CellRenderer.MinScale, scale, precision: 3);
    }

    [Fact]
    public void DrobnePismo_NezmensiPodCtyriBody()
    {
        var drobny = CellDefaults.Default with { FontSize = 6f };
        float scale = CellRenderer.ComputeShrinkScale(
            drobny, ["České Budějovice,U Zelené ratolesti"], 6f, MmToPx,
            fontScale: 1f, shrinkReserveMm: CellRenderer.DefaultShrinkReserveMm);
        Assert.Equal(CellRenderer.MinFontPt, drobny.FontSize * scale, precision: 3);
    }

    [Fact]
    public void JizZmensenyPrvek_MaMezPomernouKeSveSkale()
    {
        // Prvek už zmenšil písmo na 60 % (nevešel se na výšku). Polovina z toho by byla 2,7 bodu,
        // takže rozhoduje absolutní dno - výsledek jsou 4 body, ne polovina zmenšené velikosti.
        float scale = CellRenderer.ComputeShrinkScale(
            Styl, ["České Budějovice,U Zelené ratolesti rozc. 1.0"], 6f, MmToPx,
            fontScale: 0.6f, shrinkReserveMm: CellRenderer.DefaultShrinkReserveMm);
        Assert.Equal(CellRenderer.MinFontPt, Styl.FontSize * scale, precision: 3);
    }

    [Fact]
    public void KratkyText_MezNicNepokazi()
    {
        Assert.Equal(1f, Scale("Dasný", widthMm: 40f));
    }
}
