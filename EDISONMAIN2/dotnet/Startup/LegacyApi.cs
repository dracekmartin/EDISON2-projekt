using System;
using System.Collections.Generic;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2;

/// <summary>
/// Hranice k legacy DLL (EDISONAPI, EDLicence, EDUsers) a k událostem MLRest. Ty chtějí konkrétní
/// <see cref="MLRest"/>, shell pracuje s <see cref="IMLRest"/>. Jediné místo, které bere <see cref="MLRestAdapter.Inner"/>;
/// jiná implementace (mock) tu skončí výjimkou, ne tichým přeskočením.
/// </summary>
internal static class LegacyApi
{
    /// <summary>
    /// Firmy serveru (legacy <c>GetCompanyList</c>), řazené podle názvu. <paramref name="checkAccess"/> = jen povolené
    /// uživateli spojení: server filtruje podle MLP a funkčního omezení DB v Idfa, bez MLP vrátí všechny.
    /// </summary>
    public static IReadOnlyList<CompanyStruct> CompanyList(IMLRest rest, bool checkAccess)
    {
        string errState = "";
        var companies = GetCompanyList(ref errState, Inner(rest), checkAccess);
        if (!string.IsNullOrEmpty(errState))
            throw new InvalidOperationException(errState);
        return companies;
    }

    /// <summary>Načtení aktivní licence session (EDLicence) pro <see cref="EDServices.LicenceLoader"/>.</summary>
    public static Func<Lic?> ActiveLicenceLoader(IMLRest rest, Action<string> showInfo) =>
        new LicenceService(Inner(rest), showInfo).GetActiveLicence;

    /// <inheritdoc cref="LicenceService.SyncFromMline"/>
    public static LicenceService.MlineSyncResult SyncFromMline(IMLRest rest) => LicenceService.SyncFromMline(Inner(rest));

    /// <summary>
    /// Konec session ze strany serveru: zaniklý ACTIVITY zámek (timeout, výpadek, odpojení adminem) a jiná verze
    /// (per-request version gate). Události má jen sdílená instance, klony session je na ni přeposílají.
    /// </summary>
    public static void OnSessionEnd(IMLRest rest, Action activityLost, Action versionMismatch)
    {
        var inner = Inner(rest);
        inner.EdisonActivityFailed += (_, _) => activityLost();
        inner.EdisonVersionFailed += (_, _) => versionMismatch();
    }

    private static MLRest Inner(IMLRest rest) => rest is MLRestAdapter adapter
        ? adapter.Inner
        : throw new InvalidOperationException($"Legacy API potřebuje spojení MLRestAdapter, dostalo {rest.GetType().Name}.");
}
