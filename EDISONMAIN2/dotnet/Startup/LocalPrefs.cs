using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace EDISONMAIN2;

/// <summary>
/// Lokální preference per Windows-user. Injectable abstrakce - produkce
/// <see cref="LocalPrefs"/> persistuje do <c>%APPDATA%\m-line\EDISONMAIN2\local.json</c>,
/// testy substituují in-memory implementaci.
/// </summary>
public interface ILocalPrefs
{
    /// <summary>Poslední firma per server (klíč <see cref="ServerPrefs.Key"/>) - shell v ní příště startuje. Null = žádná.</summary>
    Dictionary<string, string>? LastIdfaByServer { get; set; }

    /// <summary>Poslední firma starší verze, jedna pro všechny servery.</summary>
    [Obsolete(ServerPrefs.LegacyGlobalObsolete)]
    string? LastIdfa { get; }

    /// <summary>Poslední přihlašovací jméno per server (klíč <see cref="ServerPrefs.Key"/>) - předvyplní se v login
    /// dialogu. Dvě instalace na stanici (nebo „Připojit k databázi") míří na různé servery. Null = žádné.</summary>
    Dictionary<string, string>? LoginNames { get; set; }

    /// <summary>Zapamatované heslo per server - DPAPI blob (viz <see cref="PasswordProtector"/>). Předvyplní se jen když
    /// jméno v blobu == jméno z <see cref="LoginNames"/>. Null = žádné.</summary>
    /// <remarks>Odděleně od jména, aby je Save slučoval zvlášť: zapomenutí hesla v jedné instanci nesmí vrátit
    /// jméno, které na tomtéž serveru mezitím uložila jiná.</remarks>
    Dictionary<string, string>? RememberedPasswords { get; set; }

    /// <summary>Poslední přihlašovací jméno starší verze, jedno pro všechny servery.</summary>
    [Obsolete(ServerPrefs.LegacyLoginObsolete)]
    string? LastLoginName { get; }

    /// <summary>Zapamatované heslo starší verze (DPAPI blob ke <see cref="LastLoginName"/>), jedno pro všechny servery.</summary>
    [Obsolete(ServerPrefs.LegacyLoginObsolete)]
    string? RememberedPassword { get; }

    /// <summary>Uložený layout miniaplikací (AvalonDock XML) per session, klíč viz <c>DockScope</c>. Null = žádný uložený.</summary>
    Dictionary<string, string>? MiniAppLayouts { get; set; }

    /// <summary>Uložená šířka stromu modulů (px) per session, klíč viz <c>DockScope</c>. Null = žádná uložená.</summary>
    Dictionary<string, double>? TreeWidths { get; set; }

    /// <summary>Uložená výška pruhu podpory (px) per session, klíč viz <c>DockScope</c>. Null = žádná uložená.</summary>
    Dictionary<string, double>? SupportStripHeights { get; set; }

    /// <summary>Uživatelem uložená připojení pro „Připojit k databázi" (výchozí z EDISONini.xml sem nepatří). Null = žádná.</summary>
    List<SavedConnection>? Connections { get; set; }

    /// <summary>Geometrie hlavního okna per server; záznam serveru sdílí shell i přihlašovací okno
    /// (<see cref="ShellWindowStateStore"/>). Null = nic uloženo.</summary>
    Dictionary<string, WindowBoundsPrefs>? ShellWindowBoundsByServer { get; set; }

    /// <summary>Geometrie hlavního okna starší verze, jedna pro všechny servery.</summary>
    [Obsolete(ServerPrefs.LegacyGlobalObsolete)]
    WindowBoundsPrefs? ShellWindowBounds { get; }

    /// <summary>Naposledy nahlášený neúspěšný běh updateru per adresář instalace (<see cref="UpdaterFailureReport"/>).
    /// Null = nic nenahlášeno.</summary>
    Dictionary<string, string>? ReportedUpdaterFailures { get; set; }

    void Save();
}

/// <summary>Uložená geometrie okna. Rozměry si drží i maximalizované okno - platí po un-maximize.</summary>
public class WindowBoundsPrefs
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Maximized { get; set; }
}

