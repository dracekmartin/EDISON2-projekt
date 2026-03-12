using System.IO;
using SkiaSharp;
using UglyToad.PdfPig;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.PdfImport;

/// <summary>
/// Orchestrátor: PDF soubor → Template objekt.
/// Extrahuje stránky, seskupí text, převede na elementy.
/// </summary>
public class PdfToTemplateConverter
{
    private const string Black = "#FF000000";
    private const string Transparent = "#00FFFFFF";

    /// <summary>Debug log z posledního importu. Null pokud debug vypnutý.</summary>
    public string? DebugLog { get; private set; }

    /// <summary>Zapnout detailní debug výpis.</summary>
    public bool EnableDebug { get; set; }

    /// <summary>Režim sloučení grafiky do pozadí obrázku.</summary>
    public GraphicsMergeMode MergeMode { get; set; } = GraphicsMergeMode.None;

    public Template Convert(string pdfPath, int[]? pages = null)
    {
        using var doc = PdfDocument.Open(pdfPath);
        var log = EnableDebug ? new System.Text.StringBuilder() : null;

        var pageNumbers = pages ?? Enumerable.Range(1, doc.NumberOfPages).ToArray();
        var sections = new List<Section>();
        var extractedPages = new List<ExtractedPage>();

        float pageWidthMm = 210f;
        float pageHeightMm = 297f;

        foreach (int pageNum in pageNumbers)
        {
            if (pageNum < 1 || pageNum > doc.NumberOfPages) continue;

            var extracted = PdfPageExtractor.Extract(doc, pageNum);
            pageWidthMm = extracted.WidthMm;
            pageHeightMm = extracted.HeightMm;

            if (log is not null)
            {
                log.AppendLine($"=== PAGE {pageNum} ({extracted.WidthMm:0.#} x {extracted.HeightMm:0.#} mm) ===");
                log.AppendLine($"\n--- RAW WORDS ({extracted.Words.Count}) ---");
                foreach (var w in extracted.Words)
                    log.AppendLine($"  [{w.X:0.#},{w.Y:0.#} {w.Width:0.#}x{w.Height:0.#}] \"{w.Text}\"  font={w.FontFamily} {w.FontSizePt:0.#}pt {(w.IsBold?"B":"")}{(w.IsItalic?"I":"")} color={w.ColorHex}");

                log.AppendLine($"\n--- RAW LINES ({extracted.Lines.Count}) ---");
                foreach (var l in extracted.Lines)
                    log.AppendLine($"  ({l.X1:0.#},{l.Y1:0.#})->({l.X2:0.#},{l.Y2:0.#}) sw={l.StrokeWidth:0.##} color={l.ColorHex} style={l.Style}");

                log.AppendLine($"\n--- RAW RECTANGLES ({extracted.Rectangles.Count}) ---");
                foreach (var r in extracted.Rectangles)
                    log.AppendLine($"  [{r.X:0.#},{r.Y:0.#} {r.Width:0.#}x{r.Height:0.#}] fill={r.FillColorHex} stroke={r.StrokeColorHex} sw={r.StrokeWidth:0.##}");

                log.AppendLine($"\n--- IMAGES ({extracted.Images.Count}) ---");
                foreach (var img in extracted.Images)
                    log.AppendLine($"  [{img.X:0.#},{img.Y:0.#} {img.Width:0.#}x{img.Height:0.#}] base64={img.Base64Png.Length} chars");
            }

            var textBlocks = PdfTextGrouper.GroupWords(extracted.Words);

            if (log is not null)
            {
                log.AppendLine($"\n--- GROUPED TEXT BLOCKS ({textBlocks.Count}) ---");
                foreach (var b in textBlocks)
                    log.AppendLine($"  [{b.X:0.#},{b.Y:0.#} {b.Width:0.#}x{b.Height:0.#}] \"{b.Text.Replace("\n","\\n")}\"  font={b.FontFamily} {b.FontSizePt:0.#}pt");
            }

            var section = BuildSection(pageNum, extracted, textBlocks, pageWidthMm, pageHeightMm, MergeMode);
            sections.Add(section);
            extractedPages.Add(extracted);
        }

        DebugLog = log?.ToString();

        // Detekce okrajů ze všech elementů (v merge mode jen z textů)
        var (marginTop, marginBottom, marginLeft, marginRight) =
            DetectMargins(sections, pageWidthMm, pageHeightMm);

        // Posunout elementy relativně k okrajům
        AdjustElementPositions(sections, marginLeft, marginTop);

        // Výška sekcí = výška stránky minus okraje
        float contentHeight = pageHeightMm - marginTop - marginBottom;
        foreach (var section in sections)
            section.DesignerHeight = Math.Max(contentHeight, 10f);

        // Merge mode: vykreslení sloučené grafiky s offset na margin
        if (MergeMode != GraphicsMergeMode.None)
        {
            float contentWidth = pageWidthMm - marginLeft - marginRight;
            for (int i = 0; i < sections.Count; i++)
            {
                var bgImage = RenderGraphicsToBackground(
                    extractedPages[i],
                    marginLeft, marginTop, contentWidth, contentHeight, MergeMode);
                if (bgImage != null)
                {
                    sections[i].Elements.Insert(0, new ImageElement
                    {
                        Id = Guid.NewGuid(),
                        X = 0, Y = 0,
                        Width = contentWidth,
                        Height = contentHeight,
                        ZIndex = -1,
                        ImageBase64 = bgImage,
                        IsLocked = true
                    });
                }

                // LinesOnly: obdélníky a obrázky se přidají jako samostatné elementy
                if (MergeMode == GraphicsMergeMode.LinesOnly)
                {
                    int zIdx = 0;
                    var extraElements = new List<TemplateElement>();
                    foreach (var rect in extractedPages[i].Rectangles)
                    {
                        extraElements.Add(new RectangleElement
                        {
                            Id = Guid.NewGuid(),
                            X = Math.Max(0, rect.X - marginLeft), Y = Math.Max(0, rect.Y - marginTop),
                            Width = rect.Width, Height = rect.Height,
                            ZIndex = zIdx++,
                            FillColor = rect.FillColorHex ?? Transparent,
                            StrokeColor = rect.StrokeColorHex ?? Black,
                            StrokeWidth = Math.Clamp(rect.StrokeWidth, 0.1f, 0.5f)
                        });
                    }
                    zIdx = 50;
                    foreach (var img in extractedPages[i].Images)
                    {
                        extraElements.Add(new ImageElement
                        {
                            Id = Guid.NewGuid(),
                            X = Math.Max(0, img.X - marginLeft), Y = Math.Max(0, img.Y - marginTop),
                            Width = img.Width, Height = img.Height,
                            ZIndex = zIdx++,
                            ImageBase64 = img.Base64Png
                        });
                    }
                    // Vložit za background obrázek (index 0), před texty
                    sections[i].Elements.InsertRange(1, extraElements);
                }
            }
        }

        // Automatické skupiny podle typu elementu (po merge, kdy jsou všechny elementy na místě)
        AssignAutoGroups(sections, MergeMode);

        return new Template
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileNameWithoutExtension(pdfPath),
            PageSize = new PageSize
            {
                Width = pageWidthMm,
                Height = pageHeightMm,
                MarginTop = marginTop,
                MarginBottom = marginBottom,
                MarginLeft = marginLeft,
                MarginRight = marginRight
            },
            Sections = sections,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

    private static Section BuildSection(int pageNum, ExtractedPage page,
        List<ExtractedTextBlock> textBlocks, float pageWidthMm, float pageHeightMm,
        GraphicsMergeMode mergeMode)
    {
        var elements = new List<TemplateElement>();
        int zIndex = 0;

        if (mergeMode == GraphicsMergeMode.None)
        {
            // Žádné sloučení: jednotlivé grafické elementy
            BuildGraphicElements(page, elements, ref zIndex);
        }
        // Merge/LinesOnly: grafika se přidá později v Convert() po margin detection

        // Textové bloky (ZIndex 200+)
        zIndex = 200;
        foreach (var block in textBlocks)
        {
            var alignment = DetectAlignment(block, pageWidthMm);

            // Minimální výška textu: FontSize (pt) → mm * faktor pro ascent+descent+padding
            // SkiaSharp DrawText kreslí na baseline (top + fontSizePx), pod ní je descent (~25% fontu)
            // PDF bbox pokrývá jen glyphy, takže je příliš malý. Faktor 1.4 = bezpečný odhad.
            int lineCount = block.Text.Count(c => c == '\n') + 1;
            float fontHeightMm = block.FontSizePt * 0.3528f; // 1pt = 0.3528mm
            float minHeight = fontHeightMm * 1.4f * lineCount;
            float height = Math.Max(block.Height, minHeight);

            // Kompenzace renderovacího offsetu: SkiaSharp DrawText kreslí na baseline
            // (rect.Top + fontSizePx), ale ascent fontu je ~80% fontSize — vzniká mezera
            // ~20% fontHeight nad textem. Posuneme Y nahoru aby text vizuálně seděl
            // na stejné pozici jako v PDF.
            float ascentCompensation = fontHeightMm * 0.2f;

            elements.Add(new TextElement
            {
                Id = Guid.NewGuid(),
                X = block.X,
                Y = Math.Max(0, block.Y - ascentCompensation),
                Width = Math.Max(block.Width, fontHeightMm * 2), // minimální šířka
                Height = height,
                ZIndex = zIndex++,
                Text = block.Text,
                FontFamily = block.FontFamily,
                FontSize = block.FontSizePt,
                FontWeight = block.IsBold ? TextFontWeight.Bold : TextFontWeight.Normal,
                FontStyle = block.IsItalic ? TextFontStyle.Italic : TextFontStyle.Normal,
                HorizontalAlignment = alignment,
                TextColor = block.ColorHex,
                WordWrap = block.Text.Contains('\n')
            });
        }

        return new Section
        {
            Id = Guid.NewGuid(),
            Name = $"Strana {pageNum}",
            PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether,
            Elements = elements
        };
    }

    /// <summary>Vytvoří jednotlivé grafické elementy (obdélníky, obrázky, čáry) — původní režim.</summary>
    private static void BuildGraphicElements(ExtractedPage page, List<TemplateElement> elements, ref int zIndex)
    {
        // Obdélníky (ZIndex 0+)
        foreach (var rect in page.Rectangles)
        {
            elements.Add(new RectangleElement
            {
                Id = Guid.NewGuid(),
                X = rect.X, Y = rect.Y,
                Width = rect.Width, Height = rect.Height,
                ZIndex = zIndex++,
                FillColor = rect.FillColorHex ?? Transparent,
                StrokeColor = rect.StrokeColorHex ?? Black,
                StrokeWidth = Math.Clamp(rect.StrokeWidth, 0.1f, 0.5f)
            });
        }

        // Obrázky (ZIndex navazuje na obdélníky)
        foreach (var img in page.Images)
        {
            elements.Add(new ImageElement
            {
                Id = Guid.NewGuid(),
                X = img.X, Y = img.Y,
                Width = img.Width, Height = img.Height,
                ZIndex = zIndex++,
                ImageBase64 = img.Base64Png
            });
        }

        // Čáry (ZIndex 100+, nad obdélníky/obrázky)
        zIndex = Math.Max(zIndex, 100);
        foreach (var line in page.Lines)
        {
            bool isHorizontal = Math.Abs(line.Y1 - line.Y2) < Math.Abs(line.X1 - line.X2);
            float x = Math.Min(line.X1, line.X2);
            float y = Math.Min(line.Y1, line.Y2);
            float strokeWidth = Math.Clamp(line.StrokeWidth, 0.1f, 0.5f);
            float w, h;

            if (isHorizontal) { w = Math.Abs(line.X2 - line.X1); h = strokeWidth; }
            else              { w = strokeWidth; h = Math.Abs(line.Y2 - line.Y1); }

            elements.Add(new LineElement
            {
                Id = Guid.NewGuid(),
                X = x, Y = y,
                Width = Math.Max(w, 0.5f), Height = Math.Max(h, 0.5f),
                ZIndex = zIndex++,
                Orientation = isHorizontal ? LineOrientation.Horizontal : LineOrientation.Vertical,
                Color = line.ColorHex,
                StrokeWidth = strokeWidth,
                Style = line.Style
            });
        }
    }

    /// <summary>
    /// Vykreslí veškerou grafiku (čáry, obdélníky, obrázky) do jedné bitmapy → Base64 PNG.
    /// Rozlišení: 150 DPI. Souřadnice se posouvají o margin → výstup pokrývá jen content area.
    /// </summary>
    private static string? RenderGraphicsToBackground(ExtractedPage page,
        float marginLeft, float marginTop, float contentWidthMm, float contentHeightMm,
        GraphicsMergeMode mode)
    {
        // LinesOnly: pouze čáry do bitmapy; All: vše do bitmapy
        bool includeRects = mode == GraphicsMergeMode.All;
        bool includeImages = mode == GraphicsMergeMode.All;
        bool includeLines = true; // čáry vždy

        bool hasContent = (includeLines && page.Lines.Count > 0)
            || (includeRects && page.Rectangles.Count > 0)
            || (includeImages && page.Images.Count > 0);
        if (!hasContent) return null;

        // 150 DPI: 1mm = 150/25.4 ≈ 5.9 px
        const float dpi = 150f;
        float scale = dpi / 25.4f;
        int width = (int)(contentWidthMm * scale);
        int height = (int)(contentHeightMm * scale);
        // Offset pro posun souřadnic z absolutních do content-relative
        float ox = marginLeft * scale;
        float oy = marginTop * scale;

        if (width < 1 || height < 1) return null;

        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        // Obdélníky (jen v režimu All)
        if (includeRects)
        {
            foreach (var rect in page.Rectangles)
            {
                var skRect = new SKRect(
                    rect.X * scale - ox, rect.Y * scale - oy,
                    (rect.X + rect.Width) * scale - ox, (rect.Y + rect.Height) * scale - oy);

                if (rect.FillColorHex is not null && rect.FillColorHex != Transparent)
                {
                    using var fillPaint = new SKPaint { Color = SKColor.Parse(rect.FillColorHex), Style = SKPaintStyle.Fill };
                    canvas.DrawRect(skRect, fillPaint);
                }

                if (rect.StrokeColorHex is not null && rect.StrokeColorHex != Transparent)
                {
                    float sw = Math.Clamp(rect.StrokeWidth, 0.1f, 0.5f) * scale;
                    using var strokePaint = new SKPaint
                    {
                        Color = SKColor.Parse(rect.StrokeColorHex),
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = Math.Max(1f, sw),
                        IsAntialias = true
                    };
                    canvas.DrawRect(skRect, strokePaint);
                }
            }
        }

        // Obrázky (jen v režimu All)
        if (includeImages)
        {
            foreach (var img in page.Images)
            {
                try
                {
                    var pngBytes = System.Convert.FromBase64String(img.Base64Png);
                    using var skBitmap = SKBitmap.Decode(pngBytes);
                    if (skBitmap == null) continue;

                    var destRect = new SKRect(
                        img.X * scale - ox, img.Y * scale - oy,
                        (img.X + img.Width) * scale - ox, (img.Y + img.Height) * scale - oy);

                    using var skImage = SKImage.FromBitmap(skBitmap);
                    var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
                    using var paint = new SKPaint { IsAntialias = true };
                    canvas.DrawImage(skImage, destRect, sampling, paint);
                }
                catch { /* neplatná data */ }
            }
        }

        // Čáry
        foreach (var line in page.Lines)
        {
            float sw = Math.Clamp(line.StrokeWidth, 0.1f, 0.5f) * scale;
            using var paint = new SKPaint
            {
                Color = SKColor.Parse(line.ColorHex),
                StrokeWidth = Math.Max(1f, sw),
                IsAntialias = true
            };

            if (line.Style != LineStyle.Solid)
            {
                var (dashMm, gapMm) = LineElement.GetDefaultDashPattern(line.Style);
                float dash = dashMm * scale;
                float gap  = gapMm * scale;
                var effect = SKPathEffect.CreateDash([dash, gap], 0);
                paint.PathEffect = effect;
            }

            canvas.DrawLine(
                line.X1 * scale - ox, line.Y1 * scale - oy,
                line.X2 * scale - ox, line.Y2 * scale - oy,
                paint);

            paint.PathEffect?.Dispose();
        }

        // Encode to PNG → Base64
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return System.Convert.ToBase64String(data.ToArray());
    }

    /// <summary>
    /// Přiřadí automatické GroupId importovaným elementům podle typu a merge mode.
    /// Texty vždy bez skupiny.
    /// </summary>
    private static void AssignAutoGroups(List<Section> sections, GraphicsMergeMode mergeMode)
    {
        foreach (var section in sections)
        {
            if (mergeMode == GraphicsMergeMode.LinesOnly)
            {
                // Background image + obdélníky + importované obrázky → společná skupina "Grafika"
                var graphicsElements = section.Elements
                    .Where(e => e is ImageElement or RectangleElement)
                    .ToList();
                if (graphicsElements.Count > 1)
                {
                    var gid = ElementGroupHelper.NewGroupId();
                    foreach (var el in graphicsElements)
                        el.GroupId = gid;
                }
            }
            else if (mergeMode == GraphicsMergeMode.None)
            {
                // Čáry → jedna skupina, obdélníky → druhá skupina
                var lines = section.Elements.Where(e => e is LineElement).ToList();
                var rects = section.Elements.Where(e => e is RectangleElement).ToList();

                if (lines.Count > 1)
                {
                    var gid = ElementGroupHelper.NewGroupId();
                    foreach (var el in lines) el.GroupId = gid;
                }
                if (rects.Count > 1)
                {
                    var gid = ElementGroupHelper.NewGroupId();
                    foreach (var el in rects) el.GroupId = gid;
                }
            }
            // All: background image je jen jeden prvek — skupina nemá smysl
        }
    }

    private static TextHorizontalAlignment DetectAlignment(ExtractedTextBlock block, float pageWidthMm)
    {
        float pageCenterX = pageWidthMm / 2f;
        float blockCenterX = block.X + block.Width / 2f;
        float blockRightEdge = block.X + block.Width;
        const float tolerance = 3f; // mm

        // Střed bloku blízko středu stránky → Center
        if (Math.Abs(blockCenterX - pageCenterX) < tolerance)
            return TextHorizontalAlignment.Center;

        // Pravý okraj bloku blízko pravého okraje stránky → Right
        if (Math.Abs(blockRightEdge - pageWidthMm) < tolerance * 2)
            return TextHorizontalAlignment.Right;

        return TextHorizontalAlignment.Left;
    }

    private static (float Top, float Bottom, float Left, float Right) DetectMargins(
        List<Section> sections, float pageWidthMm, float pageHeightMm)
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxRight = 0f;
        float maxBottom = 0f;

        foreach (var section in sections)
        {
            foreach (var el in section.Elements)
            {
                minX = Math.Min(minX, el.X);
                minY = Math.Min(minY, el.Y);
                maxRight = Math.Max(maxRight, el.X + el.Width);
                maxBottom = Math.Max(maxBottom, el.Y + (el.Height ?? 0));
            }
        }

        if (minX == float.MaxValue)
        {
            // Žádné elementy — výchozí okraje
            return (10f, 10f, 10f, 10f);
        }

        // Zaokrouhlit okraje na celé mm, s minimem 2mm
        float marginLeft = Math.Max(MathF.Floor(minX), 2f);
        float marginTop = Math.Max(MathF.Floor(minY), 2f);
        float marginRight = Math.Max(MathF.Floor(pageWidthMm - maxRight), 2f);
        float marginBottom = Math.Max(MathF.Floor(pageHeightMm - maxBottom), 2f);

        // Omezit rozumný rozsah
        marginLeft = Math.Min(marginLeft, 50f);
        marginTop = Math.Min(marginTop, 50f);
        marginRight = Math.Min(marginRight, 50f);
        marginBottom = Math.Min(marginBottom, 50f);

        return (marginTop, marginBottom, marginLeft, marginRight);
    }

    private static void AdjustElementPositions(List<Section> sections, float marginLeft, float marginTop)
    {
        foreach (var section in sections)
        {
            foreach (var el in section.Elements)
            {
                el.X -= marginLeft;
                el.Y -= marginTop;

                // Zajistit kladné pozice
                if (el.X < 0) el.X = 0;
                if (el.Y < 0) el.Y = 0;
            }
        }
    }
}

/// <summary>Režim sloučení grafiky při PDF importu.</summary>
public enum GraphicsMergeMode
{
    /// <summary>Každý grafický prvek jako samostatný element.</summary>
    None,
    /// <summary>Pouze čáry se sloučí do pozadí; obdélníky a obrázky zůstanou jako elementy.</summary>
    LinesOnly,
    /// <summary>Veškerá grafika (čáry, obdélníky, obrázky) se sloučí do jednoho pozadí.</summary>
    All
}
