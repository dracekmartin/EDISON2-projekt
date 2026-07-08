using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Tabulkový element s podmíněným zvýrazněním řádků (seznam <see cref="HighlightRule"/>). Sdílené
/// DynamicTable/StopList/LineTable — umožňuje editoru pravidel a apply příkazu pracovat genericky
/// nad libovolným zvýraznitelným elementem, bez společné bázové třídy.
/// </summary>
public interface IRowHighlightable
{
    List<HighlightRule>? HighlightRules { get; set; }
}
