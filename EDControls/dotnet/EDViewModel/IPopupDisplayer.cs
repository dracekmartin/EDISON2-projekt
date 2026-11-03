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
    /// Systémové potvrzení (celá aplikace nebo session: ukončení, přepnutí uživatele či firmy, přihlášení,
    /// licence): Ano/Ne s logem EDISON vlevo, ať je poznat, že se ptá EDISON, ne agenda. True = Ano.
    /// Výchozí implementace drží rozhraní kompatibilní s implementacemi v nepřebuildovaných konzumentech.
    /// </summary>
    bool ConfirmSystem(string message, Window? owner = null) => Confirm(message, owner);

    /// <summary>Systémová informace (licence, aktualizace, konec session, verze): Ok s logem EDISON vlevo.
    /// Výchozí implementace jako u <see cref="ConfirmSystem"/>.</summary>
    void InfoSystem(string message, Window? owner = null) => Info(message, owner);

    /// <summary>
    /// Yes/No/Cancel potvrzení. Vrátí true (Ano) / false (Ne) / null (Storno nebo Esc).
    /// Typický use case: "Šablona obsahuje neuložené změny. Chcete je uložit?"
    /// Title volitelný - default "Potvrzení". Lze nastavit kontextově (např. "Neuložené změny").
    /// </summary>
    bool? ConfirmYesNoCancel(string message, string? title = null, Window? owner = null);

    /// <summary>
    /// Scrollovatelný error dialog s plným textem výjimky. Implementace musí chybu nejdřív nahlásit přes
    /// <see cref="ErrorReporting.ErrorReports.Report"/> (před dialogem kvůli snímku okna) a vrácené číslo ukázat
    /// v dialogu: <c>RelayCommand</c>/<c>AsyncRelayCommand</c> chytají výjimky jen přes displayer, jinak se hlášení ztratí.
    /// </summary>
    void Error(Exception ex, Window? owner = null);
}
