using System.Net.NetworkInformation;
using Microsoft.Win32;

namespace EDControls.ErrorReporting;

/// <summary>
/// Rozpoznání „tohle je náš vlastní stroj, ne zákazníkův". Zrcadlí legacy
/// <c>ALVA.CORE.Network.IsDomainMline</c> včetně registrového klíče - ten tam je kvůli
/// virtuálům mimo doménu a bez něj by nám vývojářské VM padaly do zákaznických statistik.
/// Jediná implementace: čte ji hlášení chyb i strom shellu (mlineOnly uzly). Na rozdíl od legacy
/// se interní běh v hlášení nezahazuje, jen označí (<c>ErrorReport.InternalSuffix</c>).
/// </summary>
public static class MlineEnvironment
{
    private const string MlineDomain = "main.m-line.cz";
    private const string RegistryPath = @"Software\Edison";
    private const string RegistryValue = "MlineDomain";

    private static bool? _cached;

    /// <summary>Běží aplikace na m-line stroji? Zjištění je drahé, drží se v paměti.</summary>
    public static bool IsInternal => _cached ??= Detect(ReadDomainName, ReadRegistryValue);

    /// <summary>
    /// DNS doména stanice <c>main.m-line.cz</c>, nebo override v registru
    /// <c>HKCU\Software\Edison\MlineDomain</c> (zapisuje legacy SetPcVirtualDomainMline).
    /// </summary>
    internal static bool Detect(Func<string?> domainName, Func<string?> registryValue) =>
        string.Equals(domainName(), MlineDomain, StringComparison.OrdinalIgnoreCase)
        || IsTruthyRegistryValue(registryValue());

    // Legacy zapisuje "1"/"0" a čte přes VB CBool: z něj stačí bool a celé číslo (nenulové = true).
    internal static bool IsTruthyRegistryValue(string? value)
    {
        if (value is null) return false;
        if (bool.TryParse(value, out var b)) return b;
        return int.TryParse(value, out var i) && i != 0;
    }

    private static string? ReadDomainName()
    {
        try { return IPGlobalProperties.GetIPGlobalProperties().DomainName; }
        catch { return null; }   // bez domény rozhodne registr
    }

    private static string? ReadRegistryValue()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            return key?.GetValue(RegistryValue)?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