/// <summary>Údaje v <see cref="ILocalPrefs"/> vázané na server: přihlášení, poslední firma a geometrie hlavního okna.</summary>
public static class ServerPrefs
{
    /// <summary>Text <see cref="ObsoleteAttribute"/> u přihlášení starší verze.</summary>
    public const string LegacyLoginObsolete =
        "Přihlášení verze před ukládáním per server. Čte ho jen AdoptLegacyLogin a nikdo ho nezapisuje: "
        + "starší verze shellu na téže stanici z něj dál předvyplňuje. Odebrat, až ji žádná stanice nemá.";

    /// <summary>Text <see cref="ObsoleteAttribute"/> u firmy a geometrie okna starší verze.</summary>
    public const string LegacyGlobalObsolete =
        "Hodnota verze před ukládáním per server. Čte ji jen LastIdfaOrLegacyFor a ShellWindowStateStore a nikdo "
        + "ji nezapisuje: starší verze shellu na téže stanici z ní dál čte. Odebrat, až ji žádná stanice nemá.";

    // Malá písmena: host se v EDISONini.xml i v argumentech píše v různé velikosti a pořád jde o týž server.
    public static string Key(string server, int port) => $"{server}:{port}".ToLowerInvariant();

#pragma warning disable CS0618 // jediné místo, které přihlášení a firmu starší verze čte
    /// <summary>
    /// Dokud žádný server nemá vlastní záznam (první start po aktualizaci), převezme přihlášení starší verze
    /// (jen v paměti, uloží ho příští Save): uživatel tak jméno a heslo znovu nezadává.
    /// </summary>
    /// <remarks>Jen jednou na stanici: později by jinému serveru vrátilo heslo, které uživatel mezitím zapomněl nebo změnil.</remarks>
    public static void AdoptLegacyLogin(this ILocalPrefs prefs, string server, int port)
    {
        if (prefs.LoginNames is { Count: > 0 } || prefs.LastLoginName is not { } name) return;
        prefs.SetLoginName(server, port, name);
        prefs.SetRememberedPassword(server, port, prefs.RememberedPassword);
    }

    /// <summary>
    /// Poslední firma serveru. Dokud žádný server nemá vlastní (první start po aktualizaci), firma starší verze.
    /// </summary>
    /// <remarks>Nic nezapisuje: firmu serveru uloží až přijatá session, ne login, který licence odmítne.
    /// Po prvním uložení už firmu starší verze nedostane žádný server, druhá instalace by jinak startovala ve firmě té první.</remarks>
    public static string? LastIdfaOrLegacyFor(this ILocalPrefs prefs, string server, int port) =>
        prefs.LastIdfaByServer is { Count: > 0 } ? prefs.LastIdfaFor(server, port)
        : string.IsNullOrEmpty(prefs.LastIdfa) ? null : prefs.LastIdfa;
#pragma warning restore CS0618

    public static string? LastIdfaFor(this ILocalPrefs prefs, string server, int port) =>
        prefs.LastIdfaByServer?.GetValueOrDefault(Key(server, port));

    public static void SetLastIdfa(this ILocalPrefs prefs, string server, int port, string? idfa) =>
        prefs.LastIdfaByServer = Put(prefs.LastIdfaByServer, Key(server, port), idfa);

    public static WindowBoundsPrefs? ShellWindowBoundsFor(this ILocalPrefs prefs, string server, int port) =>
        prefs.ShellWindowBoundsByServer?.GetValueOrDefault(Key(server, port));

    public static void SetShellWindowBounds(this ILocalPrefs prefs, string server, int port, WindowBoundsPrefs? bounds) =>
        prefs.ShellWindowBoundsByServer = Put(prefs.ShellWindowBoundsByServer, Key(server, port), bounds);

    public static string? LoginNameFor(this ILocalPrefs prefs, string server, int port) =>
        prefs.LoginNames?.GetValueOrDefault(Key(server, port));

    public static void SetLoginName(this ILocalPrefs prefs, string server, int port, string? name) =>
        prefs.LoginNames = Put(prefs.LoginNames, Key(server, port), name);

    public static string? RememberedPasswordFor(this ILocalPrefs prefs, string server, int port) =>
        prefs.RememberedPasswords?.GetValueOrDefault(Key(server, port));

