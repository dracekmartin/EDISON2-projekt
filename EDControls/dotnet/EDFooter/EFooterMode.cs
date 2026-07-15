namespace EDControls.EDFooter;

public enum EFooterMode
{
    /// <summary>Footer shows a single "Zavřít" button.</summary>
    Close,

    /// <summary>Footer shows "Ok" (confirmed via Ctrl+Enter, not IsDefault) and "Storno" buttons.</summary>
    OkCancel
}
