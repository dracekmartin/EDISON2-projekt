using System;
using System.IO;
using System.Threading;

namespace EDControls.Tests;

/// <summary>
/// Mechanismus pod font.json a theme.json bez WPF: zápis jedné instance (jiný proces) musí
/// druhá přes FileSystemWatcher načíst a ohlásit. Vlastní instance, žádný statický seam.
/// </summary>
public class SharedJsonPreferenceTests
{
    private sealed class Probe
    {
        public string? Name { get; set; }
    }

    [Fact]
    public void SaveByOtherInstance_ListenerReloadsCurrentAndRaisesChanged()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"edshared_{Guid.NewGuid():N}");
        var file = Path.Combine(dir, "probe.json");
        try
        {
            using var listener = new SharedJsonPreference<Probe>(file);
            var otherProcess = new SharedJsonPreference<Probe>(file);
            using var reloaded = new ManualResetEventSlim();
            // Jeden zápis hlásí víc eventů a první může přijít nad prázdným souborem - čeká se na načtenou hodnotu.
            listener.Changed += () => { if (listener.Current.Name == "shell") reloaded.Set(); };
            listener.BeginListening();

            otherProcess.Save(new Probe { Name = "shell" });

            Assert.True(reloaded.Wait(TimeSpan.FromSeconds(5)), "watcher zápis jiné instance nenačetl");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
