using System;
using System.IO;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Save/Load round-trip + Exists() (first-launch detekce). Píše reálný
/// appsettings.json vedle test exe - po každém testu uklidíme.
/// </summary>
public class SettingsLoaderTests : IDisposable
{
    private static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public SettingsLoaderTests() => Cleanup();
    public void Dispose() => Cleanup();

    private static void Cleanup()
    {
        if (File.Exists(Path)) File.Delete(Path);
    }

    [Fact]
    public void Exists_NoFile_False()
    {
        Assert.False(SettingsLoader.Exists());
    }

    [Fact]
    public void Save_ThenExists_True()
    {
        new SettingsLoader().Save();

        Assert.True(SettingsLoader.Exists());
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsServer()
    {
        new SettingsLoader { Server = new ServerSettings { Host = "10.0.0.5", Port = 12345 } }.Save();

        var loaded = SettingsLoader.Load();

        Assert.Equal("10.0.0.5", loaded.Server.Host);
        Assert.Equal(12345, loaded.Server.Port);
    }
}
