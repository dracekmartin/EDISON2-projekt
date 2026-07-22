using System;

namespace EDControls.EDFilterComboBox;

/// <summary>
/// Čistá logika <see cref="EDFilterComboBox"/> oddělená od WPF Controlu kvůli testovatelnosti
/// (zobrazovaný řetězec + filtrovací predikát). Bez WPF závislostí → testovatelné bez STA.
/// </summary>
internal static class EDFilterComboBoxLogic
{
    // Oddělovač "Key - Label" — sdílený dropdownem i klidovým zobrazením; lze přebít per instanci.
    internal const string DisplaySeparator = " - ";

    /// <summary>Zobrazovaný řetězec položky: „Key - Label", nebo jen Key když Label chybí / je shodný.</summary>
    internal static string Display(ListItem item) => Display(item, DisplaySeparator);

    /// <summary>Jako <see cref="Display(ListItem)"/>, ale s vlastním separátorem (viz <c>EDFilterComboBox.KeyLabelSeparator</c>).</summary>
    internal static string Display(ListItem item, string separator) =>
        string.IsNullOrEmpty(item.Label) || item.Label == item.Key
            ? item.Key
            : item.Key + separator + item.Label;

    /// <summary>Filtrovací shoda: prázdný text = vše, jinak substring na Key NEBO Label (case-insensitive).</summary>
    internal static bool Matches(ListItem item, string? text) =>
        string.IsNullOrEmpty(text)
        || item.Key.Contains(text, StringComparison.OrdinalIgnoreCase)
        || item.Label.Contains(text, StringComparison.OrdinalIgnoreCase);
}
