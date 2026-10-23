using System.Diagnostics;
using Microsoft.Win32;

namespace TemplateDesigner.Services;

/// <summary>
/// Otevře soubor ve výchozím prohlížeči Windows (PDF ze spuštění z modulu JŘ, rozhodnutí Jan 23. 10.).
/// Výchozí aplikace pro .pdf může být Acrobat nebo nic; prohlížeč PDF zobrazí na každé stanici.
/// </summary>
public static class DefaultBrowser
{
    /// <summary>Otevře soubor v prohlížeči; když prohlížeč nejde zjistit, ve výchozí aplikaci souboru.</summary>
    public static void Open(string filePath)
    {
        var url = new Uri(filePath).AbsoluteUri;
        if (BrowserCommand() is { } command && BuildStart(command, url) is { } start)
        {
            try
            {
                Process.Start(new ProcessStartInfo(start.Exe, start.Arguments) { UseShellExecute = false });
                return;
            }
            catch (System.ComponentModel.Win32Exception) { /* prohlížeč z registru nejde spustit - níž výchozí aplikace */ }
        }
        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
    }

    /// <summary>Příkaz výchozího prohlížeče: volba uživatele pro https → příkaz „open" jeho ProgId.</summary>
    private static string? BrowserCommand()
    {
        using var choice = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
        if (choice?.GetValue("ProgId") is not string progId || progId.Length == 0) return null;
        using var command = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
        return command?.GetValue(null) as string;
    }

    /// <summary>
    /// Rozloží příkaz z registru na program a argumenty a dosadí adresu za <c>%1</c>
    /// (Edge a Chrome mají <c>--single-argument %1</c>, Firefox <c>-url "%1"</c>). Bez <c>%1</c> se adresa připojí.
    /// Adresa jde bez uvozovek: <c>--single-argument</c> bere zbytek řádku doslova, uvozovky by byly součástí
    /// adresy. Mezery a uvozovky v cestě <see cref="Uri.AbsoluteUri"/> zakóduje, takže uvozovky nepotřebuje.
    /// </summary>
    internal static (string Exe, string Arguments)? BuildStart(string command, string url)
    {
        command = command.Trim();
        string exe, rest;
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            if (end < 0) return null;
            exe = command[1..end];
            rest = command[(end + 1)..].Trim();
        }
        else
        {
            int space = command.IndexOf(' ');
            exe = space < 0 ? command : command[..space];
            rest = space < 0 ? "" : command[(space + 1)..].Trim();
        }
        if (exe.Length == 0) return null;

        string args = rest.Contains("%1", StringComparison.Ordinal)
            ? rest.Replace("\"%1\"", "%1", StringComparison.Ordinal).Replace("%1", url, StringComparison.Ordinal)
            : (rest + " " + url).Trim();
        return (exe, args);
    }
}
