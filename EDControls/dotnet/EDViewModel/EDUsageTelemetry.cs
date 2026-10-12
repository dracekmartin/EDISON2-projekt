using System.Globalization;

namespace EDControls.EDViewModel;

/// <summary>
/// Usage telemetrie - zrcadlo <c>EDUsageTracker</c> z legacy EDISONu (EDLIBRARY).
/// Sbírá do paměti záznamy „které okno / který control byl použit" a při zavření okna je
/// dávkově odešle do Caché (<c>ALVA.ISY.ISYCloud.APIdotNet.Telemetry.LogUsageBatch</c>);
/// idfa+userid doplní server ze session, proto záznam nese firmu, ve které vznikl. Agregaci do globalu
/// a push na portál m-line při instalaci aktualizace řeší existující Caché strana (ISYCloud), pro EDISON2 beze změny.
/// Vše je best-effort: telemetrie nesmí NIKDY shodit aplikaci ani blokovat zavření okna.
/// </summary>
public static class EDUsageTelemetry
{
    private sealed record UsageRecord(DateTime Ts, string Form, string Control, string? Idfa, string? DataNamespace);

    private static readonly object Sync = new();
    private static readonly List<UsageRecord> Buffer = [];

    /// <summary>Pojistka proti neomezenému růstu bufferu (kdyby flush dlouho neproběhl).</summary>
    private const int MaxBuffer = 10000;

    /// <summary>Globální vypínač sledování.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>
    /// Zaznamená použití controlu na okně. Pouze zápis do paměti, žádné I/O. Nikdy nehazí.
    /// </summary>
    public static void Track(string? form, string? control)
    {
        try
        {
            if (!Enabled || string.IsNullOrEmpty(form) || string.IsNullOrEmpty(control)) return;
            var session = EDServices.CurrentOrNull?.MLRest;
            var record = new UsageRecord(DateTime.Now, form!, control!, session?.Idfa, session?.DataNamespace);
            lock (Sync)
            {
                if (Buffer.Count >= MaxBuffer) return;
                Buffer.Add(record);
            }
        }
        catch
        {
            // best-effort - polknout
        }
    }

    /// <summary>
    /// Odešle nasbírané záznamy do Caché a vyprázdní buffer. Záznamy jiné firmy než <paramref name="mlRest"/>
    /// jdou na její klon. Nejdřív počká na rozjeté zápisy na pozadí (nejvýš <see cref="BackgroundWrites.Timeout"/>):
    /// shell volá Flush při ukončení a přepnutí uživatele těsně před odhlášením session.
    /// Best-effort: případnou chybu polkne a záznamy zahodí.
    /// </summary>
    public static void Flush(IMLRest? mlRest)
    {
        BackgroundWrites.WaitPending(BackgroundWrites.Timeout);
        foreach (var (idfa, dataNamespace, input) in TakeBatches(mlRest))
        {
            try
            {
                Send(IsSession(mlRest!, idfa, dataNamespace) ? mlRest! : OtherCompany(mlRest!, idfa, dataNamespace), input);
            }
            catch
            {
                // Odeslání selhalo (odpojený server / chybí ISYCloud) - zahodit, best-effort.
            }
        }
    }

    /// <summary>Jako <see cref="Flush"/>, ale dávky jdou na pozadí: zavření okna nečeká na síť.</summary>
    internal static void FlushInBackground(IMLRest? mlRest)
    {
        foreach (var (idfa, dataNamespace, input) in TakeBatches(mlRest))
        {
            try
            {
                BackgroundWrites.Register(IsSession(mlRest!, idfa, dataNamespace)
                    ? new RestCallRunner(() => mlRest!).RunAsync(rest => Send(rest, input))
                    : Task.Run(() => Send(OtherCompany(mlRest!, idfa, dataNamespace), input)));
            }
            catch
            {
                // Volá se z Window.Closed, výjimka by propadla až do DispatcherUnhandledException.
            }
        }
    }

    /// <summary>Vybere buffer a rozdělí ho po firmách. Bez session nic nevybere, záznamy počkají na další flush.</summary>
    private static List<(string? Idfa, string? DataNamespace, object[] Input)> TakeBatches(IMLRest? mlRest)
    {
        try
        {
            if (mlRest is null) return [];
            List<UsageRecord> records;
            lock (Sync)
            {
                if (Buffer.Count == 0) return [];
                records = [.. Buffer];
                Buffer.Clear();
            }
            char del = mlRest.Delimiter;
            return [.. records.GroupBy(r => (r.Idfa, r.DataNamespace))
                .Select(batch => (batch.Key.Idfa, batch.Key.DataNamespace, Input(batch, del)))];
        }
        catch
        {
            return [];
        }
    }

    private static bool IsSession(IMLRest mlRest, string? idfa, string? dataNamespace) =>
        idfa is null || (idfa == mlRest.Idfa && dataNamespace == mlRest.DataNamespace);

    // Přepnutí firmy odhlásilo zámek staré firmy, s CacheLocks=1 by server dávku odmítl (LockExist).
    private static IMLRest OtherCompany(IMLRest mlRest, string? idfa, string? dataNamespace) =>
        mlRest.With(cacheLocks: 0, idfa: idfa, dataNamespace: dataNamespace);

    // object[] záměrně (ne CVar): legacy Caché metoda čte numerické subscripty ^||ML("IN",i).
    private static object[] Input(IEnumerable<UsageRecord> batch, char del) => [.. batch.Select(r =>
        $"usage{del}{r.Form}{del}{r.Control}{del}{r.Ts.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}")];

    private static object[] Send(IMLRest target, object[] input) =>
        target.QueryOrThrow("ALVA.ISY.ISYCloud.APIdotNet.Telemetry", "LogUsageBatch", input);

    internal static void ResetForTests()
    {
        lock (Sync) Buffer.Clear();
        Enabled = true;
    }

    internal static int BufferCountForTests
    {
        get { lock (Sync) return Buffer.Count; }
    }
}
