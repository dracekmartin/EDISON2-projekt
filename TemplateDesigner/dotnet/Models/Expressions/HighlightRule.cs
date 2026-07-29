namespace TemplateDesigner.Models.Expressions;

/// <summary>
/// Jedno pravidlo podmíněného zvýraznění řádku tabulky: když <see cref="Condition"/> (výraz nad daty
/// řádku) platí, řádek dostane pozadí <see cref="BackgroundColor"/>. Sdílené tabulkovými elementy
/// (DynamicTable, StopList, LineTable) jako <c>List&lt;HighlightRule&gt;</c> - vyhodnocuje
/// <see cref="RowConditions.HighlightColor(System.Collections.Generic.IReadOnlyList{HighlightRule}, DataContext)"/>
/// (první splněné pravidlo vyhrává).
/// </summary>
public class HighlightRule
{
    /// <summary>Výraz nad poli řádku (např. <c>{STAV}=="STORNO"</c>). Prázdný = pravidlo se neuplatní.</summary>
    public string? Condition { get; set; }

    /// <summary>Barva pozadí <c>#AARRGGBB</c>. Prázdná = pravidlo se neuplatní.</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Klíč cílového sloupce; null = celý řádek. Rezervováno pro per-sloupcové zvýraznění
    /// (render ho zatím ignoruje a barví celý řádek).</summary>
    public string? TargetColumn { get; set; }

    public HighlightRule Clone() => new()
    {
        Condition = Condition,
        BackgroundColor = BackgroundColor,
        TargetColumn = TargetColumn,
    };
}
