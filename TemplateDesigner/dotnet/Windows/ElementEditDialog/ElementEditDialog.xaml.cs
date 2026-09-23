using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using EDControls;
using EDControls.EDInputForm;
using SkiaSharp;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;

namespace TemplateDesigner;

/// <summary>
/// Generický editor properties pro libovolný objekt (element, sekce, stránka) přes EDInputForm.
/// Form layout (pole, skupiny, labely, validace) je definovaný server-side v Caché schema
/// classe; klíč pole = property path na cílovém objektu (dot notation, např. "Style.FontSize").
///
/// Pre-fill i extract jdou přes <see cref="ElementFormBinder"/> (reflexe) - žádný
/// per-typ kód. Změny se zabalí do <see cref="ResultCommand"/> (CompositeCommand
/// ze SetPropertyCommandů) pro undo/redo. Caller vykoná přes DesignerState.Execute.
/// </summary>
public partial class ElementEditDialog : Window
{
    /// <summary>Klíč pamatování filtru hledání (viz <c>EDInputForm.SearchMemoryKey</c>). Nastavuje
    /// ho jen otevření vlastností prvku z plátna - jinde se filtr nepamatuje.</summary>
    public string SearchMemoryKey { get => TheForm.SearchMemoryKey; set => TheForm.SearchMemoryKey = value; }

    private readonly object _target;

    // Hodnoty nastavené při pre-fillu - change detection proti nim (ne proti raw property),
    // takže no-edit + OK nevytvoří žádný command (řeší nullable "inherit" props zobrazené
    // jako Caché schema default).
    private Dictionary<string, string> _preFilled = [];

    public IDesignerCommand? ResultCommand { get; private set; }

    /// <summary>Snapshot finálních hodnot polí po OK (klíč = property path). Umožní volajícímu
    /// přečíst zamýšlené hodnoty bez vykonání commandu (např. detekce změny rozměrů stránky).
    /// Null dokud nebyl dialog potvrzen.</summary>
    public IReadOnlyDictionary<string, string>? FinalValues { get; private set; }

    /// <summary>Nabídka polí pro tlačítko „vložit pole" - viz <see cref="EDInputForm.PlaceholderFields"/>.
    /// Bez naplnění by tlačítko v dialogu bylo mrtvé (příznak u pole nese Caché, seznam zná návrhář).</summary>
    public IReadOnlyList<EDControls.ListItem> PlaceholderFields
    {
        get => TheForm.PlaceholderFields;
        set => TheForm.PlaceholderFields = value;
    }

    /// <summary>Hodnoty z kaskády pro cíl, jehož „nenastaveno" znamená dědění (buňka tabulky).
    /// Viz <see cref="ElementFormBinder.ApplyInheritedValues"/>; nastavit před zobrazením.</summary>
    public IReadOnlyDictionary<string, string>? InheritedValues { get; set; }

    /// <param name="target">Editovaný objekt (element, sekce, stránka - in-memory model).</param>
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
        TheForm.SchemaLoaded += OnFormInitialized;
        ElementFormBinder.HookFontWeightRefresh(TheForm);
    }

    private void OnFormInitialized()
    {
        ElementFormBinder.PopulateFontFields(TheForm);
        ElementFormBinder.ApplyInheritedValues(TheForm, InheritedValues);
        _preFilled = ElementFormBinder.Prefill(TheForm, _target);
        // Řez (váhy) z reálných řezů rodiny - nastavit PŘED Validate, aby items přežily round-trip
        // (Validate serializuje aktuální items a Caché je echuje zpět).
        ElementFormBinder.PopulateFontWeightFields(TheForm);
        TheForm.Validate();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TheForm.Save()) return;

        FinalValues = ElementFormBinder.Snapshot(TheForm);
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
