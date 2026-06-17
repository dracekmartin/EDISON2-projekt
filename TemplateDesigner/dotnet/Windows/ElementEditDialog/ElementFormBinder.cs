using System.Collections.Generic;
using System.Linq;
using EDControls.EDInputForm;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;

namespace TemplateDesigner;

/// <summary>
/// Most mezi EDInputFormem a libovolným cílovým objektem (element/sekce/stránka) přes reflexi
/// (<see cref="ElementPropertyAccessor"/>, field key = property path).
///
/// Sdílené pro samostatný <see cref="ElementEditDialog"/> i pro embedded EDInputForm v tabu
/// „Obecné" komplexních dialogů (StopColumnsEditorDialog, DepartureGroupEditorDialog) — tam je
/// EDInputForm jen část okna vedle custom editoru sloupců.
/// </summary>
public static class ElementFormBinder
{
    /// <summary>
    /// Naplní Field.Value z cílového objektu (reflexe). Vrátí snapshot pre-filled hodnot
    /// pro pozdější change detection. Volat po EDInputForm.Initialized (schema načteno).
    /// Pokud property nemá hodnotu, nechá Caché schema default (řeší nullable „inherit" props).
    /// </summary>
    public static Dictionary<string, string> Prefill(EDInputForm form, object target)
    {
        var preFilled = new Dictionary<string, string>();
        foreach (var field in (form.Items ?? []).OfType<Field>())
        {
            var reflected = ElementPropertyAccessor.Get(target, field.Key);
            var display = !string.IsNullOrEmpty(reflected) ? reflected : (field.Value ?? "");
            field.Value = display;
            preFilled[field.Key] = display;
        }
        return preFilled;
    }

    /// <summary>
    /// Vytvoří SetPropertyCommandy pro pole, která se reálně změnila proti <paramref name="preFilled"/>.
    /// Virtuální driver pole (klíč není property cíle) přeskočí. Volat po úspěšném EDInputForm.Save().
    /// </summary>
    public static List<IDesignerCommand> ExtractCommands(
        EDInputForm form, object target, Dictionary<string, string> preFilled)
    {
        var commands = new List<IDesignerCommand>();
        foreach (var field in (form.Items ?? []).OfType<Field>())
        {
            var path = field.Key;
            var newVal = field.Value ?? "";
            if (newVal == preFilled.GetValueOrDefault(path, "")) continue;
            if (!ElementPropertyAccessor.Exists(target, path)) continue;

            var oldVal = ElementPropertyAccessor.Get(target, path);
            commands.Add(new SetPropertyCommand(
                field.Label, oldVal, newVal,
                v => ElementPropertyAccessor.Set(target, path, (string?)v)));
        }
        return commands;
    }
}
