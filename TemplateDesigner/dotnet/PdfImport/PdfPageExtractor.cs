using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Graphics.Core;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.PdfImport;

/// <summary>
/// Extrakce surového obsahu stránky z PDF přes PdfPig → meziformát ExtractedPage.
/// </summary>
public static class PdfPageExtractor
{
    private const string Black = "#FF000000";
    private const string Transparent = "#00FFFFFF";

    public static ExtractedPage Extract(PdfDocument doc, int pageNumber)
    {
        var page = doc.GetPage(pageNumber);
        float pageHeightPt = (float)page.Height;

        var result = new ExtractedPage
        {
            WidthMm = PdfCoordinateConverter.ToMm(page.Width),
            HeightMm = PdfCoordinateConverter.ToMm(page.Height)
        };

        // --- Slova ---
        foreach (var word in page.GetWords())
        {
            var bbox = word.BoundingBox;
            var (x, y, w, h) = PdfCoordinateConverter.ToTemplateBounds(
                bbox.Left, bbox.Bottom, bbox.Right, bbox.Top, pageHeightPt);

            var (fontFamily, isBold, isItalic) = PdfFontMapper.Map(
                word.Letters.Count > 0 ? word.Letters[0].FontName : null);

            float fontSizePt = word.Letters.Count > 0
                ? (float)word.Letters[0].FontSize
                : 10f;

            fontSizePt = Math.Clamp(fontSizePt, 4f, 200f);

            string colorHex = word.Letters.Count > 0
                ? ConvertColor(word.Letters[0].Color)
                : Black;

            result.Words.Add(new ExtractedWord(
                word.Text, x, y, w, h,
                fontFamily, fontSizePt, isBold, isItalic, colorHex));
        }

        // --- Paths (čáry a obdélníky) ---
        ExtractPaths(page, pageHeightPt, result);

        // --- Sloučení navazujících segmentů čar ---
        result.Lines = MergeCollinearLines(result.Lines);

        // --- Detekce obdélníků ze 4 čar ---
        DetectRectanglesFromLines(result);

        // --- Sloučení duplicitních obdélníků ---
        DeduplicateRectangles(result);

        // --- Obrázky ---
        ExtractImages(page, pageHeightPt, result);

        return result;
    }

    private static void ExtractPaths(Page page, float pageHeightPt, ExtractedPage result)
    {
        try
        {
            foreach (var path in page.Paths)
            {
                bool hasFill = path.FillColor != null && !IsWhiteOrTransparent(path.FillColor);
                bool hasStroke = path.StrokeColor != null;
                bool isFilledRect = path.IsFilled;
                bool isStroked = path.IsStroked;

                string strokeColor = hasStroke ? ConvertColor(path.StrokeColor) : Black;
                string fillColor = hasFill ? ConvertColor(path.FillColor) : Transparent;
                float lineWidth = PdfCoordinateConverter.ToMm(
                    path.LineWidth > 0 ? path.LineWidth : 0.5);

                // Clamp tloušťku čáry na rozumnou hodnotu (max 1mm)
                lineWidth = Math.Clamp(lineWidth, 0.1f, 1f);

                // Použít bounding box celého path
                var pathBbox = path.GetBoundingRectangle();
                if (pathBbox == null) continue;

                var (px, py, pw, ph) = PdfCoordinateConverter.ToTemplateBounds(
                    pathBbox.Value.Left, pathBbox.Value.Bottom,
                    pathBbox.Value.Right, pathBbox.Value.Top, pageHeightPt);

                if (pw < 0.1f && ph < 0.1f) continue;

                // Detekce stylu čáry (solid/dashed/dotted) z PDF dash patternu
                var lineStyle = DetectLineStyle(path.LineDashPattern);

                // Tenký path → čára (vertikální nebo horizontální)
                // Detekce: jedna dimenze < 1.5mm a poměr stran > 5:1
                const float thinThreshold = 1.5f; // mm — cokoli tenčí je čára
                if (ph < thinThreshold && pw > ph * 5)
                {
                    // Horizontální čára — tloušťka = lineWidth z PDF (ne bounding box height)
                    string color = isStroked ? strokeColor :
                                   (hasFill && isFilledRect ? fillColor : Black);
                    result.Lines.Add(new ExtractedLine(px, py, px + pw, py,
                        lineWidth, color, lineStyle));
                }
                else if (pw < thinThreshold && ph > pw * 5)
                {
                    // Vertikální čára
                    string color = isStroked ? strokeColor :
                                   (hasFill && isFilledRect ? fillColor : Black);
                    result.Lines.Add(new ExtractedLine(px, py, px, py + ph,
                        lineWidth, color, lineStyle));
                }
                else if (pw > 0.5f && ph > 0.5f)
                {
                    // Obdélník — jen pokud má viditelný fill NEBO viditelný stroke
                    // Přeskočit černé vyplněné obdélníky překrývající text (false positive)
                    if (!isFilledRect && !isStroked) continue;

                    // Pokud nemá fill, nastavit průhlednou výplň
                    string rectFill = (isFilledRect && hasFill) ? fillColor : Transparent;
                    string rectStroke = isStroked ? strokeColor : Transparent;

                    // Přeskočit obdélníky kde je fill černá a zabírají velkou plochu
                    // (typicky to jsou chybně detekované path segmenty)
                    if (IsBlackish(rectFill) && pw > 5f && ph > 5f)
                    {
                        // Převést na rámec bez výplně
                        rectFill = Transparent;
                        rectStroke = Black;
                    }

                    result.Rectangles.Add(new ExtractedRectangle(
                        px, py, pw, ph,
                        rectFill, rectStroke, lineWidth));
                }
            }
        }
        catch (Exception)
        {
            // Paths API může selhat na některých PDF — pokračujeme bez paths
        }
    }

