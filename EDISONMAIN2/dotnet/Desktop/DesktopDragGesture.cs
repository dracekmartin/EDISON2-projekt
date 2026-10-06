// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro WinForms color picker).
using Point = System.Windows.Point;

namespace EDISONMAIN2.Desktop;

/// <summary>
/// Gesto tažení položky ze stromu nebo dlaždice plochy: stisk si zapamatuje bod a položku, tah přes
/// systémový práh drag spustí právě jednou a klik, který po tažení přijde, se nespustí jako akce.
/// Samotné <c>DoDragDrop</c> volá view (potřebuje zdrojový element a formát).
/// </summary>
internal sealed class DesktopDragGesture<T> where T : class
{
    private Point _start;
    private T? _candidate;
    private bool _suppressClick;

    /// <summary>
    /// Stisk levého tlačítka. U vnořených prvků (tunneling) volá i rodič s <c>null</c> - vyhraje
    /// poslední, tedy nejvnitřnější položka.
    /// </summary>
    public void Press(Point position, T? item)
    {
        _start = position;
        _candidate = item;
    }

    /// <summary>Pohyb myši: položka k tažení, když se má drag spustit, jinak <c>null</c>.</summary>
    public T? Move(Point position, bool leftPressed)
    {
        if (!leftPressed || _candidate is null) return null;
        if (!DesktopDragFormats.ThresholdExceeded(_start, position)) return null;
        var item = _candidate;
        _candidate = null;
        _suppressClick = true;
        return item;
    }

    /// <summary>Uvolnění tlačítka: <c>false</c> = právě skončilo tažení, klik se nemá provést.</summary>
    public bool TakeClick()
    {
        if (!_suppressClick) return true;
        _suppressClick = false;
        return false;
    }
}
