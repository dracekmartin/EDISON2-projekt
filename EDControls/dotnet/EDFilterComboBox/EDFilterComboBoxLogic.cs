using System.Collections.Generic;
using System.Linq;

namespace EDControls.EDFilterComboBox;

/// <summary>
/// Čistá logika <see cref="EDFilterComboBox"/> oddělená od WPF Controlu kvůli testovatelnosti
/// (zobrazovaný řetězec + filtrovací predikát). Bez WPF závislostí → testovatelné bez STA.
/// </summary>
internal static class EDFilterComboBoxLogic
{
    // Oddělovač "Key - Label" - sdílený dropdownem i klidovým zobrazením; lze přebít per instanci.
    internal const string DisplaySeparator = " - ";

    /// <summary>Zobrazovaný řetězec položky: „Key - Label", nebo jen Key když Label chybí / je shodný.</summary>
    internal static string Display(ListItem item) => Display(item, DisplaySeparator);

    /// <summary>Jako <see cref="Display(ListItem)"/>, ale s vlastním separátorem (viz <c>EDFilterComboBox.KeyLabelSeparator</c>).</summary>
    internal static string Display(ListItem item, string separator) =>
        string.IsNullOrEmpty(item.Label) || item.Label == item.Key
            ? item.Key
            : item.Key + separator + item.Label;

    /// <summary>Filtrovací shoda: prázdný text = vše, jinak substring na Key NEBO Label
    /// (case-insensitive, bez diakritiky - konzistentní se stromem modulů a EDInputForm).</summary>
    internal static bool Matches(ListItem item, string? text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        var needle = SearchNormalization.NormalizeForSearch(text);
        return SearchNormalization.NormalizeForSearch(item.Key).Contains(needle)
            || SearchNormalization.NormalizeForSearch(item.Label).Contains(needle);
    }

    /// <summary>
    /// Cílový index po stisku šipky. Bez wrapování (na krajích stojí - konzistentní se stock
    /// ComboBoxem), z „nic nevybráno" (-1) jde dolů na první a nahoru na poslední položku.
    /// Prázdný seznam vrací -1.
    /// </summary>
    internal static int NextIndex(int count, int current, int delta)
    {
        if (count <= 0) return -1;
        if (current < 0) return delta > 0 ? 0 : count - 1;
        var next = current + delta;
        return next < 0 ? 0 : next >= count ? count - 1 : next;
    }

    /// <summary>
    /// Dohledá položku pro napsaný text při Enteru (bez výběru z dropdownu): exaktní shoda na Key,
    /// pak na Display/Label, nakonec jediný kandidát filtru. Vše normalizovaně (case + diakritika).
    /// Nejednoznačný nebo neznámý text → null (volající revertuje jako dřív).
    /// </summary>
    internal static ListItem? ResolveTyped(IEnumerable<ListItem>? suggestions, string? text, string separator)
    {
        if (suggestions is null || string.IsNullOrWhiteSpace(text)) return null;
        var items = suggestions.ToList();
        var needle = SearchNormalization.NormalizeForSearch(text.Trim());

        var exact = items.FirstOrDefault(i => SearchNormalization.NormalizeForSearch(i.Key) == needle)
                 ?? items.FirstOrDefault(i => SearchNormalization.NormalizeForSearch(Display(i, separator)) == needle
                                           || SearchNormalization.NormalizeForSearch(i.Label) == needle);
        if (exact != null) return exact;

        var candidates = items.Where(i => Matches(i, text)).Take(2).ToList();
        return candidates.Count == 1 ? candidates[0] : null;
    }
}