    private static void ExtractImages(Page page, float pageHeightPt, ExtractedPage result)
    {
        try
        {
            foreach (var image in page.GetImages())
            {
                // Přeskočit masky (1-bit obrázky používané jako clipping)
                if (image.IsImageMask) continue;

                // Přeskočit příliš malé obrázky (pravděpodobně artefakty)
                if (image.WidthInSamples < 4 || image.HeightInSamples < 4) continue;

                if (!image.TryGetPng(out var pngBytes) || pngBytes == null || pngBytes.Length == 0)
                    continue;

                var bounds = image.Bounds;
                var (x, y, w, h) = PdfCoordinateConverter.ToTemplateBounds(
                    bounds.Left, bounds.Bottom, bounds.Right, bounds.Top, pageHeightPt);

                // Přeskočit obrázky s nerozumnými rozměry
                if (w < 1f || h < 1f) continue;

                string base64 = Convert.ToBase64String(pngBytes);
                result.Images.Add(new ExtractedImage(x, y, w, h, base64));
            }
        }
        catch (Exception)
        {
            // Image API může selhat na některých PDF — pokračujeme bez obrázků
        }
    }

    /// <summary>
    /// Detekuje obdélníky tvořené 4 čarami (2 horizontální + 2 vertikální).
    /// Čára může být i delší než hrana obdélníku (sdílená strana) — ta se neodstraní.
    /// Čáry, které přesně odpovídají hraně, se odstraní.
    /// </summary>
    private static void DetectRectanglesFromLines(ExtractedPage result)
    {
        const float tol = 1.0f; // mm — tolerance pro napojení rohů

        var horizontal = new List<(int idx, ExtractedLine line, float left, float right)>();
        var vertical = new List<(int idx, ExtractedLine line, float top, float bottom)>();

        for (int i = 0; i < result.Lines.Count; i++)
        {
            var l = result.Lines[i];
            if (Math.Abs(l.Y1 - l.Y2) < 0.5f)
                horizontal.Add((i, l, Math.Min(l.X1, l.X2), Math.Max(l.X1, l.X2)));
            else if (Math.Abs(l.X1 - l.X2) < 0.5f)
                vertical.Add((i, l, Math.Min(l.Y1, l.Y2), Math.Max(l.Y1, l.Y2)));
        }

        var usedIndices = new HashSet<int>();

        for (int hi = 0; hi < horizontal.Count; hi++)
        {
            if (usedIndices.Contains(horizontal[hi].idx)) continue;
            var (_, hTop, hTopLeft, hTopRight) = horizontal[hi];

            for (int hj = hi + 1; hj < horizontal.Count; hj++)
            {
                if (usedIndices.Contains(horizontal[hj].idx)) continue;
                var (_, hBot, hBotLeft, hBotRight) = horizontal[hj];

                // Styl musí být podobný
                if (hTop.ColorHex != hBot.ColorHex || Math.Abs(hTop.StrokeWidth - hBot.StrokeWidth) > 0.2f)
                    continue;

                // X rozsahy se musí překrývat — najít průnik
                float overlapLeft = Math.Max(hTopLeft, hBotLeft);
                float overlapRight = Math.Min(hTopRight, hBotRight);
                if (overlapRight - overlapLeft < 2f) continue; // příliš malý průnik

                float rectTop = Math.Min(hTop.Y1, hBot.Y1);
                float rectBottom = Math.Max(hTop.Y1, hBot.Y1);
                if (rectBottom - rectTop < 1f) continue;

                // Hledat vertikální čáry pokrývající levý a pravý okraj
                int leftIdx = -1, rightIdx = -1;

                foreach (var (vi, vl, vTop, vBottom) in vertical)
                {
                    if (usedIndices.Contains(vi)) continue;
                    if (vl.ColorHex != hTop.ColorHex || Math.Abs(vl.StrokeWidth - hTop.StrokeWidth) > 0.2f)
                        continue;

                    // Vertikální musí pokrývat Y rozsah obdélníku (může být delší)
                    if (vTop > rectTop + tol || vBottom < rectBottom - tol)
                        continue;

                    if (Math.Abs(vl.X1 - overlapLeft) < tol && leftIdx == -1)
                        leftIdx = vi;
                    else if (Math.Abs(vl.X1 - overlapRight) < tol && rightIdx == -1)
                        rightIdx = vi;
                }

                if (leftIdx == -1 || rightIdx == -1) continue;

                // Nalezen obdélník
                float x = overlapLeft;
                float y = rectTop;
                float w = overlapRight - overlapLeft;
                float h = rectBottom - rectTop;

                result.Rectangles.Add(new ExtractedRectangle(
                    x, y, w, h,
                    Transparent,
                    hTop.ColorHex,
                    hTop.StrokeWidth));

                // Odebrat jen čáry, které přesně odpovídají hraně obdélníku.
                // Delší čáry (sdílená strana) ponechat.
                if (Math.Abs(hTopLeft - overlapLeft) < tol && Math.Abs(hTopRight - overlapRight) < tol)
                    usedIndices.Add(horizontal[hi].idx);
                if (Math.Abs(hBotLeft - overlapLeft) < tol && Math.Abs(hBotRight - overlapRight) < tol)
                    usedIndices.Add(horizontal[hj].idx);

                var leftLine = vertical.First(v => v.idx == leftIdx);
                if (Math.Abs(leftLine.top - rectTop) < tol && Math.Abs(leftLine.bottom - rectBottom) < tol)
                    usedIndices.Add(leftIdx);

                var rightLine = vertical.First(v => v.idx == rightIdx);
                if (Math.Abs(rightLine.top - rectTop) < tol && Math.Abs(rightLine.bottom - rectBottom) < tol)
                    usedIndices.Add(rightIdx);

                break; // top čára spotřebovaná (i pokud nebyla odebraná, obdélník vznikl)
            }
        }

        if (usedIndices.Count > 0)
            result.Lines = result.Lines.Where((_, i) => !usedIndices.Contains(i)).ToList();
    }

