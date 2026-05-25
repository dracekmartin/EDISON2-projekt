using System;
using ALWA.SYS;

namespace EDControls.EDInputForm;

/// <summary>
/// Testovatelná orchestrace EDInputFormu: Init, Validate, Save. Bez WPF závislosti —
/// bere <see cref="IMLRest"/> a <c>cacheClass</c> jako parametry konstruktoru. Vyvolává
/// <see cref="StateChanged"/> po každém Caché roundtripu, aby view synchronizoval bez
/// znalosti které property se změnily.
/// </summary>
public class EDInputFormController(IMLRest mlRest, string cacheClass)
{
    public List<Item> Items { get; private set; } = [];

    public EHelpTextMode HelpTextMode { get; private set; } = EHelpTextMode.Tooltip;

    /// <summary>Vyvoláno po každém Caché roundtripu který aktualizuje Items/HelpTextMode.</summary>
    public event Action? StateChanged;

    /// <summary>
    /// Volá Caché <c>Init</c> pro načtení definice formuláře, hned nato Init-trigger validaci
    /// pro naplnění případných výchozích chyb.
    /// </summary>
    public void Init()
    {
        CVar output = mlRest.QueryOrThrow(cacheClass, "Init", new CVar());
        ApplyOutput(output);
        ExecuteValidate(EValidationTrigger.Init);
    }

    /// <summary>Spustí Change-trigger validaci. Vrátí true pokud nejsou chyby.</summary>
    public bool Validate(string? changedKey = null) => ExecuteValidate(EValidationTrigger.Change, changedKey);

    /// <summary>
    /// Validuje s BeforeSave triggerem a pokud nejsou chyby, volá Caché <c>Save</c>.
    /// Vrátí true při úspěchu.
    /// </summary>
    public bool Save()
    {
        if (!ExecuteValidate(EValidationTrigger.BeforeSave)) return false;

        CVar output = mlRest.QueryOrThrow(cacheClass, "Save", EDInputForm.SerializeItemsCVar(Items));
        ApplyOutput(output);
        return true;
    }

    private bool ExecuteValidate(EValidationTrigger trigger, string? changedKey = null)
    {
        var input = EDInputForm.SerializeItemsCVar(Items);
        input["trigger"] = new CVar(trigger.ToString());
        input["helpTextMode"] = new CVar(HelpTextMode.ToWire());
        if (!string.IsNullOrEmpty(changedKey))
            input["changedKey"] = new CVar(changedKey);

        CVar output = mlRest.QueryOrThrow(cacheClass, "Validate", input);
        ApplyOutput(output);
        ApplyRequiredValidation();
        return Items.OfType<Field>().All(f => !f.HasError);
    }

    /// <summary>Sestaví souhrn chybových hlášek z polí s chybou. Vrací null pokud chyby nejsou.</summary>
    public static string? FormatValidationErrors(List<Item> items)
    {
        var errors = items.OfType<Field>()
            .Where(f => f.HasError)
            .Select(f => $"• {f.Label} — {f.ValidationError}")
            .ToList();

        if (errors.Count == 0) return null;

        return "Formulář obsahuje chyby:\n" + string.Join("\n", errors);
    }

    /// <summary>
    /// Aplikuje client-side validaci (type + required) na pole bez serverové chyby.
    /// Server/programmer chyby mají přednost — ovlivňujeme pouze pole bez chyb.
    /// Type chyba (Min/Max/Format/parsing) má přednost před Required (povinné prázdné pole).
    /// </summary>
    private void ApplyRequiredValidation()
    {
        foreach (var field in Items.OfType<Field>())
        {
            if (field.HasError) continue;
            var clientError = field.GetTypeError() ?? field.GetRequiredError();
            if (clientError != null)
                field.ValidationError = clientError;
        }
    }

    private void ApplyOutput(CVar output)
    {
        Items = EDInputForm.ParseItems(output);
        HelpTextMode = EDInputForm.ParseHelpTextMode(output);
        StateChanged?.Invoke();
    }
}
