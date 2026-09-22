using System;
using EDControls.EDViewModel;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Models;
using TemplateDesigner.Rendering;

namespace TemplateDesigner;

/// <summary>
/// VM pod-formuláře pro editaci jednoho TextStyle (zóny) libovolného elementu přes sdílený
/// <c>CellStyleEditor</c> (tri-state: zděděné hodnoty jako šedý placeholder). Generický -
/// styl se čte/zapisuje přes reflexi podle <paramref name="prefix"/> (property path na elementu).
/// </summary>
class StyleEditDialogViewModel : EDViewModel
{
    private readonly object _element;
    private readonly string _prefix;
    private readonly TextStyle? _original;

    public CellStyleVM Style { get; }
    public CellStyleEditOptions EditOptions { get; } = CellStyleEditOptions.AllEnabled;

    /// <summary>Po OK: undo-able command měnící danou style property (null = beze změny).</summary>
    public IDesignerCommand? ResultCommand { get; private set; }

    public RelayCommand ResetStyleCommand { get; }

    public StyleEditDialogViewModel(object element, string prefix, ResolvedTextStyle inherited)
    {
        _element = element;
        _prefix = prefix;
        _original = element.GetType().GetProperty(prefix)?.GetValue(element) as TextStyle;
        Style = CellStyleVM.FromModel(_original, inherited);

        ResetStyleCommand = Command(() => Style.Reset());
    }

    protected override void OnOk()
    {
        var prop = _element.GetType().GetProperty(_prefix);
        var newStyle = Style.ToModel();
        // Prázdná zóna, která byla a zůstává prázdná → žádná změna (nevytvářet command).
        // Jinak nikdy nezapisuj null - cíl může být nenullovatelný TextStyle (HeaderStyle = new()),
        // null by spadl při renderu. Prázdný styl = nový prázdný TextStyle (dědí default, identický výstup).
        if (prop is not null && (newStyle is not null || _original is not null))
            ResultCommand = new SetPropertyCommand(_prefix, _original, newStyle ?? new TextStyle(),
                v => prop.SetValue(_element, v));
        CloseDialog(true);
    }
}