    public static void SetRememberedPassword(this ILocalPrefs prefs, string server, int port, string? blob) =>
        prefs.RememberedPasswords = Put(prefs.RememberedPasswords, Key(server, port), blob);

    // Null hodnota klíč maže: ve slovníku by ji JSON zapsal jako "klíč": null.
    private static Dictionary<string, T>? Put<T>(Dictionary<string, T>? map, string key, T? value) where T : class
    {
        if (value is null)
        {
            map?.Remove(key);
            return map;
        }
        map ??= new();
        map[key] = value;
        return map;
    }
}

/// <summary>Alternativní připojení k jinému Caché serveru; spouští se jako nová instance shellu (<see cref="ConnectionOverride"/>).</summary>
public class SavedConnection
{
    public string Name { get; set; } = "";
    public string Server { get; set; } = "";
    public int RestPort { get; set; }
}

/// <summary>
/// Diskové persistované preference. Pro nové preference přidávej nullable property
/// (chybějící klíče v existujícím souboru zůstanou null). Composition root
/// (<see cref="AppBootstrapper"/>) vytvoří jednu instanci přes <see cref="Load"/>
/// a propaguje ji do consumerů (CompanySwitcher, EdisonMainViewModel).
/// </summary>
public class LocalPrefs : ILocalPrefs
{
    public Dictionary<string, string>? LastIdfaByServer { get; set; }
    [Obsolete(ServerPrefs.LegacyGlobalObsolete)] public string? LastIdfa { get; init; }
    public Dictionary<string, string>? LoginNames { get; set; }
    public Dictionary<string, string>? RememberedPasswords { get; set; }
    [Obsolete(ServerPrefs.LegacyLoginObsolete)] public string? LastLoginName { get; init; }
    [Obsolete(ServerPrefs.LegacyLoginObsolete)] public string? RememberedPassword { get; init; }

    public Dictionary<string, string>? MiniAppLayouts { get; set; }
    public Dictionary<string, double>? TreeWidths { get; set; }
    public Dictionary<string, double>? SupportStripHeights { get; set; }
    public List<SavedConnection>? Connections { get; set; }
    public Dictionary<string, WindowBoundsPrefs>? ShellWindowBoundsByServer { get; set; }
    [Obsolete(ServerPrefs.LegacyGlobalObsolete)] public WindowBoundsPrefs? ShellWindowBounds { get; init; }
    public Dictionary<string, string>? ReportedUpdaterFailures { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    private static readonly JsonTypeInfo Contract = JsonOptions.GetTypeInfo(typeof(LocalPrefs));

    /// <summary>JSON jména preferencí, které se slučují po klíčích (serializují se jako slovník).</summary>
    private static readonly HashSet<string> PerKeyNames = Contract.Properties
        .Where(p => JsonOptions.GetTypeInfo(p.PropertyType).Kind == JsonTypeInfoKind.Dictionary)
        .Select(p => p.Name)
        .ToHashSet();

    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Stav souboru, ze kterého instance vychází (po Load / posledním Save) - báze sloučení.</summary>
    private JsonObject _snapshot = new();

    /// <summary>Cíl persistence. Settable jen kvůli testům (jinak by šly proti reálnému %APPDATA%).</summary>
    internal static string FilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "m-line", "EDISONMAIN2", "local.json");

    /// <summary>Načte preference z disku. Poškozený / chybějící soubor → defaultní instance.</summary>
    public static LocalPrefs Load()
    {
        LocalPrefs? loaded = null;
        try
        {
            // Pod zámkem: otevřený soubor by jiné instanci v Save shodil přejmenování dočasného souboru.
            WithFileLock(() => loaded = Read()?.Prefs);
        }
        catch (Exception)
        {
            // Load běží před loginem a nesmí shodit start. Nečitelný soubor = defaulty s prázdným
            // snímkem, takže pozdější Save z disku převezme vše, co instance sama nezměnila.
        }
        var prefs = loaded ?? new();
        prefs._snapshot = prefs.ToJson();
        return prefs;
    }

