using System.ComponentModel;

namespace TemplateDesigner.Controls;

/// <summary>
/// Kontrakt řádku sloupce pro levý seznam <see cref="ColumnListEditor"/>. Implementuje ho
/// <see cref="ColumnRowVM"/> - řádek sdílené záložky Sloupce (<see cref="ColumnsTab"/>).
/// </summary>
public interface IColumnRowVM : INotifyPropertyChanged
{
    /// <summary>Zobrazovaný název v seznamu - čitelný název sloupce, ne tištěná zkratka záhlaví.</summary>
    string DisplayName { get; }

    /// <summary>Viditelnost sloupce (checkbox v seznamu).</summary>
    bool Visible { get; set; }

    /// <summary>False = sloupec se tiskne vždy a zaškrtávátko je vypnuté.</summary>
    bool CanHide { get; }

    /// <summary>True = sloupec má per-column override stylu (modrá tečka v seznamu).</summary>
    bool HasAnyOverride { get; }

    /// <summary>Šířka v mm pro zobrazení v seznamu; null = sloupec nemá pevnou šířku (auto) → nezobrazí se.</summary>
    float? ColumnWidth { get; }
}
