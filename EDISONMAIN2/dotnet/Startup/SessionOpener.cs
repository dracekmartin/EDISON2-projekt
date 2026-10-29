using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDControls;
using EDControls.EDViewModel;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2;

public enum SessionOpenOutcome
{
    /// <summary>Session běží pod novým uživatelem, licence přijatá, přihlášení zapsané.</summary>
    Opened,
    /// <summary>Uživatel zrušil změnu hesla, nesmí do žádné firmy, nebo krok volajícího session odmítl.
    /// Nová session nevznikla.</summary>
    Cancelled,
    /// <summary>Licence přesáhla limit (non-admin), nebo server vrátil neznámou chybu.</summary>
    LicenceRefused,
    /// <summary>Admin v licenčním retry zvolil Storno.</summary>
    LicenceCancelled,
    /// <summary>Spustila se aktualizace (nebo je instalace rozbitá), hlášku už uživatel viděl. Aplikace má skončit.</summary>
    UpdateRequired,
}

/// <param name="Session">Nová session; null, když nevznikla.</param>
/// <param name="Companies">Povolené firmy nového uživatele, jen u <see cref="SessionOpenOutcome.Opened"/>.</param>
/// <param name="ReleaseNotice">Běžící notice-only kontrola release serveru, jen u <see cref="SessionOpenOutcome.Opened"/>.</param>
public sealed record SessionOpenResult(
    SessionOpenOutcome Outcome,
    SessionContext? Session = null,
    IReadOnlyList<CompanyStruct>? Companies = null,
    Task<ReleaseNoticeResult>? ReleaseNotice = null);

/// <summary>Kroky, ve kterých se start a přihlášení jiného uživatele liší.</summary>
public sealed record SessionOpenSteps
{
    /// <summary>Dialog vynucené změny hesla; false = uživatel zrušil.</summary>
    public required Func<LoginResult, bool> ChangePassword { get; init; }

    /// <summary>Po výběru firmy, před založením session; false = zrušit (veto okna při přepnutí uživatele).</summary>
    public Func<bool> BeforeEstablish { get; init; } = () => true;

    /// <summary>Po přijaté licenci, před update checkem.</summary>
    public Action<SessionContext> AfterAccepted { get; init; } = _ => { };

    /// <summary>Spustit notice-only kontrolu release serveru; výsledek čte jen status bar nového shellu.</summary>
    public bool StartsReleaseNotice { get; init; }
}