    /// <summary>Odstraní duplicitní obdélníky (stejná pozice a rozměry, tolerance 0.5mm).</summary>
    private static void DeduplicateRectangles(ExtractedPage result)
    {
        if (result.Rectangles.Count <= 1) return;

        const float tol = 0.5f;
        var keep = new List<ExtractedRectangle>();

        for (int i = 0; i < result.Rectangles.Count; i++)
        {
            bool isDuplicate = false;
            var a = result.Rectangles[i];

            for (int j = 0; j < keep.Count; j++)
            {
                var b = keep[j];
                if (Math.Abs(a.X - b.X) < tol && Math.Abs(a.Y - b.Y) < tol &&
                    Math.Abs(a.Width - b.Width) < tol && Math.Abs(a.Height - b.Height) < tol)
                {
                    // Ponechat ten s viditelnějším stylem (stroke > fill-only)
                    bool aHasStroke = a.StrokeColorHex != null && a.StrokeColorHex != Transparent;
                    bool bHasStroke = b.StrokeColorHex != null && b.StrokeColorHex != Transparent;
                    bool aHasFill = a.FillColorHex != null && a.FillColorHex != Transparent;
                    bool bHasFill = b.FillColorHex != null && b.FillColorHex != Transparent;

                    int aScore = (aHasStroke ? 2 : 0) + (aHasFill ? 1 : 0);
                    int bScore = (bHasStroke ? 2 : 0) + (bHasFill ? 1 : 0);

                    if (aScore > bScore)
                        keep[j] = a; // nahradit méně viditelný

                    isDuplicate = true;
                    break;
                }
            }

            if (!isDuplicate)
                keep.Add(a);
        }

        result.Rectangles.Clear();
        result.Rectangles.AddRange(keep);
    }

