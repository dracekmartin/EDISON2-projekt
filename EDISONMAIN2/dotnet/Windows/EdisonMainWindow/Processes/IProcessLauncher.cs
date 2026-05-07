using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Processes
{
    public interface IProcessLauncher
    {
        /// <summary>Launches the executable for the given module using the provided connection context.</summary>
        void Launch(ExecutableModule module, string Namespace, string Idfa);

        /// <summary>Kills all processes that were started through this service.</summary>
        void CloseAll();
    }
}
