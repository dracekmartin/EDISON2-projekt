using System.Text.Json.Nodes;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Předpřipravené vzory vlastních sloupců vývěsky = čtyři dnešní pevná rozvržení.
/// <para>
/// Obsah vzorů (dny, vyloučené značky, potlačení značek) staví **server** z konfigurace linky
/// a posílá ho v poli PRESETY_SLOUPCU - klient ho jen přečte. Dřív si ho skládaly obě strany
/// zvlášť a paritní Caché test tak hlídal kopii pravidel, ne to, co klient reálně pošle.
/// Bez načtených dat proto vzory nejsou k dispozici (nabídne se jen prázdná definice).
/// </para>
/// </summary>
public static class DepartureColumnPresets
{
    public sealed record Preset(string Key, string Label);

    /// <summary>Nabídka vzorů - klíče stabilní (visí na nich volba v editoru), popisky 1:1
    /// s volbami parametru „Rozdělení sloupců". Obsah vzoru drží server.</summary>
    public static IReadOnlyList<Preset> All { get; } =
    [
        new("PRAC_SO_NE",       "Pracovní / Sobota / Neděle+svátky"),
        new("PRAC_VIKEND",      "Pracovní / víkend (So+Ne+svátky)"),
        new("PRAZDNINY_VIKEND", "Prázdniny: Prac. mimo+o prázd. / víkend"),
        new("PRAZDNINY_SO_NE",  "Prázdniny: Prac. mimo+o prázd. / Sobota / Neděle+svátky"),
    ];

    /// <summary>Vzory z pole PRESETY_SLOUPCU (klíč vzoru → sloupce). Prázdný/neplatný vstup =
    /// prázdná mapa; volající pak vzory nenabízí.</summary>
    public static IReadOnlyDictionary<string, List<DepartureColumnDefinition>> Parse(string? json)
    {
        var vysledek = new Dictionary<string, List<DepartureColumnDefinition>>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json)) return vysledek;

        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (System.Text.Json.JsonException) { return vysledek; }
        if (root is not JsonObject obj) return vysledek;

        foreach (var (klic, hodnota) in obj)
        {
            if (hodnota is not JsonArray pole) continue;
            var sloupce = new List<DepartureColumnDefinition>();
            foreach (var polozka in pole)
            {
                if (polozka is not JsonObject s) continue;
                sloupce.Add(new DepartureColumnDefinition
                {
                    Id = Text(s, "id"),
                    Header = Text(s, "nadpis"),
                    Header2 = Text(s, "nadpis2"),
                    Days = Text(s, "dny"),
                    IncludeMarks = Text(s, "znackyJen"),
                    ExcludeMarks = Text(s, "znackyBez"),
                    SuppressedMarks = Text(s, "nevypisovatZnacky"),
                    Period = Text(s, "obdobi"),
                });
            }
            vysledek[klic] = sloupce;
        }
        return vysledek;
    }

    private static string Text(JsonObject o, string klic) => o[klic]?.GetValue<string>() ?? "";
}
