using System;
using System.Collections.Generic;
using System.Linq;
using EDControls;
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
    // Nainstalované fonty (z FontResolver = SKFontManager, tentýž zdroj jako renderer). Cachované.
    private static List<ListItem>? _fontItems;
    private static List<ListItem> FontItems => _fontItems ??=
        Models.FontResolver.InstalledFamilies.Select(s => new ListItem(s, s)).ToList();

    /// <summary>Klientsky naplní seznam nainstalovaných fontů do ComboBox polí (klíč končí "FontFamily").
    /// Caché ten seznam nezná (OS věc). Volat po EDInputForm.Initialized (před/spolu s Prefill) — set
    /// proběhne dřív, než WPF realizuje kontejner ComboBoxu, takže se binding chytí i bez change-notifikace.
    /// Sdílené napříč všemi hosty EDInputFormu (ElementEditDialog + embedded v komplexních dialozích).</summary>
    public static void PopulateFontFields(EDInputForm form)
    {
        foreach (var field in (form.Items ?? []).OfType<Field>())
            if (field.FieldType == EFieldType.ComboBox
                && field.Key.EndsWith("FontFamily", StringComparison.Ordinal))
                field.ListItems = FontItems;
    }

    /// <summary>Klientsky naplní „Řez" (List pole s klíčem končícím "FontWeight") reálnými váhami
    /// sourozenecké rodiny ("…FontFamily") ze sdíleného <see cref="Models.FontWeightCatalog"/>. Volat
    /// PO <see cref="Prefill"/> (kdy je hodnota rodiny známá). Caché řezy nezná (OS věc, jako fonty).</summary>
    public static void PopulateFontWeightFields(EDInputForm form)
    {
        var fields = (form.Items ?? []).OfType<Field>().ToList();
        foreach (var field in fields)
        {
            if (field.FieldType != EFieldType.List
                || !field.Key.EndsWith("FontWeight", StringComparison.Ordinal)) continue;
            var familyKey = field.Key[..^"FontWeight".Length] + "FontFamily";
            var family = fields.FirstOrDefault(f => f.Key == familyKey)?.Value;
            field.ListItems = Models.FontWeightCatalog.Items(family)
                .Select(x => new ListItem(x.Weight.ToString(), x.Label)).ToList();
        }
    }

    /// <summary>Snapshot aktuálních hodnot polí formuláře (bez zápisu do cíle) — pro update
    /// change-detection baseline po živém write-backu v inline panelu.</summary>
    public static Dictionary<string, string> Snapshot(EDInputForm form)
    {
        var snap = new Dictionary<string, string>();
        foreach (var field in (form.Items ?? []).OfType<Field>())
            snap[field.Key] = field.Value ?? "";
        return snap;
    }

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
