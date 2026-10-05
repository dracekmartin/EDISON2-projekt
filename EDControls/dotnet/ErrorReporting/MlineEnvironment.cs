using System.Net.NetworkInformation;
using Microsoft.Win32;

namespace EDControls.ErrorReporting;

/// <summary>
/// Rozpoznání „tohle je náš vlastní stroj, ne zákazníkův". Zrcadlí legacy
/// <c>ALVA.CORE.Network.IsDomainMline</c> včetně registrového klíče - ten tam je kvůli
/// virtuálům mimo doménu a bez něj by nám vývojářské VM padaly do zákaznických statistik.
/// Na rozdíl od legacy se interní běh nezahazuje, jen označí (<c>ErrorReport.InternalSuffix</c>).
/// </summary>
public static class MlineEnvironment
{
    private const string MlineDomain = "main.m-line.cz";
    private const string RegistryPath = @"Software\Edison";
    private const string RegistryValue = "MlineDomain";

    private static bool? _cached;

    /// <summary>Běží aplikace na m-line stroji? Zjištění je drahé, drží se v paměti.</summary>
    public static bool IsInternal => _cached ??= Detect();

    private static bool Detect()
    {
        try
        {
            var domain = IPGlobalProperties.GetIPGlobalProperties().DomainName ?? "";
            if (domain.Equals(MlineDomain, StringComparison.OrdinalIgnoreCase)) return true;
        }
        catch
        {
            // Bez domény prostě pokračujeme registrem.
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            return IsTruthyRegistryValue(key?.GetValue(RegistryValue)?.ToString());
        }
        catch
        {
            return false;
        }
    }

    // Legacy zapisuje "1"/"0", ale čte přes CBool - snese i "True".
    internal static bool IsTruthyRegistryValue(string? value) =>
        value is not null &&
        (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));
}
