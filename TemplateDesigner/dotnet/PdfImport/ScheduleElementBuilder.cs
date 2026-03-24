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
    /// </summary>
    public static List<TemplateElement> BuildElements(
        ScheduleDetectionResult detection,
        ExtractedPage page,
        float marginLeft,
        float marginTop)
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
                DisplayMode = ScheduleDisplayMode.MinutesOnly,
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

        // 3. NotesLegendElement
        if (detection.NotesRegion is { } notesReg)
        {
            var notes = new NotesLegendElement
            {
                Id = Guid.NewGuid(),
                X = notesReg.X - marginLeft,
                Y = notesReg.Y - marginTop,
                Width = notesReg.Width,
                Height = notesReg.Height,
                ZIndex = zIndex++,
                NoteFontSize = Math.Max(detection.MinuteFontSizePt - 1, 6f),
                NoteLineHeight = Math.Max(detection.DataRowHeight - 1, 3f),
            };
            elements.Add(notes);
        }

        // 4. Nezařazené texty → generic TextElement
        foreach (var block in detection.UnmappedTexts)
        {
            float fontHeightMm = block.FontSizePt * 0.3528f;
            int lineCount = block.Text.Count(c => c == '\n') + 1;
            float minHeight = fontHeightMm * 1.4f * lineCount;

            elements.Add(new TextElement
            {
                Id = Guid.NewGuid(),
                X = block.X - marginLeft,
                Y = Math.Max(0, block.Y - marginTop - fontHeightMm * 0.2f),
                Width = Math.Max(block.Width, fontHeightMm * 2),
                Height = Math.Max(block.Height, minHeight),
                ZIndex = zIndex++,
                Text = block.Text,
                Style = new TextStyle
                {
                    FontFamily = block.FontFamily,
                    FontSize = block.FontSizePt,
                    Bold = block.IsBold,
                    Italic = block.IsItalic,
                    TextColor = block.ColorHex,
                },
                WordWrap = block.Text.Contains('\n')
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
                Key = "TravelTime",
                Header = "min.",
                Width = detection.StopColumnWidths[0],
                Visible = true,
                Alignment = TextHorizontalAlignment.Right
            });
            columns.Add(new StopColumn
            {
                Key = "StopName",
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
                Key = "StopName",
                Header = "Zastávky",
                Width = detection.StopColumnWidths.FirstOrDefault(50f),
                Visible = true,
                Alignment = TextHorizontalAlignment.Left
            });
        }

        return columns;
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
