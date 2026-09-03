using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Čitelnost QR kódu na malém prvku.
/// </summary>
/// <remarks>
/// Zákazník vyfotil QR, které vypadalo „zdrcnuté" a nešlo přečíst (#13158). Dvě příčiny, obě
/// v kreslení, ne ve velikosti prvku:
/// 1. Kód se kódoval v <b>cílových pixelech</b>, ne v modulech. ZXing škáluje jen celým násobkem
///    a zbytek doplní vycentrovaným okrajem, takže z 33 modulů na 60 px vyšel kód přes 55 % prvku.
/// 2. Ke každému modulu se přidávalo pevných 0,5 px „proti mezerám" - u modulu širokého 1 px je to
///    50 % rozlití a moduly se slily.
/// </remarks>
public class QrCitelnostTests
{
    /// <summary>URL délky, jakou má reálná vývěska - 33 modulů.</summary>
    private const string Url = "https://www.idsbbsk.sk/cestovne-poriadky/linka/108360?zastavka=28004";

    private static BarcodeElement Qr(float mm) => new()
    {
        X = 0, Y = 0, Width = mm, Height = mm,
        Content = Url,
        BarcodeType = BarcodeElementType.QR,
        ForeColor = "#FF000000",
    };

    /// <summary>Vykreslí prvek na bílé pozadí v daném rozlišení a vrátí bitmapu.</summary>
    private static SKBitmap Vykresli(BarcodeElement el, float mmToPx)
    {
        int px = (int)MathF.Ceiling(el.Width * mmToPx) + 4;
        var bitmap = new SKBitmap(px, px, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            el.Render(new RenderContext(canvas, mmToPx));
        }
        return bitmap;
    }

    /// <summary>Podíl černých pixelů v ploše kódu.</summary>
    private static float PomerCerne(SKBitmap bitmap, int stranaPx)
    {
        int cerne = 0, celkem = 0;
        for (int y = 0; y < stranaPx; y++)
            for (int x = 0; x < stranaPx; x++)
            {
                celkem++;
                if (bitmap.GetPixel(x, y).Red < 128) cerne++;
            }
        return celkem == 0 ? 0f : (float)cerne / celkem;
    }

    [Fact]
    public void MalyKod_SeNesleje()
    {
        // Slité moduly = skoro celá plocha černá. Zdravý QR má černých řádově 40 %.
        using var bitmap = Vykresli(Qr(15f), 4f);   // 15 mm při 4 px/mm = 60 px, tedy případ z fotky
        float pomer = PomerCerne(bitmap, 60);

        Assert.InRange(pomer, 0.25f, 0.65f);
    }

    [Fact]
    public void KodVyplniCelyPrvek_NeJenJehoStred()
    {
        // Kódování v pixelech dávalo vycentrovaný okraj, takže kód zabíral jen část prvku.
        // QR má v rozích zaměřovací značky, takže při plném vyplnění se hrany dotýká.
        using var bitmap = Vykresli(Qr(15f), 4f);

        bool cerneUHrany = false;
        for (int i = 0; i < 60; i++)
            if (bitmap.GetPixel(i, 0).Red < 128 || bitmap.GetPixel(0, i).Red < 128) cerneUHrany = true;

        Assert.True(cerneUHrany, "kód nesahá k hraně prvku - zůstal vycentrovaný okraj");
    }

    [Theory]
    [InlineData(2f)]
    [InlineData(4f)]
    [InlineData(12f)]
    public void KodJeCitelnyVKazdemRozliseni(float mmToPx)
    {
        // Náhled, obrazovka i tisk kreslí týž prvek jiným rozlišením - poměr černé musí zůstat
        // v rozumném pásmu, jinak se moduly buď slévají, nebo zanikají.
        var el = Qr(20f);
        int strana = (int)(20f * mmToPx);
        using var bitmap = Vykresli(el, mmToPx);

        Assert.InRange(PomerCerne(bitmap, strana), 0.25f, 0.65f);
    }

    // ── Doporučená velikost ─────────────────────────────────────────────────
    // Kolega se ptal přímo: „jak uživatelsky poznám, co je dostatečná velikost QR?" Z aplikace to
    // poznat nešlo - kód se vykreslil vždy, jen nebyl čitelný.

    [Fact]
    public void DoporucenaVelikostRostesDelkouObsahu()
    {
        var el = Qr(20f);
        float kratka = el.DoporucenaMinimalniSirkaMm("https://a.cz/1");
        float dlouha = el.DoporucenaMinimalniSirkaMm(Url);

        Assert.True(kratka > 0f, "krátká URL musí dát doporučení");
        Assert.True(dlouha > kratka, $"dlouhá URL potřebuje víc místa ({dlouha:0.#} vs {kratka:0.#} mm)");
    }

    [Fact]
    public void MalyKodSRealnouUrl_DostaneVarovaniSKonkretnimCislem()
    {
        // 10 mm je velikost z fotky zákazníka - 33 modulů se do ní nevejde čitelně.
        var hlaska = BarcodeSizeWarnings.Summarize(Sablona(Qr(10f)), null);

        Assert.NotNull(hlaska);
        Assert.Contains("QR kód", hlaska);
        Assert.Contains("mm", hlaska);
    }

    [Fact]
    public void DostatecneVelkyKod_Nevaruje()
    {
        Assert.Null(BarcodeSizeWarnings.Summarize(Sablona(Qr(30f)), null));
    }

    [Fact]
    public void VarovaniBereKratsiStranu()
    {
        // Široký a nízký prvek: QR je omezené výškou, ne šířkou.
        var el = Qr(30f);
        el.Height = 8f;
        Assert.NotNull(BarcodeSizeWarnings.Summarize(Sablona(el), null));
    }

    [Fact]
    public void PrazdnyObsah_Nevaruje()
    {
        // Nezakódovatelný nebo prázdný obsah řeší placeholder v kreslení, ne hláška o velikosti.
        var el = Qr(5f);
        el.Content = "";
        Assert.Null(BarcodeSizeWarnings.Summarize(Sablona(el), null));
    }

    private static Template Sablona(TemplateElement el)
    {
        var t = new Template();
        t.Sections.Clear();
        var section = new Section { Name = "Test" };
        section.Elements.Add(el);
        t.Sections.Add(section);
        return t;
    }

    [Fact]
    public void CarovyKod_MaPlnouVysku()
    {
        // 1D kód má nativně jeden řádek matice - musí se rozetnout na celou výšku prvku,
        // ne zůstat linkou u horní hrany.
        var el = new BarcodeElement
        {
            X = 0, Y = 0, Width = 40f, Height = 15f,
            Content = "EDISON-123", BarcodeType = BarcodeElementType.Code128, ForeColor = "#FF000000",
        };

        using var bitmap = new SKBitmap(200, 80, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            el.Render(new RenderContext(canvas, 4f));
        }

        // U spodní hrany kódu (15 mm × 4 = 60 px) musí být pruhy taky.
        bool cerneDole = false;
        for (int x = 0; x < 160; x++)
            if (bitmap.GetPixel(x, 58).Red < 128) cerneDole = true;

        Assert.True(cerneDole, "čárový kód nemá plnou výšku prvku");
    }
}
