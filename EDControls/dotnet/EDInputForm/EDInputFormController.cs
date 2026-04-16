using System;
using ALWA.SYS;

namespace EDControls.EDInputForm;

/// <summary>
/// Testable orchestration for EDInputForm: Init, Validate, Save.
/// No WPF dependency — takes IMLRest and CacheClass as constructor parameters.
/// Raises <see cref="StateChanged"/> after every Caché round-trip so the view
/// can sync without knowing which properties changed.
/// </summary>
public class EDInputFormController(IMLRest mlRest, string cacheClass)
{
    public List<Item> Items { get; private set; } = [];

    public EHelpTextMode HelpTextMode { get; private set; } = EHelpTextMode.Tooltip;

    /// <summary>Raised after every Caché round-trip that updates Items/HelpTextMode.</summary>
    public event Action? StateChanged;

    /// <summary>
    /// Calls Caché Init to load the form definition, then immediately runs
    /// an Init-trigger validation pass to populate any default errors.
    /// </summary>
    public void Init()
    {
        CVar output = mlRest.QueryOrThrow(cacheClass, "Init", new CVar());
        ApplyOutput(output);
        ExecuteValidate(EValidationTrigger.Init);
    }

    /// <summary>
    /// Runs a Change-trigger validation pass and returns true when there are
    /// no validation errors.
    /// </summary>
    public bool Validate(string? changedKey = null) => ExecuteValidate(EValidationTrigger.Change, changedKey);

    /// <summary>
    /// Validates with BeforeSave trigger and, if there are no errors, calls
    /// Caché Save. Returns true on success.
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

    /// <summary>
    /// Builds a Czech-language error summary from fields that have validation errors.
    /// Returns null when there are no errors.
    /// </summary>
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
    /// Sets "Povinné pole" on required fields that are empty and have no server error.
    /// Server/programmer errors take priority — only fields without errors are touched.
    /// </summary>
    private void ApplyRequiredValidation()
    {
        foreach (var field in Items.OfType<Field>())
        {
            if (field.HasError) continue;
            var requiredError = field.GetRequiredError();
            if (requiredError != null)
                field.ValidationError = requiredError;
        }
    }

    private void ApplyOutput(CVar output)
    {
        Items = EDInputForm.ParseItems(output);
        HelpTextMode = EDInputForm.ParseHelpTextMode(output);
        StateChanged?.Invoke();
    }
}
