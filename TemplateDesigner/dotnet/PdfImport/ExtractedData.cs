using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.PdfImport;

/// <summary>Jedno slovo extrahované z PDF.</summary>
public record ExtractedWord(
    string Text, float X, float Y, float Width, float Height,
    string FontFamily, float FontSizePt, bool IsBold, bool IsItalic, string ColorHex);

/// <summary>Seskupený textový blok (jedno nebo více slov/řádků).</summary>
public record ExtractedTextBlock(
    string Text, float X, float Y, float Width, float Height,
    string FontFamily, float FontSizePt, bool IsBold, bool IsItalic, string ColorHex);

/// <summary>Čára extrahovaná z PDF (horizontální nebo vertikální).</summary>
public record ExtractedLine(
    float X1, float Y1, float X2, float Y2, float StrokeWidth, string ColorHex,
    LineStyle Style = LineStyle.Solid);

/// <summary>Obdélník extrahovaný z PDF.</summary>
public record ExtractedRectangle(
    float X, float Y, float Width, float Height,
    string? FillColorHex, string? StrokeColorHex, float StrokeWidth);

/// <summary>Obrázek extrahovaný z PDF (logo, razítko, fotka).</summary>
public record ExtractedImage(
    float X, float Y, float Width, float Height, string Base64Png);

/// <summary>Surová data extrahovaná z jedné stránky PDF.</summary>
public class ExtractedPage
{
    public float WidthMm { get; init; }
    public float HeightMm { get; init; }
    public List<ExtractedWord> Words { get; init; } = [];
    public List<ExtractedLine> Lines { get; set; } = [];
    public List<ExtractedRectangle> Rectangles { get; init; } = [];
    public List<ExtractedImage> Images { get; init; } = [];
}
