using System.Diagnostics;
using System.Text.Json.Nodes;

namespace TemplateDesigner.Serialization;

/// <summary>
/// Migrátor schématu Template JSON. Při loadu detekuje verzi v JSON
/// (top-level <c>"version"</c> field, semver string jako <c>"1.0.0"</c>) a aplikuje
/// pipeline migrací sekvenčně do aktuální verze <see cref="CurrentVersion"/>.
///
/// <para>
/// Pipeline pattern: každá <see cref="ITemplateMigration"/> deklaruje svou FromVersion;
/// migrator aplikuje všechny migrace s FromVersion ≥ currentVersion v sortedOrder.
/// Po migraci je verze v JSON nastavena na <see cref="CurrentVersion"/>.
/// </para>
///
/// <para>
/// V Fázi 1 (2026-05-25) je migrations list **prázdný** — žádná konkrétní breaking
/// change zatím nepadla. Framework je připraven; při budoucí breaking change přidej
/// novou <c>ITemplateMigration</c> impl + bumpni <see cref="CurrentVersion"/>.
/// Detail convention: <c>docs/template-schema-versions.md</c>.
/// </para>
/// </summary>
public class TemplateMigrator
{
    /// <summary>Aktuální nejvyšší podporovaná verze Template JSON.</summary>
    public const string CurrentVersion = "1.0.0";

    /// <summary>
    /// Registrované migrace, sortovány podle FromVersion vzestupně.
    /// Prázdný seznam = žádná breaking change zatím; framework je no-op.
    /// </summary>
    private readonly List<ITemplateMigration> _migrations = [
        // new MigrationFrom1To2(),  // až přijde první breaking change
    ];

    /// <summary>
    /// Spustí migraci nad JSON dokumentem. Vrátí (migrovaný JSON, wasMigrated flag,
    /// detekovaná původní verze).
    ///
    /// <para>
    /// wasMigrated = true znamená, že verze v souboru byla starší a alespoň jedna
    /// migrace proběhla. UI by mělo zobrazit warning ("uložením přepíšeš starou verzi").
    /// </para>
    /// </summary>
    public (JsonNode Migrated, bool WasMigrated, string OriginalVersion) Migrate(JsonNode root)
    {
        string fromVersion = ReadVersion(root);
        string targetVersion = CurrentVersion;

        if (fromVersion == targetVersion)
            return (root, WasMigrated: false, OriginalVersion: fromVersion);

        bool applied = false;
        foreach (var migration in _migrations.OrderBy(m => m.FromVersion, VersionComparer.Instance))
        {
            // Aplikuj migraci pokud její FromVersion je ≥ aktuální verze v JSON
            if (VersionComparer.Instance.Compare(migration.FromVersion, fromVersion) >= 0)
            {
                try
                {
                    root = migration.Migrate(root);
                    applied = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TemplateMigrator] Migration {migration.GetType().Name} failed: {ex.Message}");
                    // Pokračujeme s dalšími migracemi — partial migration je lepší než žádná
                }
            }
        }

        // Po pipeline nastav target version, i když nebyla aplikována žádná migrace
        // (zachová original version v případě že _migrations je prázdný a fromVersion < target).
        root["version"] = targetVersion;

        return (root, WasMigrated: applied || fromVersion != targetVersion, OriginalVersion: fromVersion);
    }

    /// <summary>
    /// Načte verzi z JSON root. Chybějící nebo neplatná verze → "1.0.0" (assume oldest known).
    /// </summary>
    private static string ReadVersion(JsonNode root)
    {
        try
        {
            var node = root["version"];
            if (node is null) return "1.0.0";
            string? value = node.GetValue<string>();
            return string.IsNullOrWhiteSpace(value) ? "1.0.0" : value;
        }
        catch { return "1.0.0"; }
    }
}

/// <summary>
/// Konkrétní migrace mezi dvěma verzemi schématu Template JSON.
/// Implementace by měla být **idempotentní** (opakovaná aplikace = stejný výsledek)
/// a **defensive** (chybějící pole → default, neházet zbytečné exceptions).
/// </summary>
public interface ITemplateMigration
{
    /// <summary>Z které verze tato migrace transformuje (např. "1.0.0" → migruje na "2.0.0").</summary>
    string FromVersion { get; }

    /// <summary>Krátký popis pro logging / docs.</summary>
    string Description { get; }

    /// <summary>Transformuje root JsonNode. Může modifikovat in-place i vracet nový.</summary>
    JsonNode Migrate(JsonNode root);
}

/// <summary>
/// Comparer pro semver-like verze ("1.0.0", "1.1.0", "2.0.0").
/// Tolerantní — chybějící componenty traktuje jako 0, non-numeric komponent → 0.
/// </summary>
internal class VersionComparer : IComparer<string>
{
    public static readonly VersionComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        var xParts = ParseParts(x);
        var yParts = ParseParts(y);
        int len = Math.Max(xParts.Length, yParts.Length);
        for (int i = 0; i < len; i++)
        {
            int xp = i < xParts.Length ? xParts[i] : 0;
            int yp = i < yParts.Length ? yParts[i] : 0;
            if (xp != yp) return xp.CompareTo(yp);
        }
        return 0;
    }

    private static int[] ParseParts(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return [];
        return v.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
    }
}
