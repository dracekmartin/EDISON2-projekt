using System.Diagnostics;
using System.IO;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Services;

/// <summary>
/// Nalije systémové (M-line) výchozí šablony z embedded JSON (složka <c>SystemTemplates/</c>
/// zabalená v .exe) do <b>MLINE</b> větve DB. Governance v1 distribuční kanál: .exe je release
/// payload, seed je „naleje do MLINE zásuvky" (budoucí „jiný kanál" jen vymění tento zdroj).
///
/// <para>Idempotentní: přepíše MLINE originál obsahem z .exe (stabilní <c>Template.Id</c> →
/// update stejného záznamu). Systémové needituje uživatel — jen tento seeder — takže overwrite
/// při každém startu je bezpečný a garantuje, že MLINE = verze z aktuálního .exe. Zákaznické
/// (firma) větve se nikdy nedotkne.</para>
///
/// <para>Volá se fire-and-forget při startu; chyby (Caché nedostupná, porušený JSON) měkce
/// logují a přeskočí — nikdy neshodí start.</para>
/// </summary>
public static class SystemTemplateSeeder
{
    public static async Task SeedAsync(CacheTemplateService storage)
    {
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
                // distribution: true → jediná cesta, jak server povolí zápis do read-only MLINE větve.
                await storage.SaveAsync(template, CacheTemplateService.MlineIdFa, distribution: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemTemplateSeeder] Seed '{name}' selhal: {ex.Message}");
            }
        }
    }
}
