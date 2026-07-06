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

    // ── Řádkové stránkování (R2) — sekce s jedním IRowPaginatedElement ──
    // Když má hodnotu, renderer volá element.RenderRowWindow(StartRowUnit, EndRowUnit, RepeatHeader)
    // místo pixel-ořezu (ClipTop/ClipBottom). Zlom po celých řádcích + opakované záhlaví.

    /// <summary>První řádková jednotka na této stránce (včetně). Null = klasický pixel-ořez.</summary>
    public int? StartRowUnit { get; set; }

    /// <summary>Poslední řádková jednotka na této stránce (exkluzivně).</summary>
    public int? EndRowUnit { get; set; }

    /// <summary>Zopakovat záhlaví navrch (na pokračovacích stránkách true).</summary>
    public bool RepeatHeader { get; set; }

    /// <summary>Data pro resolvování placeholderů (obohacená o PAGE/TOTAL_PAGES).</summary>
    public DataContext? Data { get; set; }
}
