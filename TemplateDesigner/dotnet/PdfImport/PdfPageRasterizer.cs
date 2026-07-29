using SkiaSharp;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.PdfImport;

/// <summary>
/// Rasterizuje ExtractedPage na PNG screenshot pro AI vision vstup.
/// Kompozitní rendering: obdélníky → obrázky → čáry → text.
/// Výstup je vizuální aproximace PDF stránky - nemusí být pixel-perfect,
/// AI potřebuje jen rozpoznat strukturu layoutu.
/// </summary>
public static class PdfPageRasterizer
{
    /// <summary>
    /// Rasterizuje extrahovanou stránku na base64 PNG.
    /// </summary>
    /// <param name="page">Extrahovaná stránka z PdfPig.</param>
    /// <param name="dpi">Rozlišení (150 DPI = dobrá kvalita, ~300KB pro A4).</param>
    /// <returns>Base64-encoded PNG string.</returns>
    public static string RasterizeToBase64Png(ExtractedPage page, int dpi = 150)
    {
        float scale = dpi / 25.4f; // mm → px
        int width = Math.Max(1, (int)(page.WidthMm * scale));
        int height = Math.Max(1, (int)(page.HeightMm * scale));

        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        // 1. Obdélníky (pozadí)
        foreach (var rect in page.Rectangles)
        {
            var skRect = new SKRect(
                rect.X * scale, rect.Y * scale,
                (rect.X + rect.Width) * scale, (rect.Y + rect.Height) * scale);

            if (rect.FillColorHex is not null && rect.FillColorHex != "#00FFFFFF")
            {
                using var paint = new SKPaint { Color = SKColor.Parse(rect.FillColorHex), Style = SKPaintStyle.Fill };
                canvas.DrawRect(skRect, paint);
            }

            if (rect.StrokeColorHex is not null && rect.StrokeWidth > 0)
            {
                using var paint = new SKPaint
                {
                    Color = SKColor.Parse(rect.StrokeColorHex),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = Math.Max(1f, rect.StrokeWidth * scale),
                    IsAntialias = true
                };
                canvas.DrawRect(skRect, paint);
            }
        }

        // 2. Obrázky
        foreach (var img in page.Images)
        {
            try
            {
                var pngBytes = Convert.FromBase64String(img.Base64Png);
                using var skBitmap = SKBitmap.Decode(pngBytes);
                if (skBitmap == null) continue;

                var destRect = new SKRect(
                    img.X * scale, img.Y * scale,
                    (img.X + img.Width) * scale, (img.Y + img.Height) * scale);

                using var skImage = SKImage.FromBitmap(skBitmap);
                var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
                using var paint = new SKPaint { IsAntialias = true };
                canvas.DrawImage(skImage, destRect, sampling, paint);
            }
            catch { /* neplatná data */ }
        }

        // 3. Čáry
        foreach (var line in page.Lines)
        {
            using var paint = new SKPaint
            {
                Color = SKColor.Parse(line.ColorHex),
                StrokeWidth = Math.Max(1f, line.StrokeWidth * scale),
                IsAntialias = true
            };

            if (line.Style != LineStyle.Solid)
            {
                var (dashMm, gapMm) = LineElement.GetDefaultDashPattern(line.Style);
                paint.PathEffect = SKPathEffect.CreateDash([dashMm * scale, gapMm * scale], 0);
            }

            canvas.DrawLine(
                line.X1 * scale, line.Y1 * scale,
                line.X2 * scale, line.Y2 * scale,
                paint);

            paint.PathEffect?.Dispose();
        }

        // 4. Text - renderovat slova na správné pozice
        foreach (var word in page.Words)
        {
            float fontSizePx = word.FontSizePt * (dpi / 72f);

            using var font = new SKFont
            {
                Size = fontSizePx,
                Typeface = SKTypeface.FromFamilyName(
                    word.FontFamily ?? "Arial",
                    word.IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                    SKFontStyleWidth.Normal,
                    word.IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright)
            };

            using var paint = new SKPaint
            {
                Color = SKColor.Parse(word.ColorHex),
                IsAntialias = true
            };

            // PDF Y je top of bounding box; SkiaSharp DrawText kreslí na baseline
            // Approximate ascent ratio - PDF Y is top of bounding box, SkiaSharp needs baseline
            float baseline = word.Y * scale + fontSizePx * 0.8f;
            canvas.DrawText(word.Text, word.X * scale, baseline, font, paint);
        }

        // Encode PNG → Base64
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 85);
        return Convert.ToBase64String(data.ToArray());
    }
}
