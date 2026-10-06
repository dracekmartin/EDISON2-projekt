using System.Windows;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro WinForms color picker).
using Point = System.Windows.Point;

namespace EDISONMAIN2.Desktop;

/// <summary>
/// DataObject formáty pro drag&drop na plochu. In-process (nese přímo instance).
/// Dvě rozlišené operace: přetažení modulu ze stromu (přidání) vs. přeuspořádání dlaždic.
/// </summary>
internal static class DesktopDragFormats
{
    /// <summary>Modul (<c>ExecutableModule</c>) tažený ze stromu na plochu → přidání.</summary>
    public const string FromTree = "EDISON.Module.FromTree";

    /// <summary>Dlaždice (<c>Shortcut</c>) tažená uvnitř plochy → přeuspořádání.</summary>
    public const string ReorderTile = "EDISON.Shortcut.Reorder";

    /// <summary>Systémový práh odliší drag od kliku - sdílené stromem i plochou.</summary>
    internal static bool ThresholdExceeded(Point start, Point current) =>
        Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance
        || Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;
}
