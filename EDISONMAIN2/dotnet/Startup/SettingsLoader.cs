using System.IO;
using System.Xml;

namespace EDISONMAIN2;

/// <summary>
/// Připojení z <c>EDISONini.xml</c> vedle exe (<c>edison/connection@server</c> + <c>@restport</c>).
/// Soubor je autoritativní na serveru zákazníka a na stanice ho roznáší EDUPDATE; sdílí ho
/// legacy konzumenti (EDLOADER, starý shell, ESEET), proto zápis zachovává cizí uzly.
/// </summary>
public class SettingsLoader
{
    public ServerSettings Server { get; set; } = new();

    public static SettingsLoader? Current { get; private set; }

    private const string FileName = "EDISONini.xml";

    private static string MainPath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>
    /// First-launch indikátor: bez souboru s vyplněnou <c>connection</c> (server + restport)
    /// není stanice nakonfigurovaná → ukázat setup wizard. Legacy soubor bez connection sem
    /// spadne taky - prázdné připojení má poslat uživatele do wizardu, ne do pádu loginu.
    /// </summary>
    public static bool IsConfigured()
    {
        var (host, port) = ReadConnection();
        return !string.IsNullOrEmpty(host) && port > 0;
    }

    public static SettingsLoader Load()
    {
        var settings = new SettingsLoader();

        var (host, port) = ReadConnection();
        if (!string.IsNullOrEmpty(host)) settings.Server.Host = host;
        if (port > 0) settings.Server.Port = port;

        Current = settings;
        return Current;
    }

    /// <summary>Zapíše <c>EDISONini.xml</c> vedle exe (setup wizard). Šetrný merge:
    /// přepíše jen <c>connection@server</c> a <c>@restport</c>, ostatní uzly a atributy
    /// (<c>initialized</c>, <c>IRIS</c>, <c>database</c>, <c>@port</c>, ...) zachová.</summary>
    public void Save()
    {
        var xmlDoc = new XmlDocument();
        if (File.Exists(MainPath))
            xmlDoc.Load(MainPath);

        var edison = xmlDoc.DocumentElement;
        if (edison is null || edison.Name != "edison")
        {
            edison = xmlDoc.CreateElement("edison");
            xmlDoc.AppendChild(edison);
        }

        if (edison.SelectSingleNode("connection") is not XmlElement connection)
        {
            connection = xmlDoc.CreateElement("connection");
            edison.AppendChild(connection);
        }
        connection.SetAttribute("server", Server.Host);
        connection.SetAttribute("restport", Server.Port.ToString());

        xmlDoc.Save(MainPath);
        Current = this;
    }

    /// <summary>Tolerantní čtení jako legacy EDLOADER: chybějící soubor, nevalidní XML
    /// i chybějící uzel vrací prázdno, rozhodne se z toho až volající.</summary>
    private static (string Host, int Port) ReadConnection()
    {
        if (!File.Exists(MainPath)) return ("", 0);
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(MainPath);

            if (xmlDoc.SelectSingleNode("edison/connection") is not XmlElement connection)
                return ("", 0);

            int.TryParse(connection.GetAttribute("restport"), out int port);
            return (connection.GetAttribute("server"), port);
        }
        catch
        {
            return ("", 0);
        }
    }
}

public class ServerSettings
{
    public string Host { get; set; } = "192.168.16.1";
    public int    Port { get; set; } = 57770;
}
