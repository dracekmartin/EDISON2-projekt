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

    /// <summary>Typ JŘ importu - uživatel volí apriorně. None = obecný import.</summary>
    public ScheduleImportType ScheduleImportType { get; set; } = ScheduleImportType.None;

    /// <summary>Výsledek detekce JŘ (pro debug/preview). Null pokud nebyla detekce.</summary>
    public ScheduleDetectionResult? LastDetectionResult { get; private set; }

    /// <summary>Extrahované stránky z posledního importu (pro AI enrichment).</summary>
    public List<ExtractedPage> LastExtractedPages { get; private set; } = [];

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

            // Pokus o detekci JŘ struktury (pokud uživatel zvolil typ)
            if (ScheduleImportType == ScheduleImportType.ZastavkovyJrMhd)
            {
                var detection = ScheduleDetector.DetectZastavkovyJr(extracted);
                LastDetectionResult = detection;

                if (detection is not null && detection.Confidence >= 0.5f)
                {
                    if (log is not null)
                    {
                        log.AppendLine($"\n--- SCHEDULE DETECTION (confidence={detection.Confidence:0.##}) ---");
                        log.AppendLine($"  StopList: {FormatRegion(detection.StopListRegion)}");
                        log.AppendLine($"  DepartureGrid: {FormatRegion(detection.DepartureGridRegion)}");
                        log.AppendLine($"  Notes: {FormatRegion(detection.NotesRegion)}");
                        log.AppendLine($"  DayGroups: {detection.DayGroups.Count}");
                        foreach (var g in detection.DayGroups)
                            log.AppendLine($"    \"{g.HeaderText}\" x={g.X:0.#} w={g.Width:0.#}");
                        log.AppendLine($"  HourColW={detection.HourColumnWidth:0.#} MinuteW={detection.MinuteCellWidth:0.#} RowH={detection.DataRowHeight:0.#}");
                        log.AppendLine($"  MinuteFontPt={detection.MinuteFontSizePt:0.#} StopFontPt={detection.StopFontSizePt:0.#}");
                        log.AppendLine($"  StopColWidths=[{string.Join(", ", detection.StopColumnWidths.Select(w => w.ToString("0.#")))}]");
                        log.AppendLine($"  UnmappedTexts={detection.UnmappedTexts.Count} UnmappedImages={detection.UnmappedImages.Count}");
                    }

                    // Použij schedule builder místo generic BuildSection
                    // Enrichment se aplikuje později ve ViewModelu (ApplyEnrichment) - zde ještě není k dispozici
                    var scheduleElements = ScheduleElementBuilder.BuildElements(
                        detection, extracted, 0, 0); // marginy se aplikují později v AdjustElementPositions

                    var schedSection = new Section
                    {
                        Id = Guid.NewGuid(),
                        Name = $"JŘ - Strana {pageNum}",
                        PrintBehavior = SectionPrintBehavior.Once,
                        OverflowBehavior = SectionOverflowBehavior.KeepTogether,
                        Elements = scheduleElements
                    };
                    sections.Add(schedSection);
                    extractedPages.Add(extracted);
                    continue;
                }

                if (log is not null)
                    log.AppendLine($"\n--- SCHEDULE DETECTION FAILED (confidence={detection?.Confidence:0.##}) → fallback to generic ---");
            }

            var section = BuildSection(pageNum, extracted, textBlocks, pageWidthMm, pageHeightMm);
            sections.Add(section);
            extractedPages.Add(extracted);
        }

        DebugLog = log?.ToString();
        LastExtractedPages = extractedPages;

        // Detekce okrajů ze všech elementů + JŘ detekce (pro správný odhad spodního okraje)
        var (marginTop, marginBottom, marginLeft, marginRight) =
            DetectMargins(sections, pageWidthMm, pageHeightMm, LastDetectionResult);

        // Posunout elementy relativně k okrajům
        AdjustElementPositions(sections, marginLeft, marginTop);

        // Výška sekcí = výška stránky minus okraje (přesně = tisknutelná oblast)
        float contentHeight = pageHeightMm - marginTop - marginBottom;
        foreach (var section in sections)
            section.DesignerHeight = Math.Max(contentHeight, 10f);

        // Automatické skupiny podle typu elementu (po merge, kdy jsou všechny elementy na místě)
        AssignAutoGroups(sections);

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
        List<ExtractedTextBlock> textBlocks, float pageWidthMm, float pageHeightMm)
    {
        var elements = new List<TemplateElement>();
        int zIndex = 0;

        // Každý grafický prvek předlohy jako vlastní prvek šablony - čáry se do obrázku neslévají
        // (18. 8.): zamčený rastr nešel upravit a v tisku byl rozmazaný proti zbytku.
        BuildGraphicElements(page, elements, ref zIndex);

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
            // (rect.Top + fontSizePx), ale ascent fontu je ~80% fontSize - vzniká mezera
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
                Style = new TextStyle
                {
                    FontFamily = block.FontFamily,
                    FontSize = block.FontSizePt,
                    Bold = block.IsBold,
                    Italic = block.IsItalic,
                    HAlign = alignment,
                    TextColor = block.ColorHex,
                },
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

    /// <summary>Vytvoří jednotlivé grafické elementy (obdélníky, obrázky, čáry) - původní režim.</summary>
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
                BackgroundColor = rect.FillColorHex ?? Transparent,
                BorderColor = rect.StrokeColorHex ?? Black,
                BorderWidth = Math.Clamp(rect.StrokeWidth, 0.1f, 0.5f)
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
    private static void AssignAutoGroups(List<Section> sections)
    {
        foreach (var section in sections)
        {
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
    }

    private static string FormatRegion(DetectedRegion? r) =>
        r is null ? "(none)" : $"[{r.X:0.#},{r.Y:0.#} {r.Width:0.#}x{r.Height:0.#}]";

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
        List<Section> sections, float pageWidthMm, float pageHeightMm,
        ScheduleDetectionResult? detection = null)
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

        // JŘ detekce: zahrnout detekované regiony (Notes má dynamickou výšku,
        // ale detekce ví kde v PDF obsah končí)
        if (detection is not null)
        {
            if (detection.NotesRegion is { } notes)
                maxBottom = Math.Max(maxBottom, notes.Y + notes.Height);
            if (detection.DepartureGridRegion is { } grid)
                maxBottom = Math.Max(maxBottom, grid.Y + grid.Height);
            if (detection.StopListRegion is { } stops)
                maxBottom = Math.Max(maxBottom, stops.Y + stops.Height);
        }

        if (minX == float.MaxValue)
        {
            // Žádné elementy - výchozí okraje
            return (10f, 10f, 10f, 10f);
        }

        // Okraje = vzdálenost od okraje stránky k nejbližšímu elementu
        // Mírný epsilon (0.1mm) dovnitř, aby elementy po shift spolehlivě vešly do section
        float marginLeft = Math.Max(minX - 0.1f, 0.5f);
        float marginTop = Math.Max(minY - 0.1f, 0.5f);
        float marginRight = Math.Max(pageWidthMm - maxRight - 0.1f, 0.5f);
        float marginBottom = Math.Max(pageHeightMm - maxBottom - 0.1f, 0.5f);

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
