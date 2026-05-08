namespace EDControls.EDViewModel;

public interface IPopupDisplayer
{
    /// <summary>Yes/No potvrzení. Vrátí true pokud uživatel zvolil Ano.</summary>
    bool Confirm(string message);

    /// <summary>Informační zpráva s jediným Ok tlačítkem.</summary>
    void Info(string message);

    /// <summary>Standardní "zavřít bez uložení" potvrzení. Vrátí true pokud uživatel zvolil Ano.</summary>
    bool ConfirmClose();

    /// <summary>Scrollovatelný error dialog s plným textem výjimky.</summary>
    void Error(Exception ex);
}
