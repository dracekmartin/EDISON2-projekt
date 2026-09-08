using System;
using System.IO;
using System.Xml;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Čtení/zápis EDISONini.xml + IsConfigured() (first-launch detekce). Píše reálný
/// EDISONini.xml vedle test exe - po každém testu uklidíme.
/// </summary>
public class SettingsLoaderTests : IDisposable
{
    private static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "EDISONini.xml");

    // Plná legacy struktura - connection@port je superserver, shell musí číst @restport.
    private const string LegacyXml = """
        <edison>
          <initialized>1</initialized>
          <forceChange>1</forceChange>
          <IRIS>1</IRIS>
          <connection name="CACHEDEV" server="10.0.0.5" port="51972" restport="12345" />
          <database name="VS_CSAD" idfa="VS_CSAD" />
        </edison>
        """;

    public SettingsLoaderTests() => Cleanup();
    public void Dispose() => Cleanup();

    private static void Cleanup()
    {
        if (File.Exists(Path)) File.Delete(Path);
    }

    [Fact]
    public void IsConfigured_NoFile_False()
    {
        Assert.False(SettingsLoader.IsConfigured());
    }

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        var loaded = SettingsLoader.Load();

        Assert.Equal("192.168.16.1", loaded.Server.Host);
        Assert.Equal(57770, loaded.Server.Port);
    }

    [Fact]
    public void Load_LegacyFile_ReadsRestPortNotPort()
    {
        File.WriteAllText(Path, LegacyXml);

        var loaded = SettingsLoader.Load();

        Assert.True(SettingsLoader.IsConfigured());
        Assert.Equal("10.0.0.5", loaded.Server.Host);
        Assert.Equal(12345, loaded.Server.Port);
    }

    [Fact]
    public void IsConfigured_FileWithoutConnection_False()
    {
        File.WriteAllText(Path, "<edison><initialized>1</initialized></edison>");

        Assert.False(SettingsLoader.IsConfigured());
    }

    [Fact]
    public void IsConfigured_UnparsableRestPort_False()
    {
        File.WriteAllText(Path, """<edison><connection server="10.0.0.5" restport="abc" /></edison>""");

        Assert.False(SettingsLoader.IsConfigured());
    }

    [Fact]
    public void Save_LegacyFile_PreservesForeignNodesAndAttributes()
    {
        File.WriteAllText(Path, LegacyXml);

        new SettingsLoader { Server = new ServerSettings { Host = "10.9.9.9", Port = 55555 } }.Save();

        var xmlDoc = new XmlDocument();
        xmlDoc.Load(Path);
        var connection = (XmlElement)xmlDoc.SelectSingleNode("edison/connection")!;
        Assert.Equal("10.9.9.9", connection.GetAttribute("server"));
        Assert.Equal("55555", connection.GetAttribute("restport"));
        Assert.Equal("CACHEDEV", connection.GetAttribute("name"));
        Assert.Equal("51972", connection.GetAttribute("port"));
        Assert.Equal("1", xmlDoc.SelectSingleNode("edison/initialized")!.InnerText);
        Assert.Equal("1", xmlDoc.SelectSingleNode("edison/forceChange")!.InnerText);
        Assert.Equal("1", xmlDoc.SelectSingleNode("edison/IRIS")!.InnerText);
        Assert.Equal("VS_CSAD", ((XmlElement)xmlDoc.SelectSingleNode("edison/database")!).GetAttribute("idfa"));
    }

    [Fact]
    public void Save_NoFile_CreatesConnectionOnly_RoundTrips()
    {
        new SettingsLoader { Server = new ServerSettings { Host = "10.0.0.5", Port = 12345 } }.Save();

        var loaded = SettingsLoader.Load();
        Assert.Equal("10.0.0.5", loaded.Server.Host);
        Assert.Equal(12345, loaded.Server.Port);

        // Wizard nezakládá initialized - to je věc legacy setupu.
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(Path);
        Assert.Null(xmlDoc.SelectSingleNode("edison/initialized"));
    }
}
