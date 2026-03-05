namespace EDControls.EDInputForm
{
    public enum EValidationTrigger
    {
        /// <summary>Called once immediately after the form is loaded.</summary>
        Init,

        /// <summary>Called automatically when a ValidateOnChange field is edited.</summary>
        Change,

        /// <summary>Called once before Save; required-field checks are enforced here.</summary>
        BeforeSave
    }
}
