namespace EDISONMAIN2.Modules
{
    /// <summary>
    /// Diagnostické značky stavu modulů ve stromu pro vývoj a stanice M-line. U zákazníka by se suffix
    /// „- chybí exe" přetažením na plochu uložil do Caché blobu sdíleného s legacy agendami. Menu „Test funkcí"
    /// dostává týž příznak ze <c>StartupOptions</c> konstruktorem VM shellu.
    /// </summary>
    internal static class InternalDiagnostics
    {
        // Nastavuje StartupOptions.ApplyToProcess; default vypnuto = chování stanice zákazníka.
        internal static bool Enabled { get; set; }
    }
}
