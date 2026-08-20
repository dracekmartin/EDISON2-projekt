using System.Diagnostics;
using TemplateDesigner.Models;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Services;

/// <summary>
/// Doručení systémových (M-line) šablon kanálem z helpdesku do větve <b>MLINE</b> zákaznické
/// databáze. Je to jen <b>výměna zdroje</b> proti <see cref="SystemTemplateSeeder"/>: cíl, přehled
/// šablon i chování (read-only, Ctrl+S dělá kopii do firmy) zůstávají stejné.
///
/// <para><b>Identitu šablony určuje <see cref="Template.Id"/> uvnitř JSONu</b>, ne identifikátor
/// v úložišti - podle něj se zapisuje do MLINE, takže opakované stažení je idempotentní a nevzniknou
/// duplicity. Kdo šablonu do úložiště nahrává, musí proto vnitřní GUID zachovat.</para>
///
/// <para>Stav „co už máme" cestuje v šabloně (<see cref="Template.ChannelOrigin"/>), takže patří
/// databázi firmy, ne počítači uživatele - jinak by každý uživatel stahoval totéž znovu.</para>
/// </summary>
public class SystemTemplateChannelSync(CacheTemplateService storage, TemplateChannelClient? channel = null)
{
    private readonly TemplateChannelClient _channel = channel ?? new TemplateChannelClient();

    /// <summary>Co kanál nabízí navíc oproti tomu, co je v MLINE.</summary>
    public record Available(IReadOnlyList<ChannelTemplateInfo> New, IReadOnlyList<ChannelTemplateInfo> Updated)
    {
        public int Count => New.Count + Updated.Count;
        public bool Any => Count > 0;
    }

    /// <summary>
    /// Zjistí, co je k dispozici. Nic nestahuje ani nemění - jen porovná seznam z úložiště s tím,
    /// co v MLINE je. Bez sítě nebo bez klíče vrátí prázdno (přehled šablon pak funguje jako dřív).
    /// </summary>
    public async Task<Available> CheckAsync(CancellationToken ct = default)
    {
        var nabidka = await _channel.ListAsync(ct);
        if (nabidka.Count == 0) return new Available([], []);

        Dictionary<string, DateTime> mame;
        try { mame = await StazeneAsync(); }
        catch (CacheTemplateException) { return new Available([], []); }

        var nove = new List<ChannelTemplateInfo>();
        var aktualizovane = new List<ChannelTemplateInfo>();
        foreach (var polozka in nabidka)
        {
            if (!mame.TryGetValue(polozka.Id, out var mameZ)) nove.Add(polozka);
            // Vteřinová tolerance: čas jde přes JSON a databázi, na přesnou rovnost se spoléhat nedá.
            else if (polozka.UpdatedAt > mameZ.AddSeconds(1)) aktualizovane.Add(polozka);
        }
        return new Available(nove, aktualizovane);
    }

    /// <summary>
    /// Stáhne uvedené šablony do MLINE. Vrací počet skutečně uložených - co se nepodaří stáhnout
    /// nebo přečíst, se přeskočí a ostatní projdou (jedna vadná šablona nesmí zastavit doručení).
    /// </summary>
    public async Task<int> DownloadAsync(IEnumerable<ChannelTemplateInfo> polozky, CancellationToken ct = default)
    {
        int ulozeno = 0;
        foreach (var polozka in polozky)
        {
            try
            {
                var json = await _channel.DownloadAsync(polozka.Id, ct);
                if (string.IsNullOrWhiteSpace(json)) continue;

                // Deserialize spustí TemplateMigrator → šablona je uložená vždy v aktuální verzi.
                var template = TemplateSerializer.Deserialize(json);
                template.ChannelOrigin = new TemplateChannelOrigin(polozka.Id, polozka.UpdatedAt);

                // distribution: true → jediná cesta, jak server povolí zápis do read-only MLINE větve.
                await storage.SaveAsync(template, CacheTemplateService.MlineIdFa, distribution: true);
                ulozeno++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemTemplateChannelSync] '{polozka.Name}' selhalo: {ex.Message}");
            }
        }
        return ulozeno;
    }

    /// <summary>Identifikátor v úložišti → čas verze, kterou už v MLINE máme.</summary>
    private async Task<Dictionary<string, DateTime>> StazeneAsync()
    {
        var result = new Dictionary<string, DateTime>();
        foreach (var (_, origin) in await ZKanaluAsync(storage))
            result[origin.Id] = origin.UpdatedAt;
        return result;
    }

    /// <summary>Šablony v MLINE, které doručil kanál - <see cref="SystemTemplateSeeder"/> je z .exe
    /// nepřepisuje.</summary>
    public static async Task<HashSet<Guid>> ChannelBackedIdsAsync(CacheTemplateService storage) =>
        [.. (await ZKanaluAsync(storage)).Select(x => x.TemplateId)];

    private static async Task<List<(Guid TemplateId, TemplateChannelOrigin Origin)>> ZKanaluAsync(
        CacheTemplateService storage)
    {
        var result = new List<(Guid, TemplateChannelOrigin)>();
        foreach (var meta in await storage.ListAsync())
        {
            try
            {
                var (_, template) = await storage.LoadAsync(meta.TemplateId, CacheTemplateService.MlineIdFa);
                if (template.ChannelOrigin is { } origin) result.Add((template.Id, origin));
            }
            catch (Exception) { /* nečitelná šablona = tvař se, že ji nemáme */ }
        }
        return result;
    }
}
