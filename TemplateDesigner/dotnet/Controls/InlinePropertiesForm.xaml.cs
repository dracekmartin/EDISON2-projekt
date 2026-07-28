using System;
using System.Collections.Generic;
using System.Windows.Controls;
using TemplateDesigner.Commands;

namespace TemplateDesigner.Controls;

/// <summary>
/// Inline properties panel postavený na EDInputFormu (W4-10) — nahrazuje PropertyDescriptor rendering
/// pro editaci jednoho elementu. Sdílí *Edit.cls schéma + <see cref="ElementFormBinder"/> s modálním
/// <see cref="ElementEditDialog"/>; navíc **živý write-back**: změna pole → SetPropertyCommand → undo.
///
/// Živá editace řeší tři věci nad rámec modálu:
///  1. <b>write-back per změnu</b> (ne až na OK) přes EDInputForm.FieldChanged,
///  2. <b>re-Prefill při změně výběru</b> (stejný typ → jen Prefill; jiný typ → reload schema),
///  3. <b>Loaded re-init past</b> — EDInputForm se re-Inituje na Loaded → Initialized znovu → re-Prefill
///     z aktuálního cíle (bez ztráty stavu).
/// </summary>
public partial class InlinePropertiesForm : UserControl
{
    private object? _target;
    private string? _currentCacheClass;
    private Dictionary<string, string> _preFilled = [];
    private bool _suppress;            // guard: Prefill zapisuje Field.Value → FieldChanged → rekurze
    private Action<IDesignerCommand>? _execute;
    private Action? _invalidate;

    /// <summary>Probublává EDInputForm button (akční pole, např. „__highlightRules") k hostovi.</summary>
    public event Action<string>? ButtonClicked;

    public InlinePropertiesForm()
    {
        InitializeComponent();
        TheForm.Initialized  += OnInitialized;
        TheForm.FieldChanged += OnFieldChanged;
        TheForm.ButtonClicked += key => ButtonClicked?.Invoke(key);
    }

    /// <summary>Napojí undo executor + canvas invalidaci (volá host jednou).</summary>
    public void SetContext(Action<IDesignerCommand> execute, Action invalidate)
    {
        _execute = execute;
        _invalidate = invalidate;
    }

    /// <summary>Zobraz vlastnosti elementu. <paramref name="cacheClass"/> = jeho *Edit.cls schéma.
    /// Stejný typ jako minule → jen re-Prefill (žádný Caché round-trip); jiný typ → reload schématu.</summary>
    public void ShowElement(object target, string cacheClass)
    {
        _target = target;
        if (cacheClass != _currentCacheClass)
        {
            _currentCacheClass = cacheClass;
            TheForm.CacheClass = cacheClass;
            // CacheClass je plain property (Init jinak jede jen na Loaded) → reload schématu ručně.
            // Init() po dokončení vyvolá Initialized → OnInitialized → Prefill z cíle.
            TheForm.Init();
        }
        else
        {
            PrefillFromTarget();
        }
    }

    private void OnInitialized() => PrefillFromTarget();

    private void PrefillFromTarget()
    {
        if (_target is null) return;
        _suppress = true;
        try
        {
            ElementFormBinder.PopulateFontFields(TheForm);
            _preFilled = ElementFormBinder.Prefill(TheForm, _target);
            // Řez (váhy) — plní se z reálných řezů rodiny až po prefillu (kdy je hodnota rodiny známá).
            ElementFormBinder.PopulateFontWeightFields(TheForm);
        }
        finally { _suppress = false; }
    }

    private void OnFieldChanged(string key)
    {
        if (_suppress || _target is null || _execute is null) return;

        var commands = ElementFormBinder.ExtractCommands(TheForm, _target, _preFilled);
        if (commands.Count == 0) return;

        foreach (var cmd in commands) _execute(cmd);
        _preFilled = ElementFormBinder.Snapshot(TheForm);   // baseline = aktuální stav formu
        _invalidate?.Invoke();
    }
}
