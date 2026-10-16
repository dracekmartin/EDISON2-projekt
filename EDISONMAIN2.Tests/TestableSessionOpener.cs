using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDControls;
using EDControls.EDViewModel;
using EDTesting;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Tests;

/// <summary>
/// <see cref="SessionOpener"/> se skutečným pořadím kroků a nahrazenými kroky na serveru. Kroky zapisuje
/// do <see cref="Calls"/>; volající může předat vlastní seznam, ať se kroky proloží s jeho vlastními.
/// </summary>
internal sealed class TestableSessionOpener(EDServices services, Action<IMLRest>? wire = null,
                                            List<string>? calls = null, StartupOptions? options = null)
    : SessionOpener(services, options ?? new StartupOptions(), wire ?? (_ => { }))
{
    public List<string> Calls { get; } = calls ?? [];

    public IReadOnlyList<CompanyStruct> AllowedCompanies { get; set; } = [new() { Id = "CB_DP", Database = "CB_DP" }];
    public (ConnectionInfo Conn, LoginResult Login)? AllowedQuery { get; private set; }
    public ConnectionInfo? EstablishedConnection { get; private set; }
    public IMLRest NewSessionMLRest { get; set; } = MLRestMocks.Create();
    public LicenceAcquireOutcome LicenceOutcome { get; set; } = LicenceAcquireOutcome.Accepted;
    public Exception? AcquireThrows { get; set; }
    public bool UpdateRequired { get; set; }
    public Exception? UpdateCheckThrows { get; set; }
    public Task<ReleaseNoticeResult>? ReleaseNotice { get; set; }

    public SessionContext CallEstablish(ConnectionInfo conn, LoginResult login) => base.EstablishSession(conn, login);

    protected override IReadOnlyList<CompanyStruct> LoadAllowedCompanies(ConnectionInfo conn, LoginResult login)
    {
        // Jen první dotaz: druhý (po volbě jiné firmy) se ptá v ní a výsledek je tentýž seznam.
        AllowedQuery ??= (conn, login);
        Calls.Add("allowed");
        return AllowedCompanies;
    }

    protected override SessionContext EstablishSession(ConnectionInfo conn, LoginResult login)
    {
        Calls.Add("establish");
        EstablishedConnection = conn;
        return new SessionContext(NewSessionMLRest, login);
    }

    protected override LicenceAcquireOutcome AcquireSession(SessionContext session)
    {
        Calls.Add("acquire");
        if (AcquireThrows is not null) throw AcquireThrows;
        return LicenceOutcome;
    }

    public bool? NoticeRequested { get; private set; }

    protected override (bool ShouldExit, Task<ReleaseNoticeResult>? Notice) CheckForUpdate(
        SessionContext session, bool startNotice)
    {
        Calls.Add("update");
        NoticeRequested = startNotice;
        if (UpdateCheckThrows is not null) throw UpdateCheckThrows;
        return (UpdateRequired, UpdateRequired || !startNotice ? null : ReleaseNotice);
    }
}
