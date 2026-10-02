namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Diagnostika pro vývoj a stanice M-line (menu „Test funkcí", textové značky stavu modulů
    /// ve stromu). U zákazníka by „Vyvolat chybu" založilo tiket a suffix „- chybí exe" by se
    /// přetažením na plochu uložil do Caché blobu sdíleného s legacy agendami.
    /// </summary>
    internal static class InternalDiagnostics
    {
        // Nastavuje StartupOptions.ApplyToProcess; default vypnuto = chování stanice zákazníka.
        internal static bool Enabled { get; set; }
    }
}
