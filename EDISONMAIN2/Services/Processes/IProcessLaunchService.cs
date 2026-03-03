using EDISONMAIN2.Services.Modules;

namespace EDISONMAIN2.Services.Processes
{
    public interface IProcessLaunchService
    {
        /// <summary>Launches the executable for the given module using the provided connection context.</summary>
        void Launch(ExecutableModule module, string Namespace, string Idfa);

        /// <summary>Kills all processes that were started through this service.</summary>
        void CloseAll();
    }
}
