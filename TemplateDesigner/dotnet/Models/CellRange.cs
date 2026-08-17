namespace TemplateDesigner.Models;

/// <summary>
/// Obdélníkový výběr buněk v tabulce - **kotva** (kde výběr začal) a **aktivní buňka** (kam se
/// došlo). Drží se takhle, a ne jako hotový obdélník, protože rozšiřování (Shift+šipky, tažení)
/// musí kotvu nechat na místě; z dvojice se obdélník dopočítá.
///
/// Aktivní buňka je ta, do které se píše a podle které se vkládají řádky a sloupce - ve výběru
/// zůstává rozpoznatelná, stejně jako v Excelu.
/// </summary>
public readonly record struct CellRange(int AnchorRow, int AnchorCol, int ActiveRow, int ActiveCol)
{
    public static CellRange Single(int row, int col) => new(row, col, row, col);

    public int Top    => Math.Min(AnchorRow, ActiveRow);
    public int Bottom => Math.Max(AnchorRow, ActiveRow);
    public int Left   => Math.Min(AnchorCol, ActiveCol);
    public int Right  => Math.Max(AnchorCol, ActiveCol);

    public int RowCount    => Bottom - Top + 1;
    public int ColumnCount => Right - Left + 1;
    public int Count       => RowCount * ColumnCount;

    public bool IsSingle => Count == 1;

    public bool Contains(int row, int col) =>
        row >= Top && row <= Bottom && col >= Left && col <= Right;

    /// <summary>Buňky výběru po řádcích - v tomhle pořadí se i kopírují do schránky.</summary>
    public IEnumerable<(int Row, int Col)> Cells()
    {
        for (int row = Top; row <= Bottom; row++)
            for (int col = Left; col <= Right; col++)
                yield return (row, col);
    }

    /// <summary>Posun na jinou buňku (šipka, klik) - výběr se smrskne na ni.</summary>
    public static CellRange MoveTo(int row, int col) => Single(row, col);

    /// <summary>Rozšíření výběru (Shift+šipka, Shift+klik, tažení) - kotva zůstává.</summary>
    public CellRange ExtendTo(int row, int col) => this with { ActiveRow = row, ActiveCol = col };

    /// <summary>
    /// Celý řádek / sloupec / tabulka. Aktivní buňka jde na **začátek** vybraného úseku, ne tam,
    /// kde stála: obdélník je daný kotvou a aktivní buňkou, takže aktivní buňka musí být jeho roh -
    /// uprostřed výběru ji držet nelze. Začátek je přirozený, protože se od něj píše i čte.
    /// </summary>
    public CellRange WholeRow(int columns) => new(ActiveRow, columns - 1, ActiveRow, 0);
    public CellRange WholeColumn(int rows) => new(rows - 1, ActiveCol, 0, ActiveCol);
    public static CellRange Whole(int rows, int columns) => new(rows - 1, columns - 1, 0, 0);

    /// <summary>
    /// Srovná výběr do mřížky. Ctrl+Z a Ctrl+Y jdou mimo náš kód, takže po vrácení vloženého
    /// řádku může výběr ukazovat mimo - a tam už by psaní ani mazání nic nedělalo.
    /// </summary>
    public CellRange Clamp(int rows, int columns) => new(
        Math.Clamp(AnchorRow, 0, Math.Max(0, rows - 1)),
        Math.Clamp(AnchorCol, 0, Math.Max(0, columns - 1)),
        Math.Clamp(ActiveRow, 0, Math.Max(0, rows - 1)),
        Math.Clamp(ActiveCol, 0, Math.Max(0, columns - 1)));
}
