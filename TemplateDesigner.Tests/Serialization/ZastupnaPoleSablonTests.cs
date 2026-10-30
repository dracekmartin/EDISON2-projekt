using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Dodávané šablony (<c>SystemTemplates</c>, <c>LaunchTemplates</c>) smějí v zástupných polích <c>{POLE}</c> používat jen
/// to, co jejich agenda nabízí v nabídce dat (<c>GetSchema</c>), pole stránky a hodnoty, které dosadí sazba. Pole, které
/// agenda přejmenuje nebo vyřadí, by se v šabloně u zákazníka tiše vytisklo prázdně.
/// Nabídku čte z <c>.cls</c> týž parser jako <see cref="AgendaSchemaDriftTests"/> (offline).
/// </summary>
public class ZastupnaPoleSablonTests
{
    private static readonly Regex ZastupnePole = new(@"\{(?<pole>[A-Z][A-Z0-9_]*)(?::[^}]*)?\}", RegexOptions.Compiled);
    private static readonly Regex KodAgendy = new(@"^Parameter AgendaVariantCode(?: As %String)? = ""(\w+)""", RegexOptions.Multiline);
    private static readonly Regex PrevzataNabidka = new(@"##class\(ALVA\.TemplateDesigner\.Agendy\.(\w+)\)\.GetSchema\(");

    /// <summary>Hodnoty, které do dat dosadí návrhář, ne agenda: pole stránky a dopravci po verzích ze sazby.</summary>
    private static readonly HashSet<string> OdNavrhare =
    [
        .. TemplateDesignerViewModel.PoleStranky.Select(p => p.Pole),
        PageLayoutEngine.DopravciObeVerzeKlic, PageLayoutEngine.DopravciPuvodniKlic, PageLayoutEngine.DopravciNovaKlic,
    ];

    private static IEnumerable<string> Sablony() =>
        new[] { "SystemTemplates", "LaunchTemplates" }
            .SelectMany(s => Directory.GetFiles(RepoCesty.Dotnet(s), "*.json"));

    /// <summary>Kód agendy → klíče její nabídky dat, včetně nabídky převzaté z jiné agendy (porovnání verzí).</summary>
    private static Dictionary<string, HashSet<string>> NabidkaAgend()
    {
        var podleSouboru = AgendaSchemaDriftTests.KliceNabidkyPodleSouboru();
        var vysledek = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var soubor in ZdrojeAgend.KodAgend())
        {
            var zdroj = File.ReadAllText(soubor);
            if (KodAgendy.Match(zdroj) is not { Success: true } kod) continue;
            var klice = new HashSet<string>(podleSouboru.GetValueOrDefault(Path.GetFileName(soubor)) ?? [], StringComparer.Ordinal);
            foreach (Match prevzata in PrevzataNabidka.Matches(zdroj))
                klice.UnionWith(podleSouboru.GetValueOrDefault(prevzata.Groups[1].Value + ".cls") ?? []);
            vysledek[kod.Groups[1].Value] = klice;
        }
        return vysledek;
    }

    [Fact]
    public void SablonyPouzivajiJenPoleSveAgendy()
    {
        var nabidka = NabidkaAgend();
        var nalezy = new List<string>();
        int zkontrolovano = 0;

        foreach (var cesta in Sablony())
        {
            var json = File.ReadAllText(cesta);
            var agenda = JsonNode.Parse(json)?["agendaCode"]?.GetValue<string>() ?? "";
            if (!nabidka.TryGetValue(agenda, out var klice))
            {
                nalezy.Add($"{Path.GetFileName(cesta)}: agenda „{agenda}\" v kódu agend není");
                continue;
            }
            zkontrolovano++;
            nalezy.AddRange(ZastupnePole.Matches(json)
                .Select(m => m.Groups["pole"].Value)
                .Where(p => !klice.Contains(p) && !OdNavrhare.Contains(p))
                .Distinct()
                .Select(p => $"{Path.GetFileName(cesta)} ({agenda}): {{{p}}}"));
        }

        Assert.True(zkontrolovano >= 5, $"zkontrolováno jen {zkontrolovano} šablon - hledání je rozbité");
        Assert.True(nalezy.Count == 0,
            "Dodávaná šablona používá pole, které její agenda nenabízí (přejmenované nebo vyřazené?):\n" + string.Join("\n", nalezy));
    }
}
