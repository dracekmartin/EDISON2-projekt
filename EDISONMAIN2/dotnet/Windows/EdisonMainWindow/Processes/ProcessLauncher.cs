using EDControls;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Processes
{
    public class ProcessLauncher(IMLRest mlRest) : IProcessLauncher
    {
        private readonly List<Process> _processes = [];
        private readonly IMLRest _mlRest = mlRest;

        public void Launch(ExecutableModule module)
        {
            if (string.IsNullOrEmpty(module.ExecutablePath)) return;

            // Connection kontext se čte za běhu z MLRest, takže po přepnutí firmy
            // (změna Idfa / DataNamespace) modul dostane aktuální hodnoty.
            var prc = Process.Start(new ProcessStartInfo()
            {
                UseShellExecute = true,
                FileName = ResolveExecutablePath(module.ExecutablePath),
                Arguments = $"EDISONMAIN2 \"{_mlRest.Server}\" \"{_mlRest.Port}\" \"{_mlRest.DataNamespace}\" \"{_mlRest.Idfa}\" \"{_mlRest.LoginName}\" \"{_mlRest.FullName}\" \"{_mlRest.Mlp}\" \"{module.FormName}\" \"{_mlRest.Guid}\"",
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
