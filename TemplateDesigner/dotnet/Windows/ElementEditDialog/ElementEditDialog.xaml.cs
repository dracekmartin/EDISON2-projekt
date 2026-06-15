using System.Collections.Generic;
using System.Windows;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;

namespace TemplateDesigner;

/// <summary>
/// Generický editor properties pro libovolný objekt (element, sekce, stránka) přes EDInputForm.
/// Form layout (pole, skupiny, labely, validace) je definovaný server-side v Caché schema
/// classe; klíč pole = property path na cílovém objektu (dot notation, např. "Style.FontSize").
///
/// Pre-fill i extract jdou přes <see cref="ElementFormBinder"/> (reflexe) — žádný
/// per-typ kód. Změny se zabalí do <see cref="ResultCommand"/> (CompositeCommand
/// ze SetPropertyCommandů) pro undo/redo. Caller vykoná přes DesignerState.Execute.
/// </summary>
public partial class ElementEditDialog : Window
{
    private readonly object _target;

    // Hodnoty nastavené při pre-fillu — change detection proti nim (ne proti raw property),
    // takže no-edit + OK nevytvoří žádný command (řeší nullable "inherit" props zobrazené
    // jako Caché schema default).
    private Dictionary<string, string> _preFilled = [];

    public IDesignerCommand? ResultCommand { get; private set; }

    /// <param name="target">Editovaný objekt (element, sekce, stránka — in-memory model).</param>
    /// <param name="cacheClass">Caché schema class (např. "ALVA.TemplateDesigner.EDInputForm.TextElementEdit").</param>
    /// <param name="title">Titulek okna.</param>
    public ElementEditDialog(object target, string cacheClass, string title)
    {
        _target = target;
        InitializeComponent();
        Title = title;
        TheForm.CacheClass = cacheClass;

        // Po Init (Caché schema načteno) pre-fillni Field.Value z elementu přes reflexi,
        // pak re-validuj (Init validace běžela na prázdných / default hodnotách).
        TheForm.Initialized += OnFormInitialized;
    }

    private void OnFormInitialized()
    {
        _preFilled = ElementFormBinder.Prefill(TheForm, _target);
        TheForm.Validate();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TheForm.Save()) return;

        var commands = ElementFormBinder.ExtractCommands(TheForm, _target, _preFilled);
        ResultCommand = commands.Count switch
        {
            0 => null,
            1 => commands[0],
            _ => new CompositeCommand($"Změna vlastností: {Title}", commands)
        };

        DialogResult = true;
        Close();
    }
}
