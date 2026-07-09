using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.PdfImport;

/// <summary>
/// Vytváří specializované JŘ elementy z výsledku ScheduleDetector.
/// Nastavuje vizuální properties (rozměry, fonty, barvy) změřené z PDF.
/// Data (zastávky, odjezdy) přijdou z Caché za běhu — import nastavuje jen layout.
/// </summary>
public static class ScheduleElementBuilder
{
    /// <summary>
    /// Z detekovaného layoutu vytvoří sekci s JŘ elementy + generic TextElement pro nezařazený obsah.
    /// Souřadnice jsou absolutní (v mm od levého horního rohu stránky) — volající posune o marginy.
    /// Pokud je k dispozici AI enrichment, aplikuje mapování textů na DB pole.
    /// </summary>
    public static List<TemplateElement> BuildElements(
        ScheduleDetectionResult detection,
        ExtractedPage page,
        float marginLeft,
        float marginTop,
        EnrichedImportResult? enrichment = null)
    {
        var elements = new List<TemplateElement>();
        int zIndex = 0;

        // 1. StopListElement
        if (detection.StopListRegion is { } stopReg)
        {
            var stopList = new StopListElement
            {
                Id = Guid.NewGuid(),
                X = stopReg.X - marginLeft,
                Y = stopReg.Y - marginTop,
                Width = stopReg.Width,
                Height = stopReg.Height,
                ZIndex = zIndex++,
                DataStyle = new TextStyle
                {
                    FontFamily = "Arial",
                    FontSize = detection.StopFontSizePt
                },
                HeaderStyle = new TextStyle
                {
                    FontFamily = "Arial",
                    FontSize = detection.StopFontSizePt,
                    Bold = true,
                    BackgroundColor = "#FFE0E0E0"
                },
                ShowHeader = true,
                ShowRowSeparators = true,
                DataRowHeight = detection.DataRowHeight,
                Columns = BuildStopColumns(detection),
            };
            elements.Add(stopList);
        }

        // 2. DepartureGridElement
        if (detection.DepartureGridRegion is { } gridReg)
        {
            var grid = new DepartureGridElement
            {
                Id = Guid.NewGuid(),
                X = gridReg.X - marginLeft,
                Y = gridReg.Y - marginTop,
                Width = gridReg.Width,
                Height = gridReg.Height,
                ZIndex = zIndex++,
                HourColumnWidth = detection.HourColumnWidth,
                MinuteWidth = detection.MinuteCellWidth,
                MinuteFontSize = detection.MinuteFontSizePt,
                MinuteBold = true,
                DataRowHeight = detection.DataRowHeight,
                SkupinaDnuHeaderLineHeight = detection.HeaderHeight,
                ShowSkupinaDnuHeader = detection.DayGroups.Count > 0,
                GroupSeparatorWidth = EstimateGroupSeparator(detection),
                DataStyle = new TextStyle
                {
                    FontFamily = "Arial",
                    FontSize = detection.MinuteFontSizePt
                },
                HeaderStyle = new TextStyle
                {
                    FontFamily = "Arial",
                    FontSize = Math.Max(detection.MinuteFontSizePt - 1, 6f),
                    Bold = true,
                    BackgroundColor = "#FFE0E0E0"
                },
                TimeAxisMode = TimeAxisMode.Compact,
            };
            elements.Add(grid);
        }

        // 3. NotesLegendElement — těsně pod DepartureGrid
        if (detection.NotesRegion is { } notesReg)
        {
            float noteLineH = Math.Max(detection.DataRowHeight - 1, 3f);
            // Pozice: těsně pod DepartureGrid
            float gridBottom = detection.DepartureGridRegion is { } gr
                ? gr.Y + gr.Height - marginTop
                : notesReg.Y - marginTop;
            float notesY = gridBottom + 0.5f;

            var notes = new NotesLegendElement
            {
                Id = Guid.NewGuid(),
                X = notesReg.X - marginLeft,
                Y = notesY,
                Width = notesReg.Width,
                // Dynamická výška — print engine dopočítá z počtu poznámek
                Height = null,
                ZIndex = zIndex++,
                NoteFontSize = Math.Max(detection.MinuteFontSizePt - 1, 6f),
                NoteLineHeight = noteLineH,
            };
            elements.Add(notes);
        }

        // 4. Nezařazené texty → generic TextElement (s AI mapováním pokud dostupné)
        foreach (var block in detection.UnmappedTexts)
        {
            float fontHeightMm = block.FontSizePt * 0.3528f;
            int lineCount = block.Text.Count(c => c == '\n') + 1;
            float minHeight = fontHeightMm * 1.4f * lineCount;

            // AI enrichment: nahradit statický text placeholderem pokud AI navrhla mapování
            // Texty s rolí "decoration" přeskočit — jsou to oddělovače (pomlčky apod.)
            string displayText = block.Text;
            if (enrichment is not null)
            {
                var mapping = FindMapping(enrichment.TextMappings, block.Text);
                if (mapping is not null && string.Equals(mapping.Role, "decoration", StringComparison.OrdinalIgnoreCase)
                    && mapping.DbField is null)
                    continue; // přeskočit dekorativní text (pomlčky, oddělovače)

                if (mapping?.PlaceholderText is not null && mapping.Confidence >= 0.7f)
                    displayText = mapping.PlaceholderText;
            }

            // Najít obdélník z PDF, který obklopuje text → rámeček + rozšířit element
            float elX = block.X - marginLeft;
            float elY = Math.Max(0, block.Y - marginTop - fontHeightMm * 0.2f);
            float elW = Math.Max(block.Width, fontHeightMm * 2);
            float elH = Math.Max(block.Height, minHeight);
            string borderColor = "#00FFFFFF";
            float borderWidth = 0f;

            var enclosingRect = FindEnclosingRectangle(page.Rectangles, block);
            if (enclosingRect is not null)
            {
                elX = enclosingRect.X - marginLeft;
                elY = enclosingRect.Y - marginTop;
                elW = enclosingRect.Width;
                elH = enclosingRect.Height;
                borderColor = enclosingRect.StrokeColorHex ?? "#FF000000";
                borderWidth = Math.Clamp(enclosingRect.StrokeWidth, 0.1f, 0.5f);
            }

            elements.Add(new TextElement
            {
                Id = Guid.NewGuid(),
                X = elX,
                Y = Math.Max(0, elY),
                Width = elW,
                Height = elH,
                ZIndex = zIndex++,
                Text = displayText,
                Style = new TextStyle
                {
                    FontFamily = block.FontFamily,
                    FontSize = block.FontSizePt,
                    Bold = block.IsBold,
                    Italic = block.IsItalic,
                    TextColor = block.ColorHex,
                    HAlign = TextHorizontalAlignment.Center,
                    VAlign = TextVerticalAlignment.Middle,
                },
                WordWrap = block.Text.Contains('\n'),
                BorderColor = borderColor,
                BorderWidth = borderWidth,
                BackgroundColor = enclosingRect?.FillColorHex ?? "#00FFFFFF",
            });
        }

        // 5. Nezařazené obrázky → ImageElement (loga, razítka)
        foreach (var img in detection.UnmappedImages)
        {
            elements.Add(new ImageElement
            {
                Id = Guid.NewGuid(),
                X = Math.Max(0, img.X - marginLeft),
                Y = Math.Max(0, img.Y - marginTop),
                Width = img.Width,
                Height = img.Height,
                ZIndex = zIndex++,
                ImageBase64 = img.Base64Png
            });
        }

        // 6. Prázdné obdélníky v hlavičce → ImageElement placeholder (pro loga)
        // Obdélníky nad JŘ oblastí, které neobsahují žádný unmapped text = pravděpodobně logo
        float jrTop = Math.Min(
            detection.StopListRegion?.Y ?? float.MaxValue,
            detection.DepartureGridRegion?.Y ?? float.MaxValue);
        if (jrTop < float.MaxValue)
        {
            var textRects = new HashSet<ExtractedRectangle>(); // obdélníky již použité pro texty
            foreach (var block in detection.UnmappedTexts)
            {
                var encl = FindEnclosingRectangle(page.Rectangles, block);
                if (encl is not null) textRects.Add(encl);
            }

            foreach (var rect in page.Rectangles)
            {
                // Jen obdélníky nad JŘ oblastí
                if (rect.Y + rect.Height > jrTop) continue;
                // Minimální rozumná velikost pro logo/obrázek (5×5mm)
                if (rect.Width < 5f || rect.Height < 5f) continue;
                // Přeskočit obdélníky již přiřazené textu
                if (textRects.Contains(rect)) continue;
                // Přeskočit pokud obdélník obsahuje nějaký text
                bool containsText = detection.UnmappedTexts.Any(t =>
                    t.X >= rect.X - 1f && t.Y >= rect.Y - 1f
                    && t.X + t.Width <= rect.X + rect.Width + 1f
                    && t.Y + t.Height <= rect.Y + rect.Height + 1f);
                if (containsText) continue;

                elements.Add(new ImageElement
                {
                    Id = Guid.NewGuid(),
                    X = Math.Max(0, rect.X - marginLeft),
                    Y = Math.Max(0, rect.Y - marginTop),
                    Width = rect.Width,
                    Height = rect.Height,
                    ZIndex = zIndex++,
                    BorderColor = rect.StrokeColorHex ?? "#FF000000",
                    BorderWidth = Math.Clamp(rect.StrokeWidth, 0.1f, 0.5f),
                });
            }
        }

        // Zajistit kladné pozice
        foreach (var el in elements)
        {
            if (el.X < 0) el.X = 0;
            if (el.Y < 0) el.Y = 0;
        }

        return elements;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<StopColumn> BuildStopColumns(ScheduleDetectionResult detection)
    {
        var columns = new List<StopColumn>();

        if (detection.StopColumnWidths.Count >= 2)
        {
            // Dva sloupce: číslo (jízdní doba) + název zastávky
            columns.Add(new StopColumn
            {
                Key = StopFields.TravelTime,
                Header = "min.",
                Width = detection.StopColumnWidths[0],
                Visible = true,
                Alignment = TextHorizontalAlignment.Right
            });
            columns.Add(new StopColumn
            {
                Key = StopFields.StopName,
                Header = "Zastávky",
                Width = detection.StopColumnWidths[1],
                Visible = true,
                Alignment = TextHorizontalAlignment.Left
            });
        }
        else
        {
            // Jeden sloupec: jen název zastávky
            columns.Add(new StopColumn
            {
                Key = StopFields.StopName,
                Header = "Zastávky",
                Width = detection.StopColumnWidths.FirstOrDefault(50f),
                Visible = true,
                Alignment = TextHorizontalAlignment.Left
            });
        }

        return columns;
    }

    /// <summary>Najde TextMapping pro daný text (exact match nebo obsahuje).</summary>
    private static TextMapping? FindMapping(List<TextMapping> mappings, string originalText)
    {
        // Nejdřív přesná shoda
        var exact = mappings.FirstOrDefault(m =>
            string.Equals(m.OriginalText, originalText, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        // Pak obsahuje (pro případ drobných rozdílů v whitespace)
        return mappings.FirstOrDefault(m =>
            !string.IsNullOrEmpty(m.OriginalText) &&
            originalText.Contains(m.OriginalText, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Najde obdélník z PDF, který obklopuje textový blok (rámeček kolem textu).
    /// Obdélník musí obsahovat celý text s tolerancí a mít viditelný stroke.
    /// </summary>
    private static ExtractedRectangle? FindEnclosingRectangle(
        List<ExtractedRectangle> rectangles, ExtractedTextBlock block)
    {
        const float tolerance = 2f; // mm tolerance — obdélník může být mírně větší než text

        ExtractedRectangle? best = null;
        float bestArea = float.MaxValue;

        foreach (var rect in rectangles)
        {
            // Obdélník musí mít viditelný stroke
            if (rect.StrokeWidth <= 0 && string.IsNullOrEmpty(rect.StrokeColorHex)) continue;

            // Obdélník musí obsahovat text (s tolerancí)
            if (block.X < rect.X - tolerance) continue;
            if (block.Y < rect.Y - tolerance) continue;
            if (block.X + block.Width > rect.X + rect.Width + tolerance) continue;
            if (block.Y + block.Height > rect.Y + rect.Height + tolerance) continue;

            // Obdélník nesmí být příliš velký (max 20× plocha textu) — vyloučit celostránkové rámečky
            float rectArea = rect.Width * rect.Height;
            float textArea = Math.Max(block.Width, 1f) * Math.Max(block.Height, 1f);
            if (rectArea > textArea * 20f) continue;

            // Preferovat nejmenší obklopující obdélník
            if (rectArea < bestArea)
            {
                best = rect;
                bestArea = rectArea;
            }
        }

        return best;
    }

    private static float EstimateGroupSeparator(ScheduleDetectionResult detection)
    {
        if (detection.DayGroups.Count < 2) return 0.5f;

        // Mezera mezi skupinami = gap mezi pravým okrajem jedné a levým okrajem další
        var sorted = detection.DayGroups.OrderBy(g => g.X).ToList();
        var gaps = new List<float>();
        for (int i = 1; i < sorted.Count; i++)
        {
            float gap = sorted[i].X - (sorted[i - 1].X + sorted[i - 1].Width);
            if (gap > 0) gaps.Add(gap);
        }

        return gaps.Count > 0 ? gaps.Average() : 0.5f;
    }
}
