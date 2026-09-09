using System.IO;

namespace EDUPDATE2;

/// <summary>
/// Běhový log do _LOG_UPDATER2.txt v cílovém adresáři. Vlastní soubor - legacy EDUPDATE
/// si _LOG_UPDATER.txt při startu zálohuje a maže, sdílení by si logy vzájemně ničilo.
/// Zápis nesmí nikdy shodit update - chyby se polykají.
/// </summary>
public class UpdateLog
{
    public const string FileName = "_LOG_UPDATER2.txt";
    private readonly string _path;

    public UpdateLog(string directory)
    {
        _path = Path.Combine(directory, FileName);
        try
        {
            if (File.Exists(_path))
                File.Move(_path, Path.ChangeExtension(_path, ".bak"), overwrite: true);
        }
        catch { }
    }

    public string FilePath => _path;

    public virtual void Write(string phase, string message)
    {
        try
        {
            File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\t{phase}\t{message}{Environment.NewLine}");
        }
        catch { }
    }
}