    /// <summary>Sloučí navazující kolineární segmenty čar do jedné čáry.</summary>
    private static List<ExtractedLine> MergeCollinearLines(List<ExtractedLine> lines)
    {
        if (lines.Count <= 1) return lines;

        const float posTolerance = 0.5f; // mm — tolerance pro stejnou osu
        const float gapTolerance = 0.5f; // mm — maximální mezera mezi koncem a začátkem

        // Rozdělit na horizontální a vertikální
        var horizontal = new List<ExtractedLine>();
        var vertical = new List<ExtractedLine>();
        var other = new List<ExtractedLine>();

        foreach (var line in lines)
        {
            if (Math.Abs(line.Y1 - line.Y2) < posTolerance)
                horizontal.Add(line);
            else if (Math.Abs(line.X1 - line.X2) < posTolerance)
                vertical.Add(line);
            else
                other.Add(line);
        }

        var result = new List<ExtractedLine>();
        result.AddRange(MergeHorizontal(horizontal, posTolerance, gapTolerance));
        result.AddRange(MergeVertical(vertical, posTolerance, gapTolerance));
        result.AddRange(other);
        return result;
    }

    private static List<ExtractedLine> MergeHorizontal(List<ExtractedLine> lines,
        float posTolerance, float gapTolerance)
    {
        if (lines.Count == 0) return lines;

        // Seřadit podle Y, pak podle levého X
        var sorted = lines.OrderBy(l => l.Y1).ThenBy(l => Math.Min(l.X1, l.X2)).ToList();
        var result = new List<ExtractedLine>();

        float curY = sorted[0].Y1;
        float curLeft = Math.Min(sorted[0].X1, sorted[0].X2);
        float curRight = Math.Max(sorted[0].X1, sorted[0].X2);
        float curSw = sorted[0].StrokeWidth;
        string curColor = sorted[0].ColorHex;
        var curStyle = sorted[0].Style;

        for (int i = 1; i < sorted.Count; i++)
        {
            var line = sorted[i];
            float left = Math.Min(line.X1, line.X2);
            float right = Math.Max(line.X1, line.X2);

            bool sameY = Math.Abs(line.Y1 - curY) < posTolerance;
            bool connects = left - curRight <= gapTolerance;
            bool sameAppearance = Math.Abs(line.StrokeWidth - curSw) < 0.2f
                             && line.ColorHex == curColor
                             && line.Style == curStyle;

            if (sameY && connects && sameAppearance)
            {
                curRight = Math.Max(curRight, right);
            }
            else
            {
                result.Add(new ExtractedLine(curLeft, curY, curRight, curY, curSw, curColor, curStyle));
                curY = line.Y1;
                curLeft = left;
                curRight = right;
                curSw = line.StrokeWidth;
                curColor = line.ColorHex;
                curStyle = line.Style;
            }
        }

        result.Add(new ExtractedLine(curLeft, curY, curRight, curY, curSw, curColor, curStyle));
        return result;
    }

