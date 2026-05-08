namespace EDControls.EDInputForm
{
    public enum EValidationTrigger
    {
        /// <summary>Volá se jednou hned po načtení formuláře.</summary>
        Init,

        /// <summary>Volá se automaticky při editaci pole s <c>ValidateOnChange</c>.</summary>
        Change,

        /// <summary>Volá se jednou před Save; tady se vynucuje kontrola povinných polí.</summary>
        BeforeSave
    }
}
