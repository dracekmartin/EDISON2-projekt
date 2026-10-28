using System.Linq;
using System.Windows;
using EDControls.EDInputForm;
using TemplateDesigner.Commands;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner;

/// <summary>
/// Tlačítko „Upravit styl…" ve formuláři Minut odjezdů (klíč <c>style:&lt;vlastnost&gt;</c>). Formulář se
/// ukazuje v panelu vlastností i v okně nastavení prvku a okno stylu musí otevřít v obou - do 23. 10. ho
/// obsluhovalo jen okno nastavení a v panelu tlačítko nedělalo nic.
/// </summary>
static class ElementStyleButton
{
    public const string KeyPrefix = "style:";

    /// <summary>Vlastnost stylu z klíče tlačítka; null = jiné tlačítko.</summary>
    public static string? StyleProperty(string key) =>
        key.StartsWith(KeyPrefix, System.StringComparison.Ordinal) ? key[KeyPrefix.Length..] : null;

    /// <summary>Které pole okno stylu nabídne: 2. řádek záhlaví je text uvnitř záhlaví skupiny, pozadí,
    /// zarovnání a okraje mu kreslí 1. řádek - nabízet je by byla pole, která nic nedělají.</summary>
    public static CellStyleEditOptions EditOptionsFor(string property) =>
        property == nameof(DepartureGridElement.HeaderLine2Style)
            ? CellStyleEditOptions.TextOnly
            : CellStyleEditOptions.AllEnabled;

    /// <summary>Okno stylu zóny s titulkem podle popisku tlačítka; null = Storno nebo beze změny.</summary>
    public static IDesignerCommand? Edit(Window owner, DepartureGridElement element, string property, EDInputForm form)
    {
        var title = (form.Items ?? []).OfType<Field>().FirstOrDefault(f => f.Key == KeyPrefix + property)?.Label ?? "Styl";
        var vm = new StyleEditDialogViewModel(element, property, element.ResolveStyleParent(property) ?? CellDefaults.Default,
            EditOptionsFor(property));
        var dlg = new StyleEditDialog(vm) { Owner = owner, Title = title };
        return dlg.ShowDialog() == true ? vm.ResultCommand : null;
    }
}
