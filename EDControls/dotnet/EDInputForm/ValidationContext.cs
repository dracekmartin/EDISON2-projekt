namespace EDControls.EDInputForm;

/// <summary>
/// Carries the trigger kind and — for <see cref="EValidationTrigger.Change"/> events — the key
/// of the field whose value changed.
/// </summary>
public sealed class ValidationContext(EValidationTrigger trigger, string? changedKey = null)
{
    /// <summary>What caused the validation call.</summary>
    public EValidationTrigger Trigger { get; } = trigger;

    /// <summary>
    /// Key of the field that changed.
    /// Non-null only when <see cref="Trigger"/> is <see cref="EValidationTrigger.Change"/>.
    /// </summary>
    public string? ChangedKey { get; } = changedKey;
}
