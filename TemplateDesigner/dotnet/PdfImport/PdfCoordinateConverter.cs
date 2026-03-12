namespace TemplateDesigner.PdfImport;

/// <summary>
/// Konverze PDF souřadnic (body, bottom-left origin) na šablonové souřadnice (mm, top-left origin).
/// </summary>
public static class PdfCoordinateConverter
{
    private const float PtToMm = 25.4f / 72f;

    /// <summary>PDF points → mm.</summary>
    public static float ToMm(double pt) => (float)(pt * PtToMm);

    /// <summary>PDF bottom-left Y → top-left Y (mm).</summary>
    public static float FlipY(double pdfY, double pageHeightPt) => ToMm(pageHeightPt - pdfY);

    /// <summary>
    /// PDF bounding box (bottom-left origin, pt) → template bounds (top-left origin, mm).
    /// </summary>
    public static (float X, float Y, float Width, float Height) ToTemplateBounds(
        double left, double bottom, double right, double top, double pageHeightPt)
    {
        float x = ToMm(left);
        float y = FlipY(top, pageHeightPt); // top edge of bbox in top-left coords
        float w = ToMm(right - left);
        float h = ToMm(top - bottom);
        return (x, y, Math.Max(w, 0.1f), Math.Max(h, 0.1f));
    }
}
