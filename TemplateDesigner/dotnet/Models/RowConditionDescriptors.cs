using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner.Models;

/// <summary>
/// Sdílené <see cref="PropertyDescriptor"/> řádkových podmínek pro tabulkové elementy
/// (DynamicTable/StopList/LineTable) - jedno místo místo kopie v každém elementu. Vlastní pole
/// (<c>RowVisibilityExpression</c>) drží každý element sám (stav), descriptor je společný.
/// </summary>
public static class RowConditionDescriptors
{
    /// <summary>Descriptor „Viditelnost řádku" - výraz nad daty řádku; prázdné = všechny řádky.</summary>
    public static StringPropertyDescriptor RowVisibility(Func<string?> get, Action<string?> set) => new()
    {
        Label = "Viditelnost řádku", Group = "Podmínky",
        Get = () => get() ?? "",
        Set = v => set(string.IsNullOrWhiteSpace(v) ? null : v.Trim()),
        Validate = ExpressionEvaluator.Validate
    };
}
