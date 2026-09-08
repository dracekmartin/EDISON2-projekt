using System.IO;

namespace EDUPDATE2;

/// <summary>
/// Stopa vynucené aktualizace - CliUp2.ini vedle cílových binárek. Per-instalace,
/// ne per-user (LocalPrefs by na sdílené stanici spouštěl vlnu za každého uživatele).
/// Záměrně vlastní soubor, ne sdílení legacy CliUp.ini: ten legacy zapisuje relativní
/// cestou (stopa může skončit v jiném adresáři, než odkud se čte → nekonečná smyčka
/// updatů) a čte ho bez ochrany proti vadnému řádku (výjimka shodí start shellu).
/// </summary>
public static class ForcedStamp
{
    public const string FileName = "CliUp2.ini";
    private const string Key = "Spuštěn";

    /// <summary>Poslední splněné razítko; chybějící/nečitelný soubor nebo vadný řádek → "" (= vlna nesplněna).</summary>
    public static string Read(string directory)
    {
        try
        {
            var path = Path.Combine(directory, FileName);
            if (!File.Exists(path)) return "";
            foreach (var line in File.ReadAllLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                if (line[..eq].Trim() == Key) return line[(eq + 1)..].Trim();
            }
            return "";
        }
        catch
        {
            return "";
        }
    }

    public static void Write(string directory, string stamp) =>
        File.WriteAllText(Path.Combine(directory, FileName), $"{Key}={stamp}{Environment.NewLine}");
}
