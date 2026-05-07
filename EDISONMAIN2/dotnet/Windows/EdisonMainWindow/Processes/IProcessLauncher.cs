using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Processes
{
    public interface IProcessLauncher
    {
        /// <summary>Spustí modul s aktuálním connection kontextem z IMLRest (po přepnutí firmy nový modul dostane novou IdFa).</summary>
        void Launch(ExecutableModule module);

        /// <summary>Zabije všechny procesy spuštěné přes tuto službu.</summary>
        void CloseAll();
    }
}
