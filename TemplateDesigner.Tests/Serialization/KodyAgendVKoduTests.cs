using System.IO;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Návrhář se o agendě rozhoduje podle toho, co agenda hlásí v registru (druh prvků, vlastní sloupce,
/// varianty tras, rozdíly verzí), ne podle jejího kódu (<c>B194g</c> R4). Kód v podmínce znamená, že
/// nová agenda téhož druhu by potřebovala změnu návrháře - do 30. 10. tak fungovaly barvy rozdílů
/// a import z PDF. Test hledá v kódu návrháře text, který je kódem agendy nebo jeho začátkem
/// (<c>"LINKOVY_JR"</c>); kódy bere z tříd agend, takže nová agenda se hlídá sama.
/// </summary>
public class KodyAgendVKoduTests
{
    /// <summary>Vědomé výjimky: soubor → kódy, které v něm smí být, a proč.</summary>
    private static readonly Dictionary<string, (string[] Kody, string Duvod)> Vyjimky = new()
    {
        ["Serialization/MigrationKnizniToLinkovy.cs"] = (["LINKOVY_JR_AD", "LINKOVY_JR_MHD"],
            "převod starých kódů v uložených šablonách"),
        ["Serialization/TemplateSerializer.cs"] = (["ZASTAVKOVY_JR_MHD"],
            "převod starého kódu „MHD\" v uložených šablonách"),
        ["Windows/MainWindow/TemplateDesignerViewModel.SampleTemplate.cs"] = (["LINKOVY_JR_AD"],
            "výchozí agenda nové šablony - volba toho, na čem se pracuje (rozhodnutí Jan), musí fungovat i bez registru"),
    };

    private static readonly Regex Text = new(@"(?<!"")""([A-Z][A-Z0-9_]*)""(?!"")", RegexOptions.Compiled);
    private static readonly Regex KodAgendy =
        new(@"^Parameter (?:AgendaVariantCode|Agenda)\b[^=]*=\s*""([A-Z0-9_]+)""", RegexOptions.Compiled | RegexOptions.Multiline);

    [Fact]
    public void KodNavrhareNeporovnavaKodyAgend()
    {
        var kody = KodyAgend();
        var nalezy = Vyskyty(kody)
            .Where(v => !(Vyjimky.TryGetValue(v.Soubor, out var vyj) && vyj.Kody.Contains(v.Kod)))
            .Select(v => $"{v.Soubor}:{v.Radek}  \"{v.Kod}\"")
            .ToList();

        Assert.True(nalezy.Count == 0,
            "Kód návrháře obsahuje kód agendy. Rozhoduj podle údaje z registru agend (AgendaRegistryEntry), " +
            "chybí-li, přidej ho jako parametr třídy agendy v BaseAgenda.cls:\n" + string.Join("\n", nalezy));
    }

    /// <summary>Výjimka, která už nic nepokrývá, by do kódu pustila kód agendy bez povšimnutí.</summary>
    [Fact]
    public void VyjimkyPlati()
    {
        var vyskyty = Vyskyty(KodyAgend()).Select(v => (v.Soubor, v.Kod)).ToHashSet();
        var navic = Vyjimky
            .SelectMany(v => v.Value.Kody.Select(k => (Soubor: v.Key, Kod: k)))
            .Where(v => !vyskyty.Contains(v))
            .Select(v => $"{v.Soubor}  \"{v.Kod}\"")
            .ToList();

        Assert.True(navic.Count == 0, "Výjimka bez výskytu - smaž ji:\n" + string.Join("\n", navic));
    }

    [Fact]
    public void KodyAgendSeNactou() =>
        Assert.Contains("LINKOVY_JR_AD_ROZDILOVY", KodyAgend());

    private static HashSet<string> KodyAgend() =>
        ZdrojeAgend.Agendy()
            .SelectMany(f => KodAgendy.Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

    private static bool JeKodNeboZacatek(string text, HashSet<string> kody) =>
        kody.Contains(text) || kody.Any(k => k.StartsWith(text + "_", StringComparison.Ordinal));

    private static IEnumerable<(string Soubor, int Radek, string Kod)> Vyskyty(HashSet<string> kody)
    {
        var dotnet = Path.Combine(ZdrojeAgend.Koren(), "TemplateDesigner", "dotnet");
        foreach (var f in Directory.EnumerateFiles(dotnet, "*.cs", SearchOption.AllDirectories))
        {
            var soubor = Path.GetRelativePath(dotnet, f).Replace('\\', '/');
            if (soubor.StartsWith("bin/") || soubor.StartsWith("obj/")) continue;

            var radky = File.ReadAllLines(f);
            for (int i = 0; i < radky.Length; i++)
            {
                if (radky[i].TrimStart().StartsWith("//")) continue;
                foreach (Match m in Text.Matches(radky[i]))
                    if (JeKodNeboZacatek(m.Groups[1].Value, kody))
                        yield return (soubor, i + 1, m.Groups[1].Value);
            }
        }
    }
}
