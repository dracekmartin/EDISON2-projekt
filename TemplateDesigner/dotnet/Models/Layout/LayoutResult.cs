namespace TemplateDesigner.Models.Layout;

/// <summary>
/// Výstup PageLayoutEngine — kompletní rozložení šablony na stránky.
/// Renderer (preview/PDF) jen kreslí hotový layout.
/// </summary>
public class LayoutResult
{
    public List<Page> Pages { get; set; } = [];
    public int TotalPages => Pages.Count;
}

/// <summary>Jedna stránka výstupu.</summary>
public class Page
{
    public int PageNumber { get; set; }
    public List<PlacedSection> Sections { get; set; } = [];
}

/// <summary>
/// Sekce umístěná na konkrétní pozici na stránce.
/// Renderer kreslí elementy sekce s origin Y = this.Y.
/// </summary>
public class PlacedSection
{
    /// <summary>Odkaz na originální sekci šablony.</summary>
    public required Section Source { get; init; }

    /// <summary>Absolutní Y pozice na stránce v mm (v rámci printable area, relativně k MarginTop).</summary>
    public float Y { get; set; }

    /// <summary>Skutečná výška sekce v mm (vypočítaná enginem).</summary>
    public float Height { get; set; }

    /// <summary>Pro SplitAcrossPages: ořez shora v mm (kolik sekce je nad touto stránkou).</summary>
    public float? ClipTop { get; set; }

    /// <summary>Pro SplitAcrossPages: ořez zdola v mm.</summary>
    public float? ClipBottom { get; set; }

    /// <summary>Data pro resolvování placeholderů (obohacená o PAGE/TOTAL_PAGES).</summary>
    public DataContext? Data { get; set; }
}
