namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Diagnostika pro vývoj a stanice M-line (menu „Test funkcí", textové značky stavu modulů
    /// ve stromu). U zákazníka by „Vyvolat chybu" založilo tiket a suffix „- chybí exe" by se
    /// přetažením na plochu uložil do Caché blobu sdíleného s legacy agendami.
    /// </summary>
    internal static class InternalDiagnostics
    {
        // Settable jen kvůli testům; Release rozhoduje doména stanice (kolegové validují Release build).
        internal static bool Enabled { get; set; } =
#if DEBUG
            true;
#else
            ModuleTreeState.IsMlineDomainStation;
#endif
    }
}
