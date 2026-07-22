using System;
using System.IO;

namespace EDISONMAIN2;

/// <summary>
/// Čtení <c>VERZE.INF</c> + porovnávání verzí. Port legacy algoritmů z
/// <c>EDISON\EDISONMAIN\clsUpdate.vb</c> — sémantika viz docs/UPDATE_FLOW.md §1.
/// </summary>
public static class VerzeInf
{
    public const string FileName = "VERZE.INF";
    private const string LastKey = "LAST";
    /// <summary>Klíč „M-line pozastavil distribuci verze" (legacy EDISON_UPDATERBlokace_KEY).</summary>
    public const string BlockKey = "BlokovatAktualizace";

    /// <summary>
    /// Vývojová verze — numericky vyšší než jakékoli reálné yyMMdd datum (do r. 2100),
    /// takže IsNewVersion nikdy netriggne update; Caché mismatch řeší dev guard v Updater.
    /// </summary>
    public const string DevVersion = "999999.9";

    /// <summary>Verze běžícího shellu: DEBUG build = <see cref="DevVersion"/>, jinak VERZE.INF vedle exe.</summary>
    public static string Current { get; } =
#if DEBUG
        DevVersion;
#else
        ReadVersion(AppContext.BaseDirectory);
#endif

    /// <summary>Verze (klíč LAST) z VERZE.INF v daném adresáři. Chybějící soubor / klíč / IO chyba → "".</summary>
    public static string ReadVersion(string directory)
    {
        var path = Path.Combine(directory, FileName);
        try
        {
            if (!File.Exists(path)) return "";
            return ParseLast(File.ReadAllText(path));
        }
        catch (IOException) { return ""; }
        catch (UnauthorizedAccessException) { return ""; }
    }

    public static string ParseLast(string content) => ParseValue(content, LastKey);

    /// <summary>Hodnota daného klíče z obsahu VERZE.INF; chybějící klíč → "".</summary>
    public static string ParseValue(string content, string key)
    {
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            int eq = trimmed.IndexOf('=');
            if (eq < 0) continue;
            if (trimmed[..eq] == key) return trimmed[(eq + 1)..].Trim();
        }
        return "";
    }

    /// <summary>
    /// Po-složkové numerické porovnání; nová verze s více segmenty při shodě společných = newer.
    /// Non-numeric segment = 0 (legacy Val() sémantika).
    /// </summary>
    public static bool IsNewVersion(string newVersion, string oldVersion)
    {
        var p1 = newVersion.Split('.');
        var p2 = oldVersion.Split('.');
        for (int i = 0; i < p1.Length; i++)
        {
            if (p2.Length < i + 1) return true;
            long.TryParse(p1[i], out long a);
            long.TryParse(p2[i], out long b);
            if (a < b) return false;
            if (a > b) return true;
        }
        return false;
    }

    /// <summary>Striktní shoda — stejný počet segmentů + textová rovnost všech.</summary>
    public static bool IsSameVersion(string a, string b)
    {
        var p1 = a.Split('.');
        var p2 = b.Split('.');
        if (p1.Length != p2.Length) return false;
        for (int i = 0; i < p1.Length; i++)
            if (p1[i] != p2[i]) return false;
        return true;
    }
}