    /// <summary>
    /// Soubor sdílí víc instancí shellu, proto se nezapisuje celý snímek: co instance od Load / posledního
    /// Save nezměnila, převezme se z disku (slovníky po klíčích). Referenční hodnoty jsou pak nové instance,
    /// referenci na ně proto nedrž přes Save.
    /// </summary>
    public void Save()
    {
        WithFileLock(() =>
        {
            (JsonObject Json, LocalPrefs Prefs)? disk;
            try
            {
                disk = Read();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Nečitelný (ne poškozený) soubor nesmí přepsat změny jiných instancí; snímek zůstává,
                // takže vlastní změny zapíše příští Save.
                return;
            }
            // Chybějící / poškozený soubor: disk = báze, sloučení pak zapíše celý náš stav.
            var merged = Merge(_snapshot, ToJson(), disk?.Json ?? _snapshot, PerKeyNames.Contains);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            WriteAtomically(merged.ToJsonString(JsonOptions));

            // Paměť i báze = výsledek; jinak by příští Save považoval cizí klíče za smazané námi.
            var fresh = merged.Deserialize<LocalPrefs>(JsonOptions)!;
            foreach (var property in Contract.Properties) property.Set?.Invoke(this, property.Get?.Invoke(fresh));
            _snapshot = ToJson();
        });
    }

    private JsonObject ToJson() => JsonSerializer.SerializeToNode(this, JsonOptions)!.AsObject();

    /// <summary>Soubor jako JSON (i s klíči novější verze) a jako objekt; null = chybí nebo je poškozený.
    /// Nečitelný soubor (síť, přístup) hází <see cref="IOException"/> / <see cref="UnauthorizedAccessException"/>.</summary>
    private static (JsonObject Json, LocalPrefs Prefs)? Read()
    {
        string text;
        try
        {
            text = File.ReadAllText(FilePath);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        try
        {
            if (JsonNode.Parse(text) is not JsonObject json) return null;
            return json.Deserialize<LocalPrefs>(JsonOptions) is { } prefs ? (json, prefs) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Tříbodové sloučení: klíč změněný proti bázi bere náš (chybějící = smazaný), jinak z disku.</summary>
    private static JsonObject Merge(JsonObject? snapshot, JsonObject? ours, JsonObject? disk, Func<string, bool> perKey)
    {
        var merged = new JsonObject();
        var keys = new[] { ours, disk, snapshot }.OfType<JsonObject>().SelectMany(o => o.Select(p => p.Key)).Distinct();
        foreach (var key in keys)
        {
            JsonNode? before = snapshot?[key], mine = ours?[key], theirs = disk?[key];
            var value = JsonNode.DeepEquals(mine, before) ? theirs
                : perKey(key) ? Merge(before as JsonObject, mine as JsonObject, theirs as JsonObject, _ => false)
                : mine;
            if (value is not null) merged[key] = value.DeepClone();
        }
        return merged;
    }

    /// <summary>Přes dočasný soubor a přejmenování - pád ani výpadek uprostřed zápisu nenechá rozepsaný soubor.</summary>
    private static void WriteAtomically(string json)
    {
        var temp = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
            {
                stream.Write(Encoding.UTF8.GetBytes(json));
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, FilePath, overwrite: true);
        }
        catch
        {
            File.Delete(temp);
            throw;
        }
    }

    /// <summary>
    /// Zámek souboru mezi instancemi. <c>Local\</c> = přihlašovací relace: kryje všechny instance uživatele
    /// na ploše a cizí uživatel terminálového serveru jméno neobsadí. Nezískaný zámek zápis neblokuje.
    /// </summary>
    private static void WithFileLock(Action body)
    {
        Mutex? mutex = null;
        var owned = false;
        try
        {
            try
            {
                var path = Encoding.UTF8.GetBytes(Path.GetFullPath(FilePath).ToUpperInvariant());
                mutex = new Mutex(false, @"Local\EDISONMAIN2.LocalPrefs." + Convert.ToHexString(SHA256.HashData(path)));
                owned = mutex.WaitOne(LockTimeout);
            }
            catch (AbandonedMutexException)
            {
                owned = true;   // držitel spadl, zámek je náš; soubor je díky atomickému zápisu celý
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException)
            {
                // Bez zámku zbývá samotné sloučení; nechráněné je jen okno mezi čtením a zápisem.
            }
            body();
        }
        finally
        {
            if (owned) mutex!.ReleaseMutex();
            mutex?.Dispose();
        }
    }
}
