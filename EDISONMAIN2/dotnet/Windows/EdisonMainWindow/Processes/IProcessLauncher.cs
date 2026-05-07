using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Processes
{
    public interface IProcessLauncher
    {
        /// <summary>
        /// Spustí executable pro daný modul. Connection kontext (server, port,
        /// namespace, idfa, identita uživatele, shell GUID) se čte z aktuálního
        /// <see cref="EDControls.IMLRest"/> v okamžiku volání — automaticky se
        /// přizpůsobí přepnutí firmy.
        /// </summary>
        void Launch(ExecutableModule module);

        /// <summary>Kills all processes that were started through this service.</summary>
        void CloseAll();
    }
}
