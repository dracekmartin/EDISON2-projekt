namespace TemplateDesigner.Models.Layout;

/// <summary>
/// Element, který umí být stránkován po <b>řádcích</b> (svislá osa) — engine ho řídí místo dnešního
/// pixel-ořezu (viz `r2-partial-fit-kontrakt.md`). Cíl: dlouhá tabulka se láme jen mezi řádky
/// (nikdy vejpůl) a záhlaví se opakuje na každé pokračovací stránce.
///
/// „Jednotka" (row unit) definuje element: DynamicTable = 1 datový řádek (souhrnné řádky = koncové
/// jednotky); StopList = 1 zastávka; LineTable = zastávka + její vložené přípojné řádky (drží pohromadě).
/// Měřicí metody vrací <b>mm</b> (žádný SkiaSharp) → engine zůstává čistá logika.
/// </summary>
public interface IRowPaginatedElement
{
    /// <summary>Počet nedělitelných řádkových jednotek pro daná data.</summary>
    int RowUnitCount(DataContext? data);

    /// <summary>Výška opakovaného záhlaví v mm (0 = element záhlaví neopakuje).</summary>
    float HeaderBandHeightMm(DataContext? data);

    /// <summary>Výška jedné řádkové jednotky v mm.</summary>
    float RowUnitHeightMm(DataContext? data, int unitIndex);

    /// <summary>
    /// Vykreslí okno jednotek [<paramref name="startUnit"/>, <paramref name="endUnitExclusive"/>).
    /// <paramref name="drawHeader"/> = má-li se navrch zopakovat záhlaví (na pokračovací stránce true).
    /// Kreslí relativně k `ctx` originu sekce (od Y elementu), engine/renderer nastaví pozici na stránce.
    /// </summary>
    void RenderRowWindow(RenderContext ctx, int startUnit, int endUnitExclusive, bool drawHeader);
}
