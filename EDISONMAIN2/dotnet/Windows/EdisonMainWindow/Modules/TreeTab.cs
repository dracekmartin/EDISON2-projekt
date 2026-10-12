using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Jedna záložka launcher panelu. Jediný zdroj pravdy o identitě stromu - dřív žila
    /// paralelně jako jméno property, stringový klíč, index záložky a bool <c>Is*TabActive</c>,
    /// takže přeuspořádání záložek znamenalo koordinovanou editaci na deseti místech.
    /// </summary>
    /// <param name="Key">Identita pro server (<c>GetModuleTree</c>), persistenci rozbalení
    /// i uloženou volbu záložky. Přejmenování = ztráta uloženého stavu uživatelů.</param>
    /// <param name="Tree">Kolekce stromu, nebo <c>null</c> pro záložku, která strom nemá
    /// (Používané je <c>ListBox</c> nad vlastní kolekcí).</param>
    /// <param name="Expandable">Cíl Rozbalit/Sbalit vše a persistence rozbalení. Nepravda
    /// právě tam, kde <c>Tree</c> je null.</param>
    /// <param name="ServerLoaded">Strom plní <c>GetModuleTree</c> pod <see cref="Key"/>.
    /// Oblíbené i Používané se odvozují ze zdrojových stromů, takže se serveru neptají.</param>
    public record TreeTab(
        string Key,
        string Title,
        string IconKey,
        ObservableCollection<IModule>? Tree,
        bool Expandable,
        bool ServerLoaded)
    {
        /// <summary>
        /// Záložky launcher panelu v pořadí UI nad kolekcemi stromů. Okno stromy zobrazuje i dohledává podle
        /// <see cref="Tree"/>, ne podle klíče: nová záložka se stromem je změna tady, kolekce ve VM a <c>TreeView</c>
        /// se stylem <c>TabTree</c> nad ní v kartě okna.
        /// </summary>
        public static IReadOnlyList<TreeTab> Launcher(
            ObservableCollection<IModule> main,
            ObservableCollection<IModule> system,
            ObservableCollection<IModule> partners,
            ObservableCollection<IModule> favorites) =>
        [
            new("main",      "Hlavní nabídka",     "EDIcons.Home",      main,      Expandable: true,  ServerLoaded: true),
            new("system",    "Správa systému",     "EDIcons.Gears",     system,    Expandable: true,  ServerLoaded: true),
            new("partners",  "Partnerská řešení",  "EDIcons.Component", partners,  Expandable: true,  ServerLoaded: true),
            new("favorites", "Oblíbené",           "EDIcons.Star",      favorites, Expandable: true,  ServerLoaded: false),
            new("used",      "Používané",          "EDIcons.History",   null,      Expandable: false, ServerLoaded: false),
        ];
    }
}
