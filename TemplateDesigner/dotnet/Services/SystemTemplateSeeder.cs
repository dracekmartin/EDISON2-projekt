using System.Diagnostics;
using System.IO;
using TemplateDesigner.Models;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Services;

/// <summary>
/// Nalije systémové (M-line) výchozí šablony z embedded JSON (složka <c>SystemTemplates/</c>
/// zabalená v .exe) do <b>MLINE</b> větve DB. Governance v1 distribuční kanál: .exe je release
/// payload, seed je „naleje do MLINE zásuvky" (budoucí „jiný kanál" jen vymění tento zdroj).
///
/// <para>Idempotentní: přepíše MLINE originál obsahem z .exe (stabilní <c>Template.Id</c> →
/// update stejného záznamu). Systémové needituje uživatel - jen tento seeder - takže overwrite
/// při každém startu je bezpečný a garantuje, že MLINE = verze z aktuálního .exe. Zákaznické
/// (firma) větve se nikdy nedotkne.</para>
///
/// <para>Volá se fire-and-forget při startu; chyby (Caché nedostupná, porušený JSON) měkce
/// logují a přeskočí - nikdy neshodí start.</para>
/// </summary>
public static class SystemTemplateSeeder
{
    public static async Task SeedAsync(CacheTemplateService storage)
    {
        // Co už doručil kanál, se z .exe NEPŘEPISUJE - jinak by se novější šablona z helpdesku
        // při každém startu tiše vrátila na verzi zabalenou v programu, a celý kanál by byl k ničemu.
        HashSet<Guid> zKanalu;
        try { zKanalu = await SystemTemplateChannelSync.ChannelBackedIdsAsync(storage); }
        catch (Exception) { zKanalu = []; }

        var asm = typeof(SystemTemplateSeeder).Assembly;
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.Contains(".SystemTemplates.") ||
                !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                using var stream = asm.GetManifestResourceStream(name);
                if (stream is null) continue;
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();

                // Deserialize spustí TemplateMigrator → šablona je při seedu vždy v aktuální verzi.
                var template = TemplateSerializer.Deserialize(json);
                if (zKanalu.Contains(template.Id))
                {
                    Debug.WriteLine($"[SystemTemplateSeeder] '{template.Name}' už doručil kanál - přeskakuji.");
                    continue;
                }

                if (!await MaSeZapsatAsync(storage, template))
                {
                    Debug.WriteLine($"[SystemTemplateSeeder] '{template.Name}' je beze změny - přeskakuji.");
                    continue;
                }

                // distribution: true → jediná cesta, jak server povolí zápis do read-only MLINE větve.
                await storage.SaveAsync(template, CacheTemplateService.MlineIdFa, distribution: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemTemplateSeeder] Seed '{name}' selhal: {ex.Message}");
            }
        }
    }

    /// <summary>Je v MLINE něco jiného, než co nese .exe? Šablonu, která tam ještě není, zapiš.</summary>
    private static async Task<bool> MaSeZapsatAsync(CacheTemplateService storage, Template zExe)
    {
        try
        {
            var (_, vDatabazi) = await storage.LoadAsync(zExe.Id, CacheTemplateService.MlineIdFa);
            return SeLisiObsah(zExe, vDatabazi);
        }
        catch (Exception)
        {
            return true;   // není tam / nejde přečíst → zapsat
        }
    }

    /// <summary>
    /// Liší se šablony <b>obsahem</b>? Časy vzniku a změny se do porovnání nepočítají - jinak by
    /// vycházely jako změna vždycky a seed by při každém startu přepsal všechny systémové šablony.
    /// Uživateli se to projevilo tak, že „změněno" v přehledu ukazovalo čas posledního spuštění
    /// programu, ne skutečnou změnu obsahu (nález Jan 19. 8.).
    /// </summary>
    internal static bool SeLisiObsah(Template a, Template b)
    {
        // Porovnává se serializovaný tvar: chytí i vnořené změny (prvky, sloupce, parametry),
        // které by výčet vlastností musel hlídat ručně a při další vlastnosti by zestaral.
        // Čas vzniku, čas změny a verze formátu se srovnají na stejno - to jsou razítka uložení,
        // ne obsah (a čas změny navíc přepisuje každé uložení, takže by rozdíl vycházel vždy).
        var x = TemplateSerializer.DeepClone(a);
        var y = TemplateSerializer.DeepClone(b);
        x.CreatedAt = y.CreatedAt = default;
        x.ModifiedAt = y.ModifiedAt = default;
        x.Version = y.Version = "";
        return TemplateSerializer.SerializeForCompare(x) != TemplateSerializer.SerializeForCompare(y);
    }
}
