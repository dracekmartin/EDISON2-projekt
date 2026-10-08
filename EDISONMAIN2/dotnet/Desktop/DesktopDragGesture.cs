// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro WinForms color picker).
using Point = System.Windows.Point;

namespace EDISONMAIN2.Desktop;

/// <summary>
/// Klik a tažení položky ve stromu, v Používaných a na ploše. Stisk si zapamatuje bod, položku k tažení
/// a klíč kliku, tah přes systémový práh drag spustí právě jednou. Klik platí, jen když stisk i uvolnění
/// padly na položku s týmž klíčem a mezi nimi nezačalo tažení. Samotné <c>DoDragDrop</c> volá view
/// (potřebuje zdrojový element a formát).
/// </summary>
internal sealed class DesktopDragGesture
{
    /// <summary>
    /// Myš je jedna, panely proto sdílejí jeden stisk. Jinak by uvolnění po tažení zrušeném nad jiným
    /// panelem ten panel vzal jako klik.
    /// </summary>
    public static DesktopDragGesture Shared { get; } = new();

    private Point _start;
    private object? _candidate;
    private object? _pressedOn;

    /// <summary>
    /// Stisk levého tlačítka kdekoli v okně, volá ho kořen okna dřív, než stisk dojde k položce. Stisk mimo
    /// položky tak zahodí ten předchozí, jinak by tah z prázdného místa táhl dříve stisknutou položku.
    /// </summary>
    public void Reset() => (_candidate, _pressedOn) = (null, null);

    /// <summary>
    /// Stisk levého tlačítka na položce. <paramref name="clickKey"/> jsou data položky, ne prvek: přenačtení
    /// plochy vyrobí nové prvky i mezi stiskem a uvolněním. U vnořených prvků (tunneling) volá i rodič,
    /// vyhraje poslední, tedy nejvnitřnější.
    /// </summary>
    public void Press(Point position, object? dragItem, object? clickKey)
    {
        _start = position;
        _candidate = dragItem;
        _pressedOn = clickKey;
    }

    /// <summary>Pohyb myši: položka k tažení, když se má drag spustit, jinak <c>null</c>.</summary>
    public T? Move<T>(Point position, bool leftPressed) where T : class
    {
        if (!leftPressed || _candidate is not T item) return null;
        if (!DesktopDragFormats.ThresholdExceeded(_start, position)) return null;
        Reset();
        return item;
    }

    /// <summary>
    /// Uvolnění tlačítka na položce: <c>true</c> = klik, provést akci. Uvolnění při dropu WPF nedoručí
    /// (vezme si ho OLE smyčka <c>DoDragDrop</c>). Doručí ho po tažení zrušeném Esc nebo druhým
    /// tlačítkem, a to i položce jiného panelu.
    /// </summary>
    public bool TakeClick(object? clickKey)
    {
        var click = _pressedOn is not null && _pressedOn.Equals(clickKey);
        Reset();
        return click;
    }
}
