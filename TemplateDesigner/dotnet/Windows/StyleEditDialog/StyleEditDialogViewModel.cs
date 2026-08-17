using System;
using System.Collections.Generic;
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

    private readonly string? _extraTextPath;
    private readonly string? _extraTextOriginal;
    private string? _extraText;

    public CellStyleVM Style { get; }
    public CellStyleEditOptions EditOptions { get; } = CellStyleEditOptions.AllEnabled;

    public bool   HasExtraText  => _extraTextPath is not null;
    public string ExtraTextLabel { get; } = "";
    public string ExtraTextHelp  { get; } = "";

    public string? ExtraText
    {
        get => _extraText;
        set { _extraText = value; OnPropertyChanged(); }
    }

    public Func<string, string?>? RequestColorPicker { get; set; }

    /// <summary>Po OK: undo-able command měnící danou style property (null = beze změny).</summary>
    public IDesignerCommand? ResultCommand { get; private set; }

    public RelayCommand PickTextColorCommand { get; }
    public RelayCommand ClearTextColorCommand { get; }
    public RelayCommand PickBgColorCommand { get; }
    public RelayCommand ClearBgColorCommand { get; }
    public RelayCommand ResetStyleCommand { get; }

    /// <param name="extraTextPath">Nepovinná textová vlastnost cíle editovaná nad stylem (formát
    /// hodnoty buňky). Null = pod-formulář je čistě stylový, jako u zón prvku.</param>
    public StyleEditDialogViewModel(object element, string prefix, ResolvedTextStyle inherited,
        string? extraTextPath = null, string? extraTextLabel = null, string? extraTextHelp = null)
    {
        _element = element;
        _prefix = prefix;
        _original = element.GetType().GetProperty(prefix)?.GetValue(element) as TextStyle;
        Style = CellStyleVM.FromModel(_original, inherited);

        _extraTextPath = extraTextPath;
        ExtraTextLabel = extraTextLabel ?? "";
        ExtraTextHelp  = extraTextHelp ?? "";
        if (extraTextPath is not null)
        {
            _extraText = ElementPropertyAccessor.Get(element, extraTextPath);
            // Nenastaveno se drží jako null, ne jako prázdný text - jinak by zpětný krok uložil
            // do šablony prázdný formát místo „žádný" a hodnota by se přestala brát ze sloupce.
            _extraTextOriginal = string.IsNullOrEmpty(_extraText) ? null : _extraText;
        }

        PickTextColorCommand  = Command(() => { var c = RequestColorPicker?.Invoke(Style.TextColor ?? ""); if (c is not null) Style.TextColor = c; });
        PickBgColorCommand    = Command(() => { var c = RequestColorPicker?.Invoke(Style.BgColor ?? "");   if (c is not null) Style.BgColor = c; });
        ClearTextColorCommand = Command(() => Style.TextColor = null);
        ClearBgColorCommand   = Command(() => Style.BgColor = null);
        ResetStyleCommand     = Command(() => Style.Reset());
    }

    protected override void OnOk()
    {
        var commands = new List<IDesignerCommand>();

        var prop = _element.GetType().GetProperty(_prefix);
        var newStyle = Style.ToModel();
        // Prázdná zóna, která byla a zůstává prázdná → žádná změna (nevytvářet command).
        // Jinak nikdy nezapisuj null - cíl může být nenullovatelný TextStyle (HeaderStyle = new()),
        // null by spadl při renderu. Prázdný styl = nový prázdný TextStyle (dědí default, identický výstup).
        if (prop is not null && (newStyle is not null || _original is not null))
            commands.Add(new SetPropertyCommand(_prefix, _original, newStyle ?? new TextStyle(),
                v => prop.SetValue(_element, v)));

        if (_extraTextPath is { } path && (_extraText ?? "") != (_extraTextOriginal ?? ""))
        {
            // Prázdno je „nenastaveno" (formát se pak bere ze sloupce), ne prázdný řetězec.
            string? newValue = string.IsNullOrWhiteSpace(_extraText) ? null : _extraText;
            commands.Add(new SetPropertyCommand(ExtraTextLabel, _extraTextOriginal, newValue,
                v => ElementPropertyAccessor.Set(_element, path, (string?)v)));
        }

        ResultCommand = commands.Count switch
        {
            0 => null,
            1 => commands[0],
            _ => new CompositeCommand("Vlastnosti buňky", commands)
        };
        CloseDialog(true);
    }
}
