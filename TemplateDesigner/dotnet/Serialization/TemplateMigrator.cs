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
/// První migrace = <see cref="MigrationHighlightRules"/> (1.0.0 → 1.1.0). Při další breaking
/// change přidej novou <c>ITemplateMigration</c> impl do default listu + bumpni
/// <see cref="CurrentVersion"/>. Detail convention: <c>docs/template-schema-versions.md</c>.
/// </para>
/// </summary>
public class TemplateMigrator
{
    /// <summary>Aktuální nejvyšší podporovaná verze Template JSON.</summary>
    public const string CurrentVersion = "1.1.0";

    /// <summary>
    /// Registrované migrace, sortovány podle FromVersion vzestupně.
    /// Prázdný seznam = žádná breaking change zatím; framework je no-op.
    /// </summary>
    private readonly List<ITemplateMigration> _migrations;

    public TemplateMigrator() : this(null) { }

    /// <summary>Explicitní seznam migrací — pro testy pipeline; produkce používá default.</summary>
    public TemplateMigrator(IEnumerable<ITemplateMigration>? migrations)
    {
        _migrations = migrations?.ToList() ?? [
            new MigrationHighlightRules(),   // 1.0.0 → 1.1.0
        ];
    }

    /// <summary>
    /// Spustí migraci nad JSON dokumentem. Vrátí (migrovaný JSON, wasMigrated flag,
    /// detekovaná původní verze).
    ///
    /// <para>
    /// wasMigrated = true znamená, že verze v souboru byla starší a šablona byla
    /// transformována/přerazítkována. UI by mělo zobrazit warning
    /// ("uložením přepíšeš starou verzi").
    /// </para>
    /// </summary>
    /// <exception cref="TemplateDeserializationException">
    /// Verze šablony je novější než <see cref="CurrentVersion"/> (forward-compat guard, R5 —
    /// tiché přerazítkování dolů by ztratilo data novější verze), nebo některá migrace selhala
    /// (částečně zmigrovaná šablona se nesmí tiše otevřít).
    /// </exception>
    public (JsonNode Migrated, bool WasMigrated, string OriginalVersion) Migrate(JsonNode root)
    {
        string fromVersion = ReadVersion(root);
        string targetVersion = CurrentVersion;

        // Sémantické porovnání (VersionComparer): "1.0" == "1.0.0" — string equality by
        // kosmeticky odlišnou verzi zbytečně hnala přes pipeline a hlásila migraci.
        int cmp = VersionComparer.Instance.Compare(fromVersion, targetVersion);

        if (cmp == 0)
            return (root, WasMigrated: false, OriginalVersion: fromVersion);

        if (cmp > 0)
            throw new TemplateDeserializationException(
                $"Šablona byla uložena v novější verzi aplikace (formát {fromVersion}, " +
                $"tato verze podporuje {targetVersion}). Aktualizujte TemplateDesigner.");

        foreach (var migration in _migrations.OrderBy(m => m.FromVersion, VersionComparer.Instance))
        {
            // Aplikuj migraci pokud její FromVersion je ≥ aktuální verze v JSON
            if (VersionComparer.Instance.Compare(migration.FromVersion, fromVersion) >= 0)
            {
                try
                {
                    root = migration.Migrate(root);
                }
                catch (Exception ex)
                {
                    throw new TemplateDeserializationException(
                        $"Migraci šablony ze starší verze {fromVersion} se nepodařilo dokončit " +
                        $"({migration.Description}). Šablona nebyla otevřena, aby nedošlo " +
                        $"k poškození dat: {ex.Message}", ex);
                }
            }
        }

        // Starší verze → přerazítkovat na aktuální (i bez registrovaných migrací)
        root["version"] = targetVersion;

        return (root, WasMigrated: true, OriginalVersion: fromVersion);
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
