using EDISONMAIN2.Modules;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace EDISONMAIN2.Services.Processes
{
    public class ProcessLaunchService(ConnectionParams connParams) : IProcessLaunchService
    {
        private readonly List<Process> _processes = [];
        private readonly ConnectionParams _connParams = connParams;

        public void Launch(ExecutableModule module, string Namespace, string Idfa)
        {
            if (string.IsNullOrEmpty(module.ExecutablePath)) return;

            var prc = Process.Start(new ProcessStartInfo()
            {
                UseShellExecute = true,
                FileName = Path.Combine("C:\\EDISON", module.ExecutablePath),
                Arguments = $"EDISONMAIN2 \"{_connParams.Server}\" \"{_connParams.RestPort}\" \"{Namespace}\" \"{Idfa}\" \"{_connParams.UserName}\" \"{_connParams.FullName}\" \"{_connParams.Mlp}\" \"{module.FormName}\"",
            });

            if (prc != null) _processes.Add(prc);
        }

        public void CloseAll()
        {
            foreach (var process in _processes)
            {
                try
                {
                    if (!process.HasExited) process.Kill();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing process: {ex.Message}");
                }
            }
        }
    }
}
