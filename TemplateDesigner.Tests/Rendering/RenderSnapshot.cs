using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Vykreslí element mimo WPF do bitmapy a udělá z ní odtisk - nástroj pro **migrační důkaz**
/// u refaktorů kreslicí cesty: sejmi odtisky před zásahem, po zásahu musí být identické.
/// Bez toho se „nic se nerozbilo" dá tvrdit jen z proklikání, které neuchytí posun o desetiny mm.
///
/// ⚠️ **Odtisk je vázaný na stroj** - projekt nemá zabalené textové fonty (jen symbolové
/// `TimeTable2`), takže text rasterizuje Arial z OS. Baseline hodnoty proto **nepatří do gitu**
/// (kolegovi by padal pre-push); porovnávej vždy jen před/po na tomtéž stroji, přes soubor mimo
/// repozitář a **zapnutím `TD_RENDER_BASELINE=1`** (jinak porovnání neběží - viz
/// <c>ElementRenderSnapshotTests</c>). Testy v gitu hlídají jen determinismus a citlivost harnessu.
/// </summary>
internal static class RenderSnapshot
{
    /// <summary>Pevné měřítko odtisku (px/mm) - vyšší = citlivější na posun, ale pomalejší.</summary>
    public const float MmToPx = 4f;

    /// <summary>Vykreslí element (obsah + rámeček) na bílé pozadí a vrátí odtisk pixelů.</summary>
    public static string Capture(TemplateElement element, DataContext? data,
        float canvasWidthMm = 210f, float canvasHeightMm = 148f)
    {
        int w = (int)MathF.Ceiling(canvasWidthMm * MmToPx);
        int h = (int)MathF.Ceiling(canvasHeightMm * MmToPx);

        using var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            var ctx = new RenderContext(canvas, MmToPx, data);
            // Stejné pořadí jako skutečná kreslicí pipeline: pozadí → obsah → rámeček (2. průchod)
            element.RenderBackgroundPass(ctx);
            element.Render(ctx);
            element.RenderBorderPass(ctx);
        }

        return Hash(bitmap);
    }

    private static string Hash(SKBitmap bitmap)
    {
        using var pixmap = bitmap.PeekPixels();
        var bytes = pixmap.GetPixelSpan();
        var digest = SHA256.HashData(bytes);
        return Convert.ToHexString(digest)[..16];   // 16 znaků stačí na kolizní bezpečnost pro odtisk
    }

    // ── Stavba dat (klíče = wire kontrakt agend) ──────────────────────────────

    public static DataContext Row(params (string Key, object? Value)[] fields) =>
        new(fields.ToDictionary(f => f.Key, f => f.Value));

    public static DataContext Data(params (string Collection, IReadOnlyList<DataContext> Rows)[] collections) =>
        new(values: new Dictionary<string, object?>(),
            rows: collections.ToDictionary(c => c.Collection, c => c.Rows),
            culture: CultureInfo.GetCultureInfo("cs-CZ"));

    /// <summary>Vnořené pole (ANOTACE, ATRIBUTY…) - elementy je čtou jako <see cref="JsonArray"/>.</summary>
    public static JsonArray Nested(params (string Key, string Value)[][] items)
    {
        var arr = new JsonArray();
        foreach (var item in items)
        {
            var obj = new JsonObject();
            foreach (var (k, v) in item) obj[k] = v;
            arr.Add(obj);
        }
        return arr;
    }

    public static string Describe(string label, string hash) =>
        new StringBuilder().Append(label).Append(" = ").Append(hash).ToString();
}
