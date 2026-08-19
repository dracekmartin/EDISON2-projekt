using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.PdfImport;

/// <summary>
/// Popisky sloupců seznamu zastávek pro import z předlohy. Berou se z výchozích sloupců prvku,
/// aby se importovaná a ručně vložená tabulka nerozešly v názvosloví (import měl vlastní „min.",
/// „Zastávky", „Nást.").
/// </summary>
internal static class StopColumnHeaders
{
    private static readonly List<StopColumn> Defaults = StopListElement.GetDefaultColumns();

    internal static string TravelTime => HeaderFor(StopFields.TravelTime);
    internal static string StopName   => HeaderFor(StopFields.StopName);
    internal static string Poradi     => HeaderFor(StopFields.Poradi);
    internal static string Nastupiste => HeaderFor(StopFields.Nastupiste);
    internal static string Zone       => HeaderFor(StopFields.Zone);

    private static string HeaderFor(string key) =>
        Defaults.FirstOrDefault(c => c.Key == key)?.Header ?? "";

    /// <summary>
    /// Vloží za sloupec času sloupec variant jízdních dob - stejně, jako ho má nově vložený prvek.
    /// U linky s méně než dvěma variantami se nevykreslí, takže předlohu bez variant neovlivní;
    /// bez něj by ale importovaná šablona varianty nikdy neuměla, i když je linka má.
    /// </summary>
    internal static void InsertVariantColumn(List<StopColumn> columns)
    {
        if (columns.Any(c => c.Key == StopFields.TravelTimeVariants)) return;

        var template = Defaults.FirstOrDefault(c => c.Key == StopFields.TravelTimeVariants);
        if (template is null) return;

        int timeIdx = columns.FindIndex(c => c.Key == StopFields.TravelTime);
        if (timeIdx < 0) return;   // bez sloupce času nemá varianta kam patřit

        columns.Insert(timeIdx + 1, new StopColumn
        {
            Key = StopFields.TravelTimeVariants,
            Header = template.Header,
            Width = template.Width,
            Visible = true,
            Alignment = columns[timeIdx].Alignment
        });
    }
}
