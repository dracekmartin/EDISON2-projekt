using System.Windows;

namespace EDControls.EDViewModel;

public interface IPopupDisplayer
{
    /// <summary>Yes/No potvrzení. Vrátí true pokud uživatel zvolil Ano.</summary>
    bool Confirm(string message, Window? owner = null);

    /// <summary>Informační zpráva s jediným Ok tlačítkem.</summary>
    void Info(string message, Window? owner = null);

    /// <summary>Standardní "zavřít bez uložení" potvrzení. Vrátí true pokud uživatel zvolil Ano.</summary>
    bool ConfirmClose(Window? owner = null);

    /// <summary>
    /// Yes/No/Cancel potvrzení. Vrátí true (Ano) / false (Ne) / null (Storno nebo Esc).
    /// Typický use case: "Šablona obsahuje neuložené změny. Chcete je uložit?"
    /// Title volitelný - default "Potvrzení". Lze nastavit kontextově (např. "Neuložené změny").
    /// </summary>
    bool? ConfirmYesNoCancel(string message, string? title = null, Window? owner = null);

    /// <summary>Scrollovatelný error dialog s plným textem výjimky.</summary>
    void Error(Exception ex, Window? owner = null);
}
