using System.IO;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Každý zakázaný tvar z kanonu názvosloví (<c>ux-conventions §8</c>, přeškrtnutý <c>~~tvar~~</c>) hlídá některý
/// seznam zákazů (<see cref="EDInputFormSchemaDriftTests.BannedTerms"/>, <c>BannedLabelForms</c>, <c>BannedExactLabels</c>),
/// nebo má výjimku s důvodem. Do 30. 10. se kanon a seznamy doplňovaly ručně každý zvlášť a 31 tvarů nehlídalo nic
/// - kanon tvrdil zákaz, který nikdo nevynucoval.
/// </summary>
public class KanonNazvosloviTests
{
    /// <summary>Tvary, které kanon zakazuje jen v určitém významu - zákaz celého slova by zakázal i správné použití.</summary>
    private static readonly Dictionary<string, string> Vyjimky = new(StringComparer.Ordinal)
    {
        ["Dopředu"] = "příkazy vrstvy berou text z KeyboardShortcuts, jinak je „dopředu\" běžné slovo",
        ["Kód"] = "samotné slovo je běžné („kód agendy\"), zakázaný je jen tvar jména prvku „Kód (QR / čárový)\"",
        ["Linkový JŘ"] = "tak se jmenuje agenda; zakázané je jen jako jméno prvku (Tabulka spojů)",
        ["Nadpis"] = "„Nadpis skupiny\" v datech je jiný pojem než záhlaví tabulky a je správně",
        ["Zarovnání"] = "jako název skupiny je správně, zakázané je jen jako samotný popisek pole",
        ["označník"] = "kanonický termín pro pole z trasy linky; zakázané je jen pro číslo z číselníku sloupků",
        ["stanoviště"] = "zastřešující pojem pro zvolený označník nebo sloupek; zakázané je jen jako synonymum označníku",
    };

    private static IReadOnlyList<string> PrekrtnuteTvary()
    {
        var ux = File.ReadAllText(RepoCesty.TemplateDesigner(".claude", "rules", "ux-conventions.md"));
        int od = ux.IndexOf("## 8.", StringComparison.Ordinal), po = ux.IndexOf("## 9.", StringComparison.Ordinal);
        return [.. Regex.Matches(ux[od..po], "~~([^~]+)~~").Select(m => m.Groups[1].Value).Distinct()];
    }

    [Fact]
    public void KazdyZakazanyTvarHlidaSeznamNeboMaVyjimku()
    {
        string[] zakazy = [.. EDInputFormSchemaDriftTests.BannedTerms, .. EDInputFormSchemaDriftTests.BannedLabelForms,
                           .. EDInputFormSchemaDriftTests.BannedExactLabels];
        var tvary = PrekrtnuteTvary();
        Assert.True(tvary.Count > 40, $"v kanonu se našlo jen {tvary.Count} přeškrtnutých tvarů - hledání je rozbité");

        var nehlidane = tvary.Where(t => !Vyjimky.ContainsKey(t) && !zakazy.Any(z => t.Contains(z, StringComparison.Ordinal))).ToList();
        Assert.True(nehlidane.Count == 0,
            "Kanon (ux-conventions §8) zakazuje tvar, který žádný seznam zákazů nehlídá - doplň ho do EDInputFormSchemaDriftTests, " +
            "nebo sem výjimku s důvodem:\n" + string.Join("\n", nehlidane));
    }

    /// <summary>Výjimka, jejíž tvar už kanon nezakazuje, by jen visela.</summary>
    [Fact]
    public void VyjimkyOdpovidajiKanonu()
    {
        var tvary = PrekrtnuteTvary();
        Assert.All(Vyjimky.Keys, v => Assert.Contains(v, tvary));
    }
}
