using System.Globalization;

namespace EDControls.EDViewModel;

/// <summary>
/// Usage telemetrie - zrcadlo <c>EDUsageTracker</c> z legacy EDISONu (EDLIBRARY).
/// Sbírá do paměti záznamy „které okno / který control byl použit" a při zavření okna je
/// dávkově odešle do Caché (<c>ALVA.ISY.ISYCloud.APIdotNet.Telemetry.LogUsageBatch</c>);
/// idfa+userid doplní server ze session. Agregaci do globalu a push na portál m-line při
/// instalaci aktualizace řeší existující Caché strana (ISYCloud), pro EDISON2 beze změny.
/// Vše je best-effort: telemetrie nesmí NIKDY shodit aplikaci ani blokovat zavření okna.
/// </summary>
public static class EDUsageTelemetry
{
    private sealed record UsageRecord(DateTime Ts, string Form, string Control);

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
            lock (Sync)
            {
                if (Buffer.Count >= MaxBuffer) return;
                Buffer.Add(new UsageRecord(DateTime.Now, form!, control!));
            }
        }
        catch
        {
            // best-effort - polknout
        }
    }

    /// <summary>
    /// Odešle nasbírané záznamy do Caché a vyprázdní buffer. Best-effort: případnou chybu
    /// polkne a záznamy zahodí (neblokuje zavření okna).
    /// </summary>
    public static void Flush(IMLRest? mlRest)
    {
        List<UsageRecord> batch;
        try
        {
            if (mlRest is null) return;
            lock (Sync)
            {
                if (Buffer.Count == 0) return;
                batch = [.. Buffer];
                Buffer.Clear();
            }
        }
        catch
        {
            return;
        }

        try
        {
            char del = mlRest.Delimiter;
            // object[] záměrně (ne CVar): legacy Caché metoda čte numerické subscripty ^||ML("IN",i).
            object[] input = [.. batch.Select(r =>
                $"usage{del}{r.Form}{del}{r.Control}{del}{r.Ts.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}")];
            mlRest.QueryOrThrow("ALVA.ISY.ISYCloud.APIdotNet.Telemetry", "LogUsageBatch", input);
        }
        catch
        {
            // Odeslání selhalo (odpojený server / chybí ISYCloud) - zahodit, best-effort.
        }
    }

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
