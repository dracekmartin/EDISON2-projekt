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
    /// <summary>Povolené firmy pro dotaz v dané firmě (Idfa); firma mimo slovník dostane <see cref="AllowedCompanies"/>.</summary>
    public Dictionary<string, IReadOnlyList<CompanyStruct>> AllowedByIdfa { get; } = [];
    public (ConnectionInfo Conn, LoginResult Login)? AllowedQuery { get; private set; }
    public List<ConnectionInfo> AllowedQueries { get; } = [];
    public ConnectionInfo? EstablishedConnection { get; private set; }
    public List<ConnectionInfo> EstablishedConnections { get; } = [];
    public IMLRest NewSessionMLRest { get; set; } = MLRestMocks.Create();
    public LicenceAcquireOutcome LicenceOutcome { get; set; } = LicenceAcquireOutcome.Accepted;
    /// <summary>Výsledky licence pro jednotlivé pokusy; po vyčerpání platí <see cref="LicenceOutcome"/>.</summary>
    public Queue<LicenceAcquireOutcome> LicenceOutcomes { get; } = new();
    public Exception? AcquireThrows { get; set; }
    public bool UpdateRequired { get; set; }
    public Exception? UpdateCheckThrows { get; set; }
    public Task<ReleaseNoticeResult>? ReleaseNotice { get; set; }

    public TestableSessionOpener WithLicenceOutcomes(IEnumerable<LicenceAcquireOutcome> outcomes)
    {
        foreach (var outcome in outcomes) LicenceOutcomes.Enqueue(outcome);
        return this;
    }

    public SessionContext CallEstablish(ConnectionInfo conn, LoginResult login) => base.EstablishSession(conn, login);

    protected override IReadOnlyList<CompanyStruct> LoadAllowedCompanies(ConnectionInfo conn, LoginResult login)
    {
        // AllowedQuery jen první dotaz, další (po volbě jiné firmy) jsou v AllowedQueries.
        AllowedQuery ??= (conn, login);
        AllowedQueries.Add(conn);
        Calls.Add("allowed");
        return AllowedByIdfa.TryGetValue(conn.Idfa, out var allowed) ? allowed : AllowedCompanies;
    }

    protected override SessionContext EstablishSession(ConnectionInfo conn, LoginResult login)
    {
        Calls.Add("establish");
        EstablishedConnection = conn;
        EstablishedConnections.Add(conn);
        return new SessionContext(NewSessionMLRest, login);
    }

    protected override LicenceAcquireOutcome AcquireSession(SessionContext session)
    {
        Calls.Add("acquire");
        if (AcquireThrows is not null) throw AcquireThrows;
        return LicenceOutcomes.TryDequeue(out var outcome) ? outcome : LicenceOutcome;
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
