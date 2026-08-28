using System;
using ALWA.SYS;

namespace EDControls.EDInputForm;

/// <summary>
/// Testovatelná orchestrace EDInputFormu: Init, Validate, Save. Bez WPF závislosti -
/// bere <see cref="IMLRest"/> a <c>cacheClass</c> jako parametry konstruktoru. Vyvolává
/// <see cref="StateChanged"/> po každém Caché roundtripu, aby view synchronizoval bez
/// znalosti které property se změnily.
/// </summary>
public class EDInputFormController(IMLRest mlRest, string cacheClass)
{
    public List<Item> Items { get; private set; } = [];

    public HelpTextMode HelpTextMode { get; private set; } = HelpTextMode.Tooltip;

    /// <summary>Vyvoláno po každém Caché roundtripu který aktualizuje Items/HelpTextMode.</summary>
    public event Action? StateChanged;

    /// <summary>
    /// Volá Caché <c>Init</c> pro načtení definice formuláře, hned nato Init-trigger validaci
    /// pro naplnění případných výchozích chyb. Volitelně lze předat <paramref name="initParams"/>
    /// - Caché Init je čte přes <c>^||ML("IN","key")</c> (např. pro pre-fill polí
    /// initial hodnotami z hostitelského contextu).
    /// </summary>
    public void Init(CVar? initParams = null)
    {
        CVar output = mlRest.QueryOrThrow(cacheClass, "Init", initParams ?? new CVar());
        ApplyOutput(output);
        ExecuteValidate(ValidationTrigger.Init);
    }

    /// <summary>Spustí Change-trigger validaci. Vrátí true pokud nejsou chyby.</summary>
    public bool Validate(string? changedKey = null) => ExecuteValidate(ValidationTrigger.Change, changedKey);

    /// <summary>
    /// Validuje s BeforeSave triggerem a pokud nejsou chyby, volá Caché <c>Save</c>.
    /// Vrátí true při úspěchu.
    /// </summary>
    /// <remarks>Caché Save smí OUT nechat prázdný (host persistuje sám) - proto se Items přepisují
    /// jen když OUT nese číselné subscripty, jinak by se ztratil stav po validaci.</remarks>
    public bool Save()
    {
        if (!ExecuteValidate(ValidationTrigger.BeforeSave)) return false;

        CVar output = mlRest.QueryOrThrow(cacheClass, "Save", EDInputFormWire.SerializeItemsCVar(Items));
        if (HasFormItems(output)) ApplyOutput(output);
        return true;
    }

    /// <summary>True pokud OUT obsahuje form items na číselných subscriptech (1, 2, ...).
    /// Metadata klíče (ERR, helpTextMode, ...) se ignorují automaticky díky whitelist
    /// na číselné subscripty.</summary>
    private static bool HasFormItems(CVar output)
    {
        foreach (var kvp in output)
            if (int.TryParse(kvp.Key, out _)) return true;
        return false;
    }

    private bool ExecuteValidate(ValidationTrigger trigger, string? changedKey = null)
    {
        var input = EDInputFormWire.SerializeItemsCVar(Items);
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
            .Select(f => $"• {f.Label} - {f.ValidationError}")
            .ToList();

        if (errors.Count == 0) return null;

        return "Formulář obsahuje chyby:\n" + string.Join("\n", errors);
    }

    /// <summary>
    /// Aplikuje client-side validaci (type + required) na pole bez serverové chyby.
    /// Server/programmer chyby mají přednost - ovlivňujeme pouze pole bez chyb.
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
        // Image pole se na server neposílají (viz SerializeItemsCVar) - server by je echem
        // přepsal prázdnem. Zachovat jejich client-side hodnotu (Base64) přes round-trip.
        var preservedImages = Items.OfType<Field>()
            .Where(f => f.FieldType == FieldType.Image && !string.IsNullOrEmpty(f.Value))
            .ToDictionary(f => f.Key, f => f.Value!);

        Items = EDInputFormWire.ParseItems(output);
        HelpTextMode = EDInputFormWire.ParseHelpTextMode(output);

        if (preservedImages.Count > 0)
            foreach (var f in Items.OfType<Field>())
                if (f.FieldType == FieldType.Image && preservedImages.TryGetValue(f.Key, out var v))
                    f.Value = v;

        StateChanged?.Invoke();
    }
}
