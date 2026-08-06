using SkiaSharp;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Obrázek a čárový kód si drží mezivýsledek (dekódovaná bitmapa / nahrávka kreslení modulů),
/// aby se nepřepočítávaly při každém překreslení plátna. Testy hlídají obojí:
/// **že cache drží** (počet skutečných přepočtů) i **že nemění výsledek kreslení**
/// (odtisk vyhřáté instance == odtisk čerstvé instance).
/// </summary>
public class CachedContentRenderTests
{
    /// <summary>Malé PNG (4×2 px) jako Base64 - platná data, ať se dekódování opravdu povede.</summary>
    private static string SamplePng()
    {
        using var bitmap = new SKBitmap(4, 2, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.SetPixel(0, 0, SKColors.Red);
        bitmap.SetPixel(3, 1, SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(data.ToArray());
    }

    private static ImageElement Image(string? base64) => new()
    {
        X = 5, Y = 5, Width = 40, Height = 20,
        ImageBase64 = base64,
        ScaleMode = ImageScaleMode.Fit,
    };

    private static BarcodeElement Barcode(string content, BarcodeElementType type = BarcodeElementType.QR) => new()
    {
        X = 5, Y = 5, Width = 40, Height = 40,
        Content = content,
        BarcodeType = type,
    };

    // ── Obrázek ───────────────────────────────────────────────────────────────

    [Fact]
    public void Obrazek_DekodujeSeJenJednouProVsechnaPrekresleni()
    {
        var el = Image(SamplePng());

        for (int i = 0; i < 5; i++)
            RenderSnapshot.Capture(el, null);

        Assert.Equal(1, el.DecodeCount);
    }

    [Fact]
    public void Obrazek_ZmenaDatZnovuDekoduje()
    {
        var el = Image(SamplePng());
        RenderSnapshot.Capture(el, null);

        el.ImageBase64 = SamplePng();       // jiná instance stringu = jiný obsah z pohledu cache
        RenderSnapshot.Capture(el, null);

        Assert.Equal(2, el.DecodeCount);
    }

    [Fact]
    public void Obrazek_PoskozenaDataSeNezkousejiOpakovane()
    {
        var el = Image("tohle není base64 obrázek");

        for (int i = 0; i < 5; i++)
            RenderSnapshot.Capture(el, null);

        Assert.Equal(1, el.DecodeCount);    // neúspěch se cachuje taky
    }

    [Fact]
    public void Obrazek_CacheNemeniVysledekKresleni()
    {
        var base64 = SamplePng();

        var warm = Image(base64);
        RenderSnapshot.Capture(warm, null);                 // 1. průchod naplní cache
        string fromCache = RenderSnapshot.Capture(warm, null);

        string fresh = RenderSnapshot.Capture(Image(base64), null);

        Assert.Equal(fresh, fromCache);
    }

    [Fact]
    public void Obrazek_PomerStranJdeZTeheCache()
    {
        var el = Image(SamplePng());

        Assert.Equal(2f, el.GetImageAspectRatio());         // 4×2 px
        Assert.Equal(2f, el.GetImageAspectRatio());
        Assert.Equal(1, el.DecodeCount);
    }

    // ── Čárový kód ────────────────────────────────────────────────────────────

    [Fact]
    public void Kod_ZakodujeSeJenJednouProVsechnaPrekresleni()
    {
        var el = Barcode("EDISON-123");

        for (int i = 0; i < 5; i++)
            RenderSnapshot.Capture(el, null);

        Assert.Equal(1, el.EncodeCount);
    }

    [Theory]
    [MemberData(nameof(ZmenyKtereMusiInvalidovat))]
    public void Kod_ZmenaVstupuZnovuZakoduje(string popis, Action<BarcodeElement> zmena)
    {
        var el = Barcode("EDISON-123");
        RenderSnapshot.Capture(el, null);

        zmena(el);
        RenderSnapshot.Capture(el, null);

        Assert.True(el.EncodeCount == 2,
            $"Změna ({popis}) musí kód zakódovat znovu, ale zakódování bylo {el.EncodeCount}x.");
    }

    public static TheoryData<string, Action<BarcodeElement>> ZmenyKtereMusiInvalidovat() => new()
    {
        { "obsah",   el => el.Content = "JINY-OBSAH" },
        { "typ",     el => el.BarcodeType = BarcodeElementType.Code128 },
        { "barva",   el => el.ForeColor = "#FF0000FF" },
        { "velikost",el => el.Width = 60 },
    };

    [Fact]
    public void Kod_NezakodovatelnyObsahSeNezkousiOpakovane()
    {
        // EAN-13 chce 12-13 číslic → ZXing na tomhle obsahu vyhodí výjimku
        var el = Barcode("není-to-ean", BarcodeElementType.Ean13);

        for (int i = 0; i < 5; i++)
            RenderSnapshot.Capture(el, null);

        Assert.Equal(1, el.EncodeCount);
    }

    [Fact]
    public void Kod_CacheNemeniVysledekKresleni()
    {
        var warm = Barcode("EDISON-123");
        RenderSnapshot.Capture(warm, null);                 // 1. průchod naplní cache
        string fromCache = RenderSnapshot.Capture(warm, null);

        string fresh = RenderSnapshot.Capture(Barcode("EDISON-123"), null);

        Assert.Equal(fresh, fromCache);
    }

    /// <summary>Odtisk kódu se nesmí lišit podle toho, kolikrát se překreslil ani po změně a vrácení obsahu.</summary>
    [Fact]
    public void Kod_ZmenaAVratkaObsahuVratiTentyzOdtisk()
    {
        var el = Barcode("EDISON-123");
        string original = RenderSnapshot.Capture(el, null);

        el.Content = "JINY-OBSAH";
        RenderSnapshot.Capture(el, null);
        el.Content = "EDISON-123";

        Assert.Equal(original, RenderSnapshot.Capture(el, null));
    }
}