/// <summary>
/// Cesta od přihlášení k běžící session, společná pro start (<see cref="AppBootstrapper"/>) i přihlášení
/// jiného uživatele (<see cref="SessionSwitcher"/>): vynucená změna hesla → výběr firmy → session →
/// licence → update check → zápis přihlášení. Nový krok po loginu patří sem, jinak ho jedna cesta nedostane.
/// Kroky sahající na server jsou <c>protected virtual</c> kvůli testům.
/// </summary>
/// <param name="wireActivityFailed">Napojení force-logout / version gate handlerů na nový MLRest.</param>
/// <param name="options">Režim startu - session přihlášení jiného uživatele má stejný GUID a verzi jako startovní.</param>
public class SessionOpener(EDServices services, StartupOptions options, Action<IMLRest> wireActivityFailed,
                           ILocalPrefs? prefs = null)
{
    /// <param name="query">Spojení, ve kterém se zjistí povolené firmy nového uživatele.</param>
    /// <param name="preferredIdfa">Firma, ve které má session vzniknout, když do ní uživatel smí.</param>
    public SessionOpenResult Open(LoginResult login, ConnectionInfo query, string? preferredIdfa, SessionOpenSteps steps)
    {
        // Feature #17: server přihlásí i s příznakem změnit heslo. Příznak ale nuluje už v Login,
        // další pokus po zrušení proto projde bez změny.
        if (login.ChangePasswordNeeded && !steps.ChangePassword(login))
            return new(SessionOpenOutcome.Cancelled);

        // Nový uživatel startuje v povolené firmě (legacy frmLoginEx.SelectedCompany), do nepovolené se nepřihlásí.
        if (CompanySwitcher.PickForSession(query, preferredIdfa, conn => LoadAllowedCompanies(conn, login))
            is not { } picked)
        {
            services.PopupDisplayer.Info("Uživatel nemá přístup k žádné firmě.");
            return new(SessionOpenOutcome.Cancelled);
        }
        if (!steps.BeforeEstablish()) return new(SessionOpenOutcome.Cancelled);

        var session = EstablishSession(query with { Database = picked.Company.Database, Idfa = picked.Company.Id }, login);
        wireActivityFailed(session.MLRest);
        switch (AcquireSession(session))
        {
            case LicenceAcquireOutcome.Refused: return new(SessionOpenOutcome.LicenceRefused, session);
            case LicenceAcquireOutcome.Cancelled: return new(SessionOpenOutcome.LicenceCancelled, session);
        }

        steps.AfterAccepted(session);
        var update = CheckForUpdate(session, steps.StartsReleaseNotice);
        if (update.ShouldExit) return new(SessionOpenOutcome.UpdateRequired, session);
        // Až po update checku: legacy start ukončený updatem přihlášení nezapsal (frmNoMDI.vb:4917 před :4948).
        LicenceVerifier.LogLogin(session.MLRest);
        return new(SessionOpenOutcome.Opened, session, picked.Allowed, update.Notice);
    }

    protected virtual IReadOnlyList<CompanyStruct> LoadAllowedCompanies(ConnectionInfo conn, LoginResult login)
        => CompanySwitcher.LoadAllowed(conn, login);

    protected virtual SessionContext EstablishSession(ConnectionInfo conn, LoginResult login)
        => SessionFactory.Establish(conn, login, services, options);

    protected virtual LicenceAcquireOutcome AcquireSession(SessionContext session)
        => LicenceVerifier.TryAcquireSession(session.MLRest, services);

    /// <summary>
    /// Klientský update check - mandatory, bez user volby (stanice nemá co rozhodovat).
    /// <c>ShouldExit</c> = aplikace se má ukončit (spawnutý EDUPDATE / rozbitý stav);
    /// <c>Notice</c> = běžící notice-only kontrola release serveru pro status bar (jen se <paramref name="startNotice"/>).
    /// </summary>
    protected virtual (bool ShouldExit, Task<ReleaseNoticeResult>? Notice) CheckForUpdate(
        SessionContext session, bool startNotice)
    {
        var updater = CreateUpdater();
        // Klon s Locks=0: session nese verzi stanice a po FULL vydání by ji server version gate
        // odmítl dřív, než se update spustí (legacy shell kontroluje ještě před nastavením verze).
        var settings = updater.LoadSettings(session.MLRest.With(cacheLocks: 0));
        // Před Check: spuštěný updater log předchozího běhu přepíše. Serverovou instalaci updater nemění.
        if (!settings.ServerInstallation) ReportUpdaterFailure();
        if (StationUpdate.Apply(updater, settings, updater.Check(settings), services.PopupDisplayer)) return (true, null);
        // Notice-only kontrola release serveru M-line běží paralelně se stavbou shellu.
        return (false, startNotice ? Task.Run(() => updater.GetReleaseNotice(settings, VerzeInf.Current)) : null);
    }

    protected virtual Updater CreateUpdater() => new();

    /// <summary>Neúspěšný běh legacy updateru stanice do telemetrie, jednou za běh (<see cref="UpdaterFailureReport"/>).</summary>
    protected virtual void ReportUpdaterFailure()
    {
        if (prefs is null || Updater.ActiveKind != ClientUpdaterKind.LegacyEdupdate) return;
        UpdaterFailureReport.ForInstallation(prefs).ReportIfNew();
    }
}
