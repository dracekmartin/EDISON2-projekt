using EDISONMAIN2.Services;
using EDISONMAIN2.Services.Modules;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace EDISONMAIN2.Services.Processes
{
    public class ProcessLauncher(ConnectionParams connParams) : IProcessLauncher
    {
        private readonly List<Process> _processes = [];
        private readonly ConnectionParams _connParams = connParams;

        public void Launch(ExecutableModule module, string Namespace, string Idfa)
        {
            if (string.IsNullOrEmpty(module.ExecutablePath)) return;

            var prc = Process.Start(new ProcessStartInfo()
            {
                UseShellExecute = true,
                FileName = ResolveExecutablePath(module.ExecutablePath),
                Arguments = $"EDISONMAIN2 \"{_connParams.Server}\" \"{_connParams.RestPort}\" \"{Namespace}\" \"{Idfa}\" \"{_connParams.UserName}\" \"{_connParams.FullName}\" \"{_connParams.Mlp}\" \"{module.FormName}\" \"{_connParams.Guid}\"",
            });

            if (prc != null) _processes.Add(prc);
        }

        private static string ResolveExecutablePath(string relativePath)
        {
            var localPath = Path.Combine(AppContext.BaseDirectory, relativePath);
            if (File.Exists(localPath)) return localPath;
            return Path.Combine(@"C:\Edison", relativePath);
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
