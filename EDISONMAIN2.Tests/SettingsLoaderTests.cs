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
    // securedMode legacy setup nezapisuje (edituje se ručně) - zápis ho musí zachovat.
    private const string LegacyXml = """
        <edison>
          <initialized>1</initialized>
          <forceChange>1</forceChange>
          <securedMode>1</securedMode>
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
    public void Load_LegacyFile_ReadsAllFields()
    {
        File.WriteAllText(Path, LegacyXml);

        var loaded = SettingsLoader.Load();

        Assert.True(SettingsLoader.IsConfigured());
        Assert.Equal("10.0.0.5", loaded.Server.Host);
        Assert.Equal(12345, loaded.Server.Port);       // @restport, ne @port
        Assert.Equal(51972, loaded.Server.SuperPort);
        Assert.Equal("CACHEDEV", loaded.Server.Name);
        Assert.True(loaded.Server.Iris);
        Assert.Equal("VS_CSAD", loaded.Server.Database);
        Assert.Equal("VS_CSAD", loaded.Server.IdFa);
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
    public void Save_LegacyFile_UpdatesOwnedNodesPreservesForeign()
    {
        File.WriteAllText(Path, LegacyXml);

        new SettingsLoader
        {
            Server = new ServerSettings
            {
                Name = "NOVE", Host = "10.9.9.9", SuperPort = 51973, Port = 55555,
                Iris = false, Database = "JINA_DB", IdFa = "JINA_FA",
            }
        }.Save();

        var xmlDoc = new XmlDocument();
        xmlDoc.Load(Path);
        var connection = (XmlElement)xmlDoc.SelectSingleNode("edison/connection")!;
        Assert.Equal("NOVE", connection.GetAttribute("name"));
        Assert.Equal("10.9.9.9", connection.GetAttribute("server"));
        Assert.Equal("51973", connection.GetAttribute("port"));
        Assert.Equal("55555", connection.GetAttribute("restport"));
        var database = (XmlElement)xmlDoc.SelectSingleNode("edison/database")!;
        Assert.Equal("JINA_DB", database.GetAttribute("name"));
        Assert.Equal("JINA_FA", database.GetAttribute("idfa"));
        Assert.Equal("0", xmlDoc.SelectSingleNode("edison/IRIS")!.InnerText);
        Assert.Equal("1", xmlDoc.SelectSingleNode("edison/initialized")!.InnerText);
        Assert.Equal("1", xmlDoc.SelectSingleNode("edison/forceChange")!.InnerText);
        // Cizí uzel mimo množinu wizardu přežije.
        Assert.Equal("1", xmlDoc.SelectSingleNode("edison/securedMode")!.InnerText);
    }

    [Fact]
    public void Save_NoFile_WritesCompleteLegacyParityFile_RoundTrips()
    {
        new SettingsLoader
        {
            Server = new ServerSettings
            {
                Name = "CACHE", Host = "10.0.0.5", SuperPort = 51972, Port = 12345,
                Iris = true, Database = "DB", IdFa = "FA",
            }
        }.Save();

        var loaded = SettingsLoader.Load();
        Assert.Equal("10.0.0.5", loaded.Server.Host);
        Assert.Equal(12345, loaded.Server.Port);
        Assert.Equal(51972, loaded.Server.SuperPort);
        Assert.True(loaded.Server.Iris);
        Assert.Equal("DB", loaded.Server.Database);
        Assert.Equal("FA", loaded.Server.IdFa);

        // Parita s legacy SaveSettingsToFile - initialized i forceChange = 1.
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(Path);
        Assert.Equal("1", xmlDoc.SelectSingleNode("edison/initialized")!.InnerText);
        Assert.Equal("1", xmlDoc.SelectSingleNode("edison/forceChange")!.InnerText);
    }
}
