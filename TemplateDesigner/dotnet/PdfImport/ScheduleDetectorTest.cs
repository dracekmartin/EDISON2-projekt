using UglyToad.PdfPig;

namespace TemplateDesigner.PdfImport;

/// <summary>
/// Jednorázový test detekce JŘ na reálných PDF.
/// Spouští se ručně: ScheduleDetectorTest.Run() v Immediate Window nebo z debug kódu.
/// </summary>
public static class ScheduleDetectorTest
{
    public static string Run(string pdfPath)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== ScheduleDetector Test: {System.IO.Path.GetFileName(pdfPath)} ===\n");

        using var doc = PdfDocument.Open(pdfPath);

        for (int pageNum = 1; pageNum <= doc.NumberOfPages; pageNum++)
        {
            var page = PdfPageExtractor.Extract(doc, pageNum);
            sb.AppendLine($"--- Page {pageNum} ({page.WidthMm:0.#} x {page.HeightMm:0.#} mm) ---");
            sb.AppendLine($"  Words: {page.Words.Count}, Lines: {page.Lines.Count}, Rects: {page.Rectangles.Count}, Images: {page.Images.Count}");

            // Debug: ukaž všechna slova, abychom viděli co extrakce vytáhla
            sb.AppendLine($"\n  ALL WORDS (first 50):");
            foreach (var w in page.Words.Take(50))
                sb.AppendLine($"    [{w.X:0.#},{w.Y:0.#} {w.Width:0.#}x{w.Height:0.#}] \"{w.Text}\" font={w.FontFamily} {w.FontSizePt:0.#}pt {(w.IsBold?"B":"")}{(w.IsItalic?"I":"")}");

            // Debug: kolik slov je číslo 0-59 s fontem <=14pt?
            var minuteCandidates = page.Words
                .Where(w => w.Text.Length <= 2 && int.TryParse(w.Text, out int n) && n >= 0 && n <= 59 && w.FontSizePt <= 14)
                .ToList();
            sb.AppendLine($"\n  MINUTE CANDIDATES (0-59, <=14pt): {minuteCandidates.Count}");
            foreach (var w in minuteCandidates.Take(30))
                sb.AppendLine($"    [{w.X:0.#},{w.Y:0.#}] \"{w.Text}\" {w.FontSizePt:0.#}pt");

            // Spusť detekci
            var result = ScheduleDetector.DetectZastavkovyJr(page);

            if (result is null)
            {
                sb.AppendLine("  DETECTION: FAILED (null result)\n");
                continue;
            }

            sb.AppendLine($"  DETECTION: confidence={result.Confidence:0.##}");
            sb.AppendLine($"  StopList:       {FormatRegion(result.StopListRegion)}");
            sb.AppendLine($"  DepartureGrid:  {FormatRegion(result.DepartureGridRegion)}");
            sb.AppendLine($"  Notes:          {FormatRegion(result.NotesRegion)}");
            sb.AppendLine($"  HourColumnW:    {result.HourColumnWidth:0.#} mm");
            sb.AppendLine($"  MinuteCellW:    {result.MinuteCellWidth:0.#} mm");
            sb.AppendLine($"  DataRowHeight:  {result.DataRowHeight:0.#} mm");
            sb.AppendLine($"  HeaderHeight:   {result.HeaderHeight:0.#} mm");
            sb.AppendLine($"  MinuteFontPt:   {result.MinuteFontSizePt:0.#}");
            sb.AppendLine($"  StopFontPt:     {result.StopFontSizePt:0.#}");
            sb.AppendLine($"  StopColWidths:  [{string.Join(", ", result.StopColumnWidths.Select(w => w.ToString("0.#")))}]");
            sb.AppendLine($"  DayGroups:      {result.DayGroups.Count}");
            foreach (var g in result.DayGroups)
                sb.AppendLine($"    \"{g.HeaderText}\" x={g.X:0.#} w={g.Width:0.#}");
            sb.AppendLine($"  UnmappedTexts:  {result.UnmappedTexts.Count}");
            foreach (var t in result.UnmappedTexts.Take(10))
                sb.AppendLine($"    [{t.X:0.#},{t.Y:0.#}] \"{t.Text.Replace("\n", "\\n")}\"");
            sb.AppendLine($"  UnmappedImages: {result.UnmappedImages.Count}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Spustí detekci + plný import s debug logem a uloží výsledky vedle PDF.
    /// </summary>
    public static string RunFullImport(string pdfPath)
    {
        var converter = new PdfToTemplateConverter
        {
            EnableDebug = true,
            ScheduleImportType = ScheduleImportType.ZastavkovyJrMhd
        };

        var template = converter.Convert(pdfPath);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== Full Import: {System.IO.Path.GetFileName(pdfPath)} ===\n");
        sb.AppendLine($"Template: {template.Name}");
        sb.AppendLine($"PageSize: {template.PageSize.Width:0.#} x {template.PageSize.Height:0.#} mm");
        sb.AppendLine($"Margins: T={template.PageSize.MarginTop:0.#} B={template.PageSize.MarginBottom:0.#} L={template.PageSize.MarginLeft:0.#} R={template.PageSize.MarginRight:0.#}");
        sb.AppendLine($"Sections: {template.Sections.Count}");

        foreach (var section in template.Sections)
        {
            sb.AppendLine($"\n  Section: \"{section.Name}\" height={section.DesignerHeight:0.#}mm elements={section.Elements.Count}");
            foreach (var el in section.Elements)
            {
                string typeName = el.GetType().Name;
                sb.AppendLine($"    {typeName} [{el.X:0.#},{el.Y:0.#} {el.Width:0.#}x{el.Height:0.#}] z={el.ZIndex}");
            }
        }

        // Ulož debug log
        if (converter.DebugLog is not null)
        {
            var logPath = System.IO.Path.ChangeExtension(pdfPath, ".schedule-detect.txt");
            System.IO.File.WriteAllText(logPath, converter.DebugLog);
            sb.AppendLine($"\nDebug log saved: {logPath}");
        }

        return sb.ToString();
    }

    private static string FormatRegion(DetectedRegion? r) =>
        r is null ? "(none)" : $"[{r.X:0.#},{r.Y:0.#} {r.Width:0.#}x{r.Height:0.#}]";
}
