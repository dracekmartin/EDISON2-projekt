using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Rendering;

/// <summary>
/// Sdílené řádkové tabulkové jádro (R1) — algoritmy společné všem řádkovým tabulkovým elementům
/// (StopList, DynamicTable, LineTable). Stojí NAD <see cref="CellRenderer"/> / <see cref="ColumnRenderer"/>.
///
/// **Záměrně nesjednocuje datovou třídu sloupce** — StopColumn i TableColumn zůstávají (odlišná
/// serializace, odlišné potřeby). Elementy sem jen promítnou neutrální tvar (<see cref="ColWidth"/>,
/// per-column TextStyle). Tím je nulové riziko pro uložené šablony. DepartureGrid je mimo (pivot).
///
/// Viz `.claude/docs/r1-sdilene-tabulkove-jadro-navrh.md`.
/// </summary>
public static class TableLayout
{
    /// <summary>Neutrální popis šířky sloupce pro <see cref="ResolveWidths"/>.
    /// <paramref name="WidthMm"/> = null → auto (rovnoměrné rozdělení zbytku).</summary>
    public readonly record struct ColWidth(float? WidthMm, ColumnWidthMode Mode = ColumnWidthMode.Mm);

    /// <summary>
    /// Vyřeší šířky sloupců v mm. Pevné (Width != null) se honorují (Percent přepočítá na mm z šířky
    /// elementu), auto (null) si rovným dílem rozdělí zbytek po pevných sloupcích. Nikdy záporné.
    ///
    /// Sjednocuje `StopListElement.ResolveColumnWidths` (mm, auto = null) a
    /// `DynamicTableElement.ComputeColumnWidths` (mm/%, auto = Width&lt;=0) do jednoho vzorce —
    /// behavior-preserving pro oba (auto sentinel se převádí na hranici v projekci elementu).
    /// </summary>
    public static float[] ResolveWidths(IReadOnlyList<ColWidth> cols, float elementWidthMm)
    {
        float Fixed(ColWidth c) =>
            c.Mode == ColumnWidthMode.Percent ? elementWidthMm * (c.WidthMm ?? 0f) / 100f : (c.WidthMm ?? 0f);

        float fixedSum = 0f;
        int autoCount = 0;
        foreach (var c in cols)
        {
            if (c.WidthMm.HasValue) fixedSum += Fixed(c);
            else autoCount++;
        }
        float autoEach = autoCount > 0 ? MathF.Max(0f, (elementWidthMm - fixedSum) / autoCount) : 0f;

        var result = new float[cols.Count];
        for (int i = 0; i < cols.Count; i++)
            result[i] = cols[i].WidthMm.HasValue ? Fixed(cols[i]) : autoEach;
        return result;
    }

    /// <summary>
    /// Vyřeší styl buňky sloupce kaskádou `column-override → element-parent → (baked v parentu)`.
    /// <paramref name="columnLayer"/> = per-column TextStyle (u StopColumn `Style.Header/Data`,
    /// u TableColumn `HeaderStyle/DataStyle`); null = dědit z elementu.
    /// Legacy <paramref name="legacyAlignment"/> (column.Alignment) se použije jako HAlign jen když
    /// ho nedefinuje ani sloupec, ani element (<paramref name="elementDefinesHAlign"/>).
    ///
    /// Sjednocuje `ResolveHeaderStyle`/`ResolveDataStyle` (3× tentýž vzorec ve StopList/DynamicTable/LineTable).
    /// </summary>
    public static ResolvedTextStyle ResolveCascade(
        TextStyle? columnLayer,
        ResolvedTextStyle elementParent,
        TextHorizontalAlignment legacyAlignment,
        bool elementDefinesHAlign)
    {
        var resolved = columnLayer is { } layer ? layer.Resolve(elementParent) : elementParent;
        if (columnLayer?.HAlign is null && !elementDefinesHAlign)
            resolved = resolved with { HAlign = legacyAlignment };
        return resolved;
    }
}
