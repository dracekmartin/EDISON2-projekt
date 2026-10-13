using System;
using System.Collections.Generic;
using System.Linq;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.LoginWindow;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2;

/// <summary>
/// Sdílená logika pro výběr a přepnutí firmy. <see cref="PickInitial"/> řeší startup
/// (<see cref="AppBootstrapper"/>), <see cref="Switch"/> runtime přepnutí ve VM. Drift mezi oběma cestami
/// (priorita firmy, ACTIVITY zámek, licenční retry, rollback) byl historicky zdrojem
/// bugů - proto společný service. Čte MLRest/LicenceLoader/IsAdmin/Popup/Nav lazily z
/// <see cref="EDServices"/>, takže po user switch automaticky vidí novou session.
/// </summary>
/// <param name="flushTelemetry">Dopsání zápisů na pozadí a telemetrie staré firmy; null = <see cref="EDUsageTelemetry.Flush"/>.</param>
public class CompanySwitcher(
    EDServices services,
    ILocalPrefs localPrefs,
    Func<bool> closeShellWindows,
    Action closeLegacyModules,
    Action<IMLRest?>? flushTelemetry = null)
{
    private readonly Action<IMLRest?> _flushTelemetry = flushTelemetry ?? EDUsageTelemetry.Flush;

    /// <summary>
    /// Vybere výchozí firmu pro startup: priorita <paramref name="preferredIdfa"/> →
    /// <c>IsDefault</c> flag → <c>companies[0]</c>. Stateless, volá se v <see cref="AppBootstrapper"/>
    /// před vytvořením MLRest connection.
    /// </summary>
    public static CompanyStruct PickInitial(IReadOnlyList<CompanyStruct> companies, string? preferredIdfa)
    {
        var match = companies.FirstOrDefault(c => c.Id == preferredIdfa);
        if (match.Id == null) match = companies.FirstOrDefault(c => c.IsDefault);
        if (match.Id == null) match = companies[0];
        return match;
    }

    /// <summary>
    /// Firmy, do kterých smí přihlášený uživatel (legacy <c>GetCompanyList(checkAccess=True)</c>).
    /// Server filtruje podle povolených databází uživatele a funkčního omezení DB v Idfa spojení,
    /// obojí jen se známým MLP - bez uživatele vrací všechny firmy serveru.
    /// </summary>
    public static IReadOnlyList<CompanyStruct> LoadAllowed(IMLRest rest) => LegacyApi.CompanyList(rest, checkAccess: true);

    /// <summary>
    /// Firma nové session a povolené firmy k ní; null = uživatel nesmí nikam. Funkční omezení DB je per IDFa,
    /// proto se seznam z firmy dotazu po volbě jiné firmy načte znovu v ní (legacy CreateCompanyMenu až ve
    /// výsledné firmě). Vybraná firma v seznamu zůstane i bez oprávnění - index přepínače odpovídá MLRest.Idfa.
    /// </summary>
    public static (CompanyStruct Company, IReadOnlyList<CompanyStruct> Allowed)? PickForSession(
        ConnectionInfo query, string? preferredIdfa, Func<ConnectionInfo, IReadOnlyList<CompanyStruct>> loadAllowed)
    {
        var allowed = loadAllowed(query);
        if (allowed.Count == 0) return null;
        var company = PickInitial(allowed, preferredIdfa);
        if (company.Id != query.Idfa)
            allowed = loadAllowed(query with { Database = company.Database, Idfa = company.Id });
        if (!allowed.Any(c => c.Id == company.Id))
            allowed = [.. allowed, company];
        return (company, allowed);
    }

    /// <summary>Povolené firmy právě přihlášeného uživatele, pro kterého session ještě nevznikla.</summary>
    public static IReadOnlyList<CompanyStruct> LoadAllowed(ConnectionInfo conn, LoginResult login) =>
        LoadAllowed(new MLRestAdapter(LoginProbe(conn, login)));

    // Bez MLP a jména server nefiltruje a vrátí všechny firmy - identita z loginu musí jít s dotazem.
    internal static MLRest LoginProbe(ConnectionInfo conn, LoginResult login) =>
        new(conn.Server, conn.Port, conn.Database, conn.Idfa)
        {
            LoginName = login.UserName,
            Mlp = login.Mlp.ToString(),
        };

    /// <summary>
    /// Přepne firmu: WPF okna shellu se zavírají PŘED swapem Idfa, legacy moduly až po
    /// <see cref="LicenceAcquireOutcome.Accepted"/> - neúspěch tak nezabije rozpracovanou práci.
    /// Veto v Closing → <see cref="LicenceAcquireOutcome.Cancelled"/> ještě před swapem (přeživší
    /// okno by přes sdílený MLRest mluvilo s cizí firmou); při neúspěchu rollback MLRest na
    /// původní hodnoty. Confirm popup je odpovědnost callsite. <see cref="LicenceAcquireOutcome.Accepted"/>
    /// = MLRest má novou Idfa+Namespace a <see cref="EDServices.ActiveLicence"/> licenci cílové firmy.
    /// </summary>
    /// <param name="newIdfa">Cílová Idfa.</param>
    /// <param name="newNamespace">Cílový DataNamespace.</param>
    /// <param name="currentLicence">Aktuální licence; null = bez verify (testy / pre-licence stav).</param>
    public LicenceAcquireOutcome Switch(string newIdfa, string newNamespace, Lic? currentLicence)
    {
        var mlRest = services.RequireMLRest;
        var originalIdfa = mlRest.Idfa;
        var originalNamespace = mlRest.DataNamespace;

        // WPF okna shellu volají server přes sdílený MLRest - po swapu Idfa by jejich requesty
        // (save při zavření, refresh) šly do cizí firmy, proto se zavírají před ním. Legacy
        // moduly mají vlastní spojení (Idfa v launch args), swap se jich netýká → až po Accepted.
        if (!closeShellWindows())
            return LicenceAcquireOutcome.Cancelled;

        // Zavřená okna ještě dopisují na pozadí (geometrie, telemetrie, preference) pod starou firmou. Po jejím
        // odhlášení by je server odmítl (LockExist), proto počkat a dopsat ještě před výměnou Idfa.
        _flushTelemetry(mlRest);

        // Requesty pod novou Idfa (licenceLoader, VerifyLic) běží PŘED vznikem jejího ACTIVITY
        // zámku - LockExist při CacheLocks=1 by session shodil. Po dobu switche tedy bez kontrol;
        // při Accepted vrátí režim 1 LicenceVerifier, jinak se vrací původní hodnota.
        var originalCacheLocks = mlRest.CacheLocks;
        mlRest.CacheLocks = 0;
        mlRest.Idfa = newIdfa;
        mlRest.DataNamespace = newNamespace;

        // ACTIVITY zámek + počet licencí jsou per-IdFa - pro VerifyLic potřebujeme
        // licenci cílové firmy, ne původní (jinak bychom kontrolovali špatný limit).
        // Při překročení licencí + admin nabídne LicenceVerifier okno aktivity uživatelů.
        // Null currentLicence (typicky testy) → skip verify, switch je auto-accept.
        var outcome = currentLicence != null
            ? LicenceVerifier.TryAcquireSession(mlRest, services)
            : LicenceAcquireOutcome.Accepted;

        if (outcome == LicenceAcquireOutcome.Accepted)
        {
            // Null licence = verify přeskočen (testy / pre-licence stav) → LicenceVerifier
            // CacheLocks nenastavil, vrať původní režim.
            if (currentLicence == null) mlRest.CacheLocks = originalCacheLocks;

            // Kill legacy modulů PŘED logoutem staré firmy: sdílejí shell GUID s CacheLocks=1,
            // po zániku jejího ACTIVITY zámku by jim padal každý request na LockExist.
            closeLegacyModules();

            // Uvolnit ACTIVITY zámek staré firmy (per-IdFa) - jinak drží licenční slot té firmy
            // do server timeoutu. Až po Accepted (nová firma má zámek), aby rollback zůstal bezpečný.
            // Logout běží na klonu se starou idfa/namespace - sdílený MLRest (už na nové firmě) se nemění.
            // Skip při null licenci (testy / pre-licence stav) a při shodné firmě.
            if (currentLicence != null && originalIdfa != newIdfa)
            {
                // Úklid business zámků v Logoutu je GUID-wide a shell GUID přepnutí firmy přežije,
                // takže smete i ACTIVITY zámek nové firmy z VerifyLic. Po tu dobu musí jít session
                // s Locks=0 (jinak ji shodí LockExist) a zámek se pak zakládá znovu; ReacquireLock
                // vrátí Locks=1 sám, při selhání zůstane 0 - bez zámku se nemá co kontrolovat.
                mlRest.CacheLocks = 0;
                SessionLogout.Logout(mlRest, originalIdfa, originalNamespace);
                LicenceVerifier.ReacquireLock(mlRest, services.ActiveLicence);
            }

            localPrefs.LastIdfa = newIdfa;
            localPrefs.Save();
            return outcome;
        }

        mlRest.Idfa = originalIdfa;
        mlRest.DataNamespace = originalNamespace;
        mlRest.CacheLocks = originalCacheLocks;   // zámek staré firmy pořád existuje
        return outcome;
    }
}