    private static List<ExtractedLine> MergeVertical(List<ExtractedLine> lines,
        float posTolerance, float gapTolerance)
    {
        if (lines.Count == 0) return lines;

        // Seřadit podle X, pak podle horního Y
        var sorted = lines.OrderBy(l => l.X1).ThenBy(l => Math.Min(l.Y1, l.Y2)).ToList();
        var result = new List<ExtractedLine>();

        float curX = sorted[0].X1;
        float curTop = Math.Min(sorted[0].Y1, sorted[0].Y2);
        float curBottom = Math.Max(sorted[0].Y1, sorted[0].Y2);
        float curSw = sorted[0].StrokeWidth;
        string curColor = sorted[0].ColorHex;
        var curStyle = sorted[0].Style;

        for (int i = 1; i < sorted.Count; i++)
        {
            var line = sorted[i];
            float top = Math.Min(line.Y1, line.Y2);
            float bottom = Math.Max(line.Y1, line.Y2);

            bool sameX = Math.Abs(line.X1 - curX) < posTolerance;
            bool connects = top - curBottom <= gapTolerance;
            bool sameAppearance = Math.Abs(line.StrokeWidth - curSw) < 0.2f
                             && line.ColorHex == curColor
                             && line.Style == curStyle;

            if (sameX && connects && sameAppearance)
            {
                curBottom = Math.Max(curBottom, bottom);
            }
            else
            {
                result.Add(new ExtractedLine(curX, curTop, curX, curBottom, curSw, curColor, curStyle));
                curX = line.X1;
                curTop = top;
                curBottom = bottom;
                curSw = line.StrokeWidth;
                curColor = line.ColorHex;
                curStyle = line.Style;
            }
        }

        result.Add(new ExtractedLine(curX, curTop, curX, curBottom, curSw, curColor, curStyle));
        return result;
    }

    /// <summary>
    /// Detekuje styl čáry z PDF dash patternu.
    /// Vrací jen styl (Solid/Dotted/Dashed) — skutečné rozměry dash/gap z PDF nejsou
    /// spolehlivé (PDF může mít transformační matici, která mění měřítko).
    /// LineElement použije své výchozí hodnoty pro daný styl.
    /// </summary>
    private static LineStyle DetectLineStyle(LineDashPattern? pattern)
    {
        if (pattern == null) return LineStyle.Solid;

        var p = pattern.Value;
        if (p.Array == null || p.Array.Count == 0)
            return LineStyle.Solid;

        // Neprázdný dash array = ne-plná čára.
        // Klasifikace: dash ≤ gap → tečkovaná (typické pro vyplňovací pole formulářů),
        // jinak čárkovaná.
        double dashVal = p.Array[0];
        double gapVal = p.Array.Count > 1 ? p.Array[1] : dashVal;

        return dashVal <= gapVal ? LineStyle.Dotted : LineStyle.Dashed;
    }

    /// <summary>Kontrola, zda barva je bílá nebo průhledná (ne-viditelná fill).</summary>
    private static bool IsWhiteOrTransparent(IColor? color)
    {
        if (color == null) return true;
        if (color is GrayColor gray) return gray.Gray >= 0.99;
        if (color is RGBColor rgb) return rgb.R >= 0.99 && rgb.G >= 0.99 && rgb.B >= 0.99;
        return false;
    }

    /// <summary>Kontrola, zda hex barva je blízko černé.</summary>
    private static bool IsBlackish(string hex)
    {
        if (hex.Length < 9) return false;
        // #AARRGGBB — parsovat R, G, B
        if (byte.TryParse(hex.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
            byte.TryParse(hex.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
            byte.TryParse(hex.AsSpan(7, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
        {
            return r < 40 && g < 40 && b < 40;
        }
        return false;
    }

    private static string ConvertColor(IColor? color)
    {
        if (color == null)
            return Black;

        return color switch
        {
            RGBColor rgb => $"#FF{ToByte(rgb.R):X2}{ToByte(rgb.G):X2}{ToByte(rgb.B):X2}",
            GrayColor gray => FormatGray(gray.Gray),
            CMYKColor cmyk => ConvertCmyk(cmyk.C, cmyk.M, cmyk.Y, cmyk.K),
            _ => Black
        };
    }

    private static string ConvertCmyk(double c, double m, double y, double k)
    {
        byte r = (byte)Math.Clamp((int)(255 * (1 - c) * (1 - k)), 0, 255);
        byte g = (byte)Math.Clamp((int)(255 * (1 - m) * (1 - k)), 0, 255);
        byte b = (byte)Math.Clamp((int)(255 * (1 - y) * (1 - k)), 0, 255);
        return $"#FF{r:X2}{g:X2}{b:X2}";
    }

    private static byte ToByte(double value) => (byte)Math.Clamp((int)(value * 255), 0, 255);

    private static string FormatGray(double gray)
    {
        byte v = ToByte(gray);
        return $"#FF{v:X2}{v:X2}{v:X2}";
    }
}
