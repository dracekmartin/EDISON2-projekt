using System.ComponentModel;

namespace TemplateDesigner.Controls;

/// <summary>
/// Sdílený kontrakt řádku sloupce pro <see cref="ColumnListEditor"/> - jednotné levé seznamy
/// napříč tabulkovými editory (StopList, DepartureGrid; výhledově TableElement).
/// Implementují ho per-komponentní column VM (StopColumnVM, DepartureGroupColumnVM).
/// </summary>
public interface IColumnRowVM : INotifyPropertyChanged
{
    /// <summary>Zobrazovaný název v seznamu (efektivní záhlaví = override, jinak zdroj).</summary>
    string DisplayName { get; }

    /// <summary>Viditelnost sloupce (checkbox v seznamu).</summary>
    bool Visible { get; set; }

    /// <summary>True = sloupec má per-column override stylu (modrá tečka v seznamu).</summary>
    bool HasAnyOverride { get; }

    /// <summary>Šířka v mm pro zobrazení v seznamu; null = sloupec nemá pevnou šířku (auto) → nezobrazí se.</summary>
    float? ColumnWidth { get; }
}
