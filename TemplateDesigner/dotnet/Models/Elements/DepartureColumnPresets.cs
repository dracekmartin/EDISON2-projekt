namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Předpřipravené vzory vlastních sloupců vývěsky = čtyři dnešní pevná rozvržení. Paritu zařazení
/// spojů se serverovým „automaticky" hlídají Caché testy (TestDefiniceParitaPreset* v
/// ZastavkoveJrAdHelperyTest). Prázdninové vzory potřebují NZ konfiguraci linky (čísla značek
/// „jede/nejede o prázdninách") - bez ní se oba pracovní sloupce chovají stejně.
/// </summary>
public static class DepartureColumnPresets
{
    public sealed record Preset(string Key, string Label);

    /// <summary>Nabídka vzorů - labely 1:1 s volbami parametru „Rozdělení sloupců".</summary>
    public static IReadOnlyList<Preset> All { get; } =
    [
        new("PRAC_SO_NE",       "Pracovní / Sobota / Neděle+svátky"),
        new("PRAC_VIKEND",      "Pracovní / víkend (So+Ne+svátky)"),
        new("PRAZDNINY_VIKEND", "Prázdniny: Prac. mimo+o prázd. / víkend"),
        new("PRAZDNINY_SO_NE",  "Prázdniny: Prac. mimo+o prázd. / Sobota / Neděle+svátky"),
    ];

    /// <summary>
    /// Sestaví sloupce vzoru. nzConfig = "NZ1~NZ2|NENZ1~NENZ2" z dat linky (pole NZ_KONFIG),
    /// suppressedSlots = poziční seznam „nevypisovat značku" (pole NEVYPISOVAT_ZNACKY_SLOUPCU,
    /// slot N patří N-tému sloupci). Obojí smí být prázdné - pak vzor nese jen dny.
    /// </summary>
    public static List<DepartureColumnDefinition> Build(string key, string? nzConfig, string? suppressedSlots)
    {
        // 1. pozice = značka „jede o prázdninách", 2. = „nejede o prázdninách"; poziční seznamy
        // skrývání spojů mají legacy sloty prohozené (viz ZarazeniSpoje v ZastavkoveJrAd.cls).
        var nz = (nzConfig ?? "").Split('|');
        var nzMain = nz[0].Split('~');
        var jedeOPrazd   = nzMain.Length > 0 ? nzMain[0].Trim() : "";
        var nejedeOPrazd = nzMain.Length > 1 ? nzMain[1].Trim() : "";
        var nenz = (nz.Length > 1 ? nz[1] : "").Split('~');
        var nenzMimo   = nenz.Length > 1 ? nenz[1].Trim() : "";
        var nenzOPrazd = nenz.Length > 0 ? nenz[0].Trim() : "";

        var mimoBez   = JoinMarks(jedeOPrazd, nenzMimo);
        var oPrazdBez = JoinMarks(nejedeOPrazd, nenzOPrazd);

        List<DepartureColumnDefinition> cols = key switch
        {
            "PRAC_SO_NE" =>
            [
                Col("1", "Pracovní dny", "12345"),
                Col("2", "Soboty", "6"),
                Col("3", "Neděle a svátky", "7+"),
            ],
            "PRAC_VIKEND" =>
            [
                Col("1", "Pracovní dny", "12345"),
                Col("2", "Soboty, neděle a svátky", "67+"),
            ],
            "PRAZDNINY_VIKEND" =>
            [
                Col("1", "Pracovní dny mimo prázdniny", "12345", mimoBez),
                Col("2", "Pracovní dny o prázdninách", "12345", oPrazdBez),
                Col("3", "Soboty, neděle a svátky", "67+"),
            ],
            "PRAZDNINY_SO_NE" =>
            [
                Col("1", "Pracovní dny mimo prázdniny", "12345", mimoBez),
                Col("2", "Pracovní dny o prázdninách", "12345", oPrazdBez),
                Col("3", "Soboty", "6"),
                Col("4", "Neděle a svátky", "7+"),
            ],
            _ => []
        };

        // poziční „nevypisovat značku" se překlopí na per-sloupcový seznam (slot N → sloupec N)
        var slots = (suppressedSlots ?? "").Split('~');
        for (int i = 0; i < cols.Count && i < slots.Length; i++)
            cols[i].SuppressedMarks = slots[i].Trim();

        return cols;
    }

    private static DepartureColumnDefinition Col(string id, string header, string days, string excludeMarks = "") =>
        new() { Id = id, Header = header, Days = days, ExcludeMarks = excludeMarks };

    private static string JoinMarks(string a, string b)
    {
        var parts = (a + "," + b).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return string.Join(",", parts.Distinct());
    }
}
