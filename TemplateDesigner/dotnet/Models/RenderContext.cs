using SkiaSharp;

namespace TemplateDesigner.Models;

/// <summary>
/// Předává renderovacím metodám vše potřebné: canvas, měřítko, data a pozici sekce.
/// Elementy pracují v mm relativně k sekci - RenderContext zajišťuje přepočet na px.
/// </summary>
public class RenderContext
{
    public SKCanvas Canvas { get; }

    /// <summary>Koeficient pro převod mm → pixely (závislý na DPI a zoomu).</summary>
    public float MmToPx { get; }

    /// <summary>Data pro renderování. Null v designeru, mock data v preview, reálná data v print enginu.</summary>
    public DataContext? Data { get; }

    /// <summary>Absolutní X pozice levého horního rohu aktuální sekce na canvasu (px).</summary>
    public float SectionOriginX { get; init; }

    /// <summary>Absolutní Y pozice levého horního rohu aktuální sekce na canvasu (px).</summary>
    public float SectionOriginY { get; init; }

    /// <summary>Kde na sekci začíná pás viditelný na téhle stránce (mm). 0 = sekce začíná tady.</summary>
    public float ClipTopMm { get; init; }

    /// <summary>Výška pásu sekce viditelného na téhle stránce (mm; 0 = neomezeno).</summary>
    /// <remarks>Obsah ořezává plátno samo, tohle je pro <b>rámečky</b>: kreslí se mimo ořez sekce
    /// (aby nebyly půlené na její hraně), takže si prvek s vlastním rámečkem musí pás pohlídat sám
    /// - jinak nakreslí i rámeček části, která na téhle stránce vůbec není.</remarks>
    public float ViditelnyPasMm { get; init; }

    /// <summary>Výška hlavičky, kterou má prvek na téhle stránce zopakovat (mm; 0 = žádnou).
    /// Nenulová jen tam, kde řez padl doprostřed nedělitelného dílu.</summary>
    public float OpakovanaHlavickaMm { get; init; }

    /// <summary>Sběrač oříznutých buněk pro hlášku ve stavovém řádku. Null = nesbírá se (tisk, testy).</summary>
    public Rendering.ClippedCells? ClippedCells { get; init; }

    /// <summary>
    /// Kreslí se na rastr, kde má smysl srovnávat čáry na celý pixel?
    /// </summary>
    /// <remarks>
    /// ⚠️ Jen pro obrazovku a PNG. V PDF je jednotkou <b>bod</b> (0,35 mm), takže srovnání na
    /// „pixel" by čáru posunulo o desetiny milimetru a tabulka by se rozešla s tím, co je
    /// v náhledu (nález Jan 22. 9. na exportu 330530).
    /// </remarks>
    public bool SrovnatCaryNaPixel { get; init; }

    public RenderContext(SKCanvas canvas, float mmToPx, DataContext? data = null)
    {
        Canvas = canvas;
        MmToPx = mmToPx;
        Data = data;
    }

    // --- Pomocné metody pro převod souřadnic ---

    /// <summary>Převede mm na pixely.</summary>
    public float Px(float mm) => mm * MmToPx;

    /// <summary>Převede souřadnice elementu (relativní k sekci, v mm) na absolutní px pozici na canvasu.</summary>
    public SKPoint ToCanvas(float xMm, float yMm) =>
        new(SectionOriginX + xMm * MmToPx, SectionOriginY + yMm * MmToPx);

    /// <summary>Převede obdélník elementu (relativní k sekci, v mm) na absolutní SKRect v px.</summary>
    public SKRect ToCanvasRect(float xMm, float yMm, float wMm, float hMm) =>
        new(SectionOriginX + xMm * MmToPx,
            SectionOriginY + yMm * MmToPx,
            SectionOriginX + (xMm + wMm) * MmToPx,
            SectionOriginY + (yMm + hMm) * MmToPx);

    /// <summary>Vrátí nový kontext s posunutým originem sekce (px).</summary>
    public RenderContext WithSectionOrigin(float xPx, float yPx) =>
        new(Canvas, MmToPx, Data) { SectionOriginX = xPx, SectionOriginY = yPx, ClippedCells = ClippedCells };
}
