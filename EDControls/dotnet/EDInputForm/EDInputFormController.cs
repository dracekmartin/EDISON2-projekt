using ALWA.SYS;

namespace EDControls.EDInputForm;

/// <summary>
/// Testable orchestration for EDInputForm: Init, Validate, Save.
/// No WPF dependency — takes IMLRest and CacheClass as constructor parameters.
/// </summary>
public class EDInputFormController(IMLRest mlRest, string cacheClass)
{
    public List<Item> Items { get; private set; } = [];

    /// <summary>
    /// Calls Caché Init to load the form definition, then immediately runs
    /// an Init-trigger validation pass to populate any default errors.
    /// </summary>
    public void Init()
    {
        CVar output = mlRest.QueryOrThrow(cacheClass, "Init", new CVar());
        var newItems = EDInputForm.ParseItems(output);
        PreserveExpansionState(newItems);
        Items = newItems;
        ExecuteValidate(EValidationTrigger.Init);
    }

    /// <summary>
    /// Runs a Change-trigger validation pass and returns true when there are
    /// no validation errors.
    /// </summary>
    public bool Validate() => ExecuteValidate(EValidationTrigger.Change);

    /// <summary>
    /// Validates with BeforeSave trigger and, if there are no errors, calls
    /// Caché Save. Returns true on success.
    /// </summary>
    public bool Save()
    {
        if (!ExecuteValidate(EValidationTrigger.BeforeSave)) return false;

        CVar output = mlRest.QueryOrThrow(cacheClass, "Save", EDInputForm.SerializeItemsCVar(Items));
        Items = EDInputForm.ParseItems(output);
        return true;
    }

    private bool ExecuteValidate(EValidationTrigger trigger)
    {
        var input = EDInputForm.SerializeItemsCVar(Items);
        input["trigger"] = new CVar(trigger.ToString());

        CVar output = mlRest.QueryOrThrow(cacheClass, "Validate", input);
        var newItems = EDInputForm.ParseItems(output);
        PreserveExpansionState(newItems);
        Items = newItems;
        return Items.OfType<Field>().All(f => !f.HasError);
    }

    private void PreserveExpansionState(List<Item> newItems)
    {
        var expanded = Items.OfType<Group>().ToDictionary(g => g.GroupName, g => g.IsExpanded);
        foreach (var g in newItems.OfType<Group>())
            if (expanded.TryGetValue(g.GroupName, out bool was))
                g.IsExpanded = was;
    }
}
