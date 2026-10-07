using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Win32;

namespace EDISONMAIN2;

/// <summary>
/// Poslední firma starého shellu pro dané připojení (legacy <c>frmNoMDI.GetConnectionCompany</c>): klíč
/// <c>HKCU\Software\ALVA\EDISONMAIN\ConnectionList\&lt;připojení&gt;</c>, hodnota <c>idfa</c>. Nový shell ji bere,
/// když jeho vlastní <c>LastIdfa</c> na serveru není (první start) - jinak by začal ve výchozí firmě serveru.
/// </summary>
internal static class LegacyLastCompany
{
    private const string ConnectionListKey = @"Software\ALVA\EDISONMAIN\ConnectionList";

    internal sealed record Entry(string? Server, string? RestPort, string? Idfa, bool IsDefault);

    /// <summary>Null = starý shell na tomhle připojení firmu nezná (nebo registr nejde přečíst).</summary>
    internal static string? Read(string server, int restPort)
    {
        try
        {
            using var list = Registry.CurrentUser.OpenSubKey(ConnectionListKey);
            if (list is null) return null;
            var entries = new List<Entry>();
            foreach (var name in list.GetSubKeyNames())
            {
                using var key = list.OpenSubKey(name);
                if (key is null) continue;
                entries.Add(new Entry(key.GetValue("server")?.ToString(), key.GetValue("restport")?.ToString(),
                    key.GetValue("idfa")?.ToString(), key.GetValue("default")?.ToString() == "1"));
            }
            return Pick(entries, server, restPort);
        }
        // Jen náhradní zdroj firmy na startu: cokoli z registru = firma starého shellu se nepoužije, start nesmí spadnout.
        catch (Exception)
        {
            return null;
        }
    }

    // Víc připojení na týž server (legacy je vede pod různými jmény): přednost má výchozí připojení starého shellu.
    // Port jako číslo: legacy kopíruje text z EDISONini.xml doslova (i s mezerou), shell ho parsuje.
    internal static string? Pick(IEnumerable<Entry> entries, string server, int restPort) =>
        entries.Where(e => !string.IsNullOrEmpty(e.Idfa)
                           && string.Equals(e.Server?.Trim(), server.Trim(), StringComparison.OrdinalIgnoreCase)
                           && int.TryParse(e.RestPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
                           && port == restPort)
               .OrderByDescending(e => e.IsDefault)
               .Select(e => e.Idfa)
               .FirstOrDefault();
}
