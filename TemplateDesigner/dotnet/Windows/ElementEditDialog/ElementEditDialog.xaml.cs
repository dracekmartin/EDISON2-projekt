using System.Collections.Generic;
using System.Linq;
using System.Windows;
using EDControls.EDInputForm;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Models;

namespace TemplateDesigner;

/// <summary>
/// Generický editor properties pro libovolný <see cref="TemplateElement"/> přes EDInputForm.
/// Form layout (pole, skupiny, labely, validace) je definovaný server-side v Caché schema
/// classe; klíč pole = property path na elementu (dot notation, např. "Style.FontSize").
///
/// Pre-fill i extract jdou přes <see cref="ElementPropertyAccessor"/> (reflexe) — žádný
/// per-element kód. Změny se zabalí do <see cref="ResultCommand"/> (CompositeCommand
/// ze SetPropertyCommandů) pro undo/redo. Caller vykoná přes DesignerState.Execute.
/// </summary>
public partial class ElementEditDialog : Window
{
    private readonly TemplateElement _element;

    // Hodnoty nastavené při pre-fillu — change detection proti nim (ne proti raw property),
    // takže no-edit + OK nevytvoří žádný command (řeší nullable "inherit" props zobrazené
    // jako Caché schema default).
    private readonly Dictionary<string, string> _preFilled = [];

    public IDesignerCommand? ResultCommand { get; private set; }

    /// <param name="element">Editovaný element (in-memory model).</param>
    /// <param name="cacheClass">Caché schema class (např. "ALVA.TemplateDesigner.EDInputForm.TextElementEdit").</param>
    /// <param name="title">Titulek okna.</param>
    public ElementEditDialog(TemplateElement element, string cacheClass, string title)
    {
        _element = element;
        InitializeComponent();
        Title = title;
        TheForm.CacheClass = cacheClass;

        // Po Init (Caché schema načteno) pre-fillni Field.Value z elementu přes reflexi,
        // pak re-validuj (Init validace běžela na prázdných / default hodnotách).
        TheForm.Initialized += OnFormInitialized;
    }

    private void OnFormInitialized()
    {
        foreach (var field in (TheForm.Items ?? []).OfType<Field>())
        {
            var reflected = ElementPropertyAccessor.Get(_element, field.Key);
            // Pokud property má hodnotu → zobraz ji. Pokud null/prázdná → nech Caché schema
            // default (řeší nullable "inherit" properties, např. Style.FontSize z loaded JSON).
            var display = !string.IsNullOrEmpty(reflected) ? reflected : (field.Value ?? "");
            field.Value = display;
            _preFilled[field.Key] = display;
        }
        TheForm.Validate();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TheForm.Save()) return;

        var commands = new List<IDesignerCommand>();
        foreach (var field in (TheForm.Items ?? []).OfType<Field>())
        {
            var path = field.Key;
            var newVal = field.Value ?? "";
            // Change detection proti pre-filled hodnotě — user reálně editoval?
            if (newVal == _preFilled.GetValueOrDefault(path, "")) continue;

            // oldVal = aktuální raw hodnota property (pro undo — "" pokud byla null)
            var oldVal = ElementPropertyAccessor.Get(_element, path);
            commands.Add(new SetPropertyCommand(
                field.Label, oldVal, newVal,
                v => ElementPropertyAccessor.Set(_element, path, (string?)v)));
        }

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
