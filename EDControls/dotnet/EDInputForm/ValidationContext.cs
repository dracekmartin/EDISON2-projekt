namespace EDControls.EDInputForm;

/// <summary>
/// Nese typ triggeru a - pro <see cref="ValidationTrigger.Change"/> - klíč pole které
/// změnu způsobilo.
/// </summary>
public sealed class ValidationContext(ValidationTrigger trigger, string? changedKey = null)
{
    /// <summary>Důvod volání validace.</summary>
    public ValidationTrigger Trigger { get; } = trigger;

    /// <summary>
    /// Klíč pole které změnilo hodnotu. Non-null pouze když <see cref="Trigger"/> je
    /// <see cref="ValidationTrigger.Change"/>.
    /// </summary>
    public string? ChangedKey { get; } = changedKey;
}
