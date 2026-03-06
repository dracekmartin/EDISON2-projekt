namespace EDControls.EDViewModel;

public interface IPopupDisplayer
{
    /// <summary>Shows a yes/no confirmation. Returns true if the user confirmed (Ano).</summary>
    bool Confirm(string message);

    /// <summary>Shows an informational message with a single Ok button.</summary>
    void Info(string message);

    /// <summary>Standard "close without saving" confirmation. Returns true if user confirmed (Ano).</summary>
    bool ConfirmClose();
}
