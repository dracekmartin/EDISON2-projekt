namespace EDControls.ErrorReporting;

/// <summary>Co se má s chybou stát: poslat hned, jen započítat, nebo poslat souhrn opakování.</summary>
internal enum ThrottleAction
{
    /// <summary>První výskyt v okně - poslat.</summary>
    Send,
    /// <summary>Opakování uvnitř okna - jen započítat, neposílat.</summary>
    Suppress,
    /// <summary>Okno vypršelo a mezitím se to opakovalo - poslat souhrn s počtem.</summary>
    SendSummary,
}

internal readonly record struct ThrottleDecision(ThrottleAction Action, int RepeatCount);

/// <summary>
/// Deduplikace hlášení. Bez ní je automatické odesílání zahlcení vlastní podpory: chyba
/// v <c>OnPaint</c> nebo v timeru se umí opakovat stokrát za minutu a suppression list
/// vědomě nemáme, takže projde i běžný provozní stav typu <c>Lock exist failed!</c>.
/// Čas se předává zvenčí, aby šlo testovat bez čekání.
/// </summary>
internal sealed class ErrorThrottle
{
    public const int DefaultMaxReports = 50;
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(5);

    private sealed class Entry
    {
        public DateTime WindowStart;
        public int SuppressedCount;
    }

    private readonly Dictionary<string, Entry> _entries = [];
    private readonly TimeSpan _window;
    private readonly int _maxReportsPerRun;
    private readonly object _lock = new();
    private int _sent;

    public ErrorThrottle(TimeSpan? window = null, int maxReportsPerRun = DefaultMaxReports)
    {
        _window = window ?? DefaultWindow;
        _maxReportsPerRun = maxReportsPerRun;
    }

    /// <summary>Kolik hlášení už v tomhle běhu odešlo (proti stropu <c>maxReportsPerRun</c>).</summary>
    public int SentCount { get { lock (_lock) return _sent; } }

    /// <summary>
    /// Klíč skupiny: typ výjimky + prvních pár rámců stacku + okno. Celý stack ne - rámce
    /// níž se mezi výskyty téže chyby liší (jiný handler, jiná hloubka) a skupina by se
    /// rozpadla na samostatné záznamy, což je přesně to, čemu deduplikace brání.
    /// </summary>
    public static string BuildKey(Exception ex, string? formId, int frames = 5)
    {
        ArgumentNullException.ThrowIfNull(ex);

        var lines = (ex.StackTrace ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Take(frames)
            .Select(l => l.Trim());

        return $"{ex.GetType().FullName}|{formId}|{string.Join(';', lines)}";
    }

    /// <summary>Rozhodne, co s výskytem udělat. <paramref name="now"/> se předává kvůli testům.</summary>
    public ThrottleDecision Register(string key, DateTime now)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        lock (_lock)
        {
            if (_sent >= _maxReportsPerRun)
            {
                // Strop na běh: dál jen počítáme, ať se z chybové smyčky nestane DoS na podporu.
                if (_entries.TryGetValue(key, out var capped)) capped.SuppressedCount++;
                return new ThrottleDecision(ThrottleAction.Suppress, 0);
            }

            if (!_entries.TryGetValue(key, out var entry))
            {
                _entries[key] = new Entry { WindowStart = now };
                _sent++;
                return new ThrottleDecision(ThrottleAction.Send, 1);
            }

            if (now - entry.WindowStart < _window)
            {
                entry.SuppressedCount++;
                return new ThrottleDecision(ThrottleAction.Suppress, 0);
            }

            // Okno vypršelo. Byla-li mezitím opakování, jde ven souhrn (+1 = tenhle výskyt).
            var repeats = entry.SuppressedCount;
            entry.WindowStart = now;
            entry.SuppressedCount = 0;
            _sent++;
            return repeats > 0
                ? new ThrottleDecision(ThrottleAction.SendSummary, repeats + 1)
                : new ThrottleDecision(ThrottleAction.Send, 1);
        }
    }

    internal void ResetForTests()
    {
        lock (_lock)
        {
            _entries.Clear();
            _sent = 0;
        }
    }
}
