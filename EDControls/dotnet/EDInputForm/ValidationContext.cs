namespace EDControls.EDInputForm;

/// <summary>
/// Nese typ triggeru a — pro <see cref="EValidationTrigger.Change"/> — klíč pole které
/// změnu způsobilo.
/// </summary>
public sealed class ValidationContext(EValidationTrigger trigger, string? changedKey = null)
{
    /// <summary>Důvod volání validace.</summary>
    public EValidationTrigger Trigger { get; } = trigger;

    /// <summary>
    /// Klíč pole které změnilo hodnotu. Non-null pouze když <see cref="Trigger"/> je
    /// <see cref="EValidationTrigger.Change"/>.
    /// </summary>
    public string? ChangedKey { get; } = changedKey;
}
