using System.Text.Json.Serialization;

namespace TemplateDesigner.Models;

public class PageSize
{
    public float Width { get; set; } = 210f;       // mm
    public float Height { get; set; } = 297f;      // mm
    // Margins default = 0 (full-bleed design jako výchozí).
    // Reálné tiskárny mají vlastní unprintable area (3-5mm), tak je to safe;
    // user si může margins nastavit přes PageSize editor pokud je chce jako safe zone.
    public float MarginTop { get; set; } = 0f;
    public float MarginBottom { get; set; } = 0f;
    public float MarginLeft { get; set; } = 0f;
    public float MarginRight { get; set; } = 0f;

    [JsonIgnore]
    public float PrintableWidth => Width - MarginLeft - MarginRight;

    [JsonIgnore]
    public float PrintableHeight => Height - MarginTop - MarginBottom;

    // Předdefinované formáty
    public static PageSize A4Portrait => new() { Width = 210, Height = 297 };
    public static PageSize A4Landscape => new() { Width = 297, Height = 210 };
    public static PageSize A3Portrait => new() { Width = 297, Height = 420 };
    public static PageSize A3Landscape => new() { Width = 420, Height = 297 };
}
