using System.IO;
using System.Xml;

namespace EDISONMAIN2;

/// <summary>
/// Nastavení z <c>EDISONini.xml</c> vedle exe (parita s legacy <c>SaveSettingsToFile</c>).
/// Soubor je autoritativní na serveru zákazníka a na stanice ho roznáší EDUPDATE; sdílí ho
/// legacy konzumenti (EDLOADER, starý shell, ESEET), proto zápis zachovává cizí uzly.
/// Shell sám používá jen <c>connection@server</c> + <c>@restport</c>.
/// </summary>
public class SettingsLoader
{
    public ServerSettings Server { get; set; } = new();

    public static SettingsLoader? Current { get; private set; }

    private const string FileName = "EDISONini.xml";

    /// <summary>Settable jen kvůli testům - produkce čte EDISONini.xml vedle exe.</summary>
    internal static string FilePath { get; set; } = Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>
    /// First-launch indikátor: bez souboru s vyplněnou <c>connection</c> (server + restport)
    /// není stanice nakonfigurovaná → ukázat setup wizard. Legacy soubor bez connection sem
    /// spadne taky - prázdné připojení má poslat uživatele do wizardu, ne do pádu loginu.
    /// </summary>
    public static bool IsConfigured()
    {
        var settings = Read();
        return !string.IsNullOrEmpty(settings.Server.Host) && settings.Server.Port > 0;
    }

    public static SettingsLoader Load()
    {
        var settings = Read();

        // Bez použitelné connection drž dev defaulty (předvyplnění wizardu).
        if (string.IsNullOrEmpty(settings.Server.Host)) settings.Server.Host = ServerSettings.DefaultHost;
        if (settings.Server.Port <= 0) settings.Server.Port = ServerSettings.DefaultPort;

        Current = settings;
        return Current;
    }

    /// <summary>
    /// Zapíše <c>EDISONini.xml</c> vedle exe (setup wizard). Parita s legacy zápisem:
    /// <c>initialized=1</c> + <c>forceChange=1</c>, <c>IRIS</c>, celá <c>connection</c>
    /// a <c>database</c>; uzly mimo tuhle množinu (např. <c>securedMode</c>) zachová.
    /// </summary>
    public void Save()
    {
        var xmlDoc = new XmlDocument();
        if (File.Exists(FilePath))
            xmlDoc.Load(FilePath);

        var edison = xmlDoc.DocumentElement;
        if (edison is null || edison.Name != "edison")
        {
            edison = xmlDoc.CreateElement("edison");
            xmlDoc.AppendChild(edison);
        }

        SetElement(edison, "initialized", "1");
        SetElement(edison, "forceChange", "1");
        SetElement(edison, "IRIS", Server.Iris ? "1" : "0");

        var connection = SetElement(edison, "connection", null);
        connection.SetAttribute("name", Server.Name);
        connection.SetAttribute("server", Server.Host);
        connection.SetAttribute("port", Server.SuperPort.ToString());
        connection.SetAttribute("restport", Server.Port.ToString());

        var database = SetElement(edison, "database", null);
        database.SetAttribute("name", Server.Database);
        database.SetAttribute("idfa", Server.IdFa);

        xmlDoc.Save(FilePath);
        Current = this;
    }

    private static XmlElement SetElement(XmlElement parent, string name, string? innerText)
    {
        if (parent.SelectSingleNode(name) is not XmlElement element)
        {
            element = parent.OwnerDocument!.CreateElement(name);
            parent.AppendChild(element);
        }
        if (innerText is not null) element.InnerText = innerText;
        return element;
    }

    /// <summary>Tolerantní čtení jako legacy EDLOADER: chybějící soubor, nevalidní XML
    /// i chybějící uzel vrací prázdno, rozhodne se z toho až volající.</summary>
    private static SettingsLoader Read()
    {
        var settings = new SettingsLoader { Server = { Host = "", Port = 0 } };
        if (!File.Exists(FilePath)) return settings;
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(FilePath);
            var server = settings.Server;

            if (xmlDoc.SelectSingleNode("edison/connection") is XmlElement connection)
            {
                server.Name = connection.GetAttribute("name");
                server.Host = connection.GetAttribute("server");
                int.TryParse(connection.GetAttribute("restport"), out int restPort);
                server.Port = restPort;
                int.TryParse(connection.GetAttribute("port"), out int superPort);
                server.SuperPort = superPort;
            }

            if (xmlDoc.SelectSingleNode("edison/IRIS") is XmlElement iris)
                server.Iris = iris.InnerText.Trim() == "1";

            if (xmlDoc.SelectSingleNode("edison/database") is XmlElement database)
            {
                server.Database = database.GetAttribute("name");
                server.IdFa = database.GetAttribute("idfa");
            }

            return settings;
        }
        catch
        {
            return new SettingsLoader { Server = { Host = "", Port = 0 } };
        }
    }
}

/// <summary>Obraz <c>EDISONini.xml</c>: <see cref="Host"/> + <see cref="Port"/> (REST) používá
/// shell, zbytek nese wizard kvůli paritě souboru s legacy setupem.</summary>
public class ServerSettings
{
    public const string DefaultHost = "192.168.16.1";
    public const int    DefaultPort = 57770;

    public string Host { get; set; } = DefaultHost;
    /// <summary>REST port (<c>connection@restport</c>).</summary>
    public int    Port { get; set; } = DefaultPort;

    public string Name      { get; set; } = "";
    /// <summary>Superserver port (<c>connection@port</c>) - shell ho nepoužívá, jen přenáší.</summary>
    public int    SuperPort { get; set; }
    public bool   Iris      { get; set; }
    public string Database  { get; set; } = "";
    public string IdFa      { get; set; } = "";
}
