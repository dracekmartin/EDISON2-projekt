using EDControls.EDInputForm;

namespace EDControls.EDInputForm
{
    /// <summary>
    /// Carries the trigger kind and — for <see cref="EValidationTrigger.Change"/> events — the key
    /// of the field whose value changed.  Serialised as a single CTRL entry that is prepended to the
    /// Caché IN array, keeping it cleanly separate from the GROUP/FIELD form data.
    ///
    /// Wire format of the CTRL entry:  "CTRL" + del + trigger + del + changedKey
    /// </summary>
    public sealed class ValidationContext
    {
        /// <summary>What caused the validation call.</summary>
        public EValidationTrigger Trigger { get; }

        /// <summary>
        /// Key of the field that changed.
        /// Non-null only when <see cref="Trigger"/> is <see cref="EValidationTrigger.Change"/>.
        /// </summary>
        public string? ChangedKey { get; }

        public ValidationContext(EValidationTrigger trigger, string? changedKey = null)
        {
            Trigger    = trigger;
            ChangedKey = changedKey;
        }

        /// <summary>
        /// Serialises the context to the CTRL entry string that goes in position IN[1].
        /// </summary>
        public string Serialize(char delimiter)
            => $"CTRL{delimiter}{Trigger}{delimiter}{ChangedKey ?? ""}";
    }

    /// <summary>
    /// EventArgs subclass delivered with <see cref="InputForm.ValidationRequested"/>.
    /// Replaces the plain <see cref="System.EventArgs"/> so subscribers receive the full context
    /// without having to reach into <c>^||ML</c> or make assumptions about IN-array positions.
    /// </summary>
    public sealed class ValidationRequestedEventArgs : System.EventArgs
    {
        public ValidationContext Context { get; }

        public ValidationRequestedEventArgs(ValidationContext context)
            => Context = context;
    }
}
