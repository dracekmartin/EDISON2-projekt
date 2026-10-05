using System.IO;

namespace EDUpdateContract;

/// <summary>
/// Stopa vynucené aktualizace vedle cílových binárek, per-instalace (per-user by na sdílené stanici
/// spouštěl vlnu za každého uživatele). EDUPDATE2 píše CliUp2.ini, legacy EDUPDATE CliUp.ini ve
/// stejném formátu; na rozdíl od legacy čtení vadný řádek start shellu neshodí.
/// </summary>
public static class ForcedStamp
{
    public const string FileName = "CliUp2.ini";
    /// <summary>Stopa legacy EDUPDATE, kterou čte i starý shell.</summary>
    public const string LegacyFileName = "CliUp.ini";
    private const string Key = "Spuštěn";

    /// <summary>Poslední splněné razítko; chybějící/nečitelný soubor nebo vadný řádek → "" (= vlna nesplněna).</summary>
    public static string Read(string directory, string fileName = FileName)
    {
        try
        {
            var path = Path.Combine(directory, fileName);
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
