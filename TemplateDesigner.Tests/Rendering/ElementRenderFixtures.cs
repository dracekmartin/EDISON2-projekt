using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Pevné vstupy pro odtisky kreslení (viz <see cref="RenderSnapshot"/>). Data jsou schválně
/// **heterogenní** - u linkové tabulky pás skupin dnů, čísla spojů, řádek platnosti, řádek
/// negativních značek i přípojné řádky - aby refaktor řádkové smyčky nemohl projít jen proto,
/// že fixtura obsahuje samé stejné řádky.
/// </summary>
internal static class ElementRenderFixtures
{
    private const string Font = "Arial";   // explicitně, ať odtisk nezávisí na výchozím fontu šablony

    private static TextStyle Style(float size, bool bold = false) =>
        new() { FontFamily = Font, FontSize = size, Bold = bold };

    // ── Zastávkový JŘ: seznam zastávek + mřížka odjezdů + legenda ─────────────

    public static DataContext StopScheduleData() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "Nádraží"), ("CAS_JIZDY", "0"),
                ("PORADI", "1"), ("AKTUALNI_ZASTAVKA", "1"), ("NASTUPISTE", "A"), ("ZONY", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "Poliklinika"), ("CAS_JIZDY", "4"),
                ("PORADI", "2"), ("NA_ZNAMENI", "1"), ("NASTUPISTE", "B"),
                ("ATRIBUTY", RenderSnapshot.Nested([[("ZNAK", "x"), ("POPIS", "na znamení")]]))),
            RenderSnapshot.Row(("ID", "3"), ("NAZEV_ZASTAVKY", "Sídliště Máj - velmi dlouhý název zastávky"),
                ("CAS_JIZDY", "11"), ("PORADI", "3")),
        ]),
        ("SKUPINY_DNU",
        [
            RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní"), ("NADPIS2", "den"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NADPIS", "Sobota"), ("PORADI", "2")),
        ]),
        ("ODJEZDY",
        [
            RenderSnapshot.Row(("ZASTAVKA_ID", "1"), ("SKUPINA_DNU_ID", "1"), ("HODINA", "5"), ("MINUTA", "12")),
            RenderSnapshot.Row(("ZASTAVKA_ID", "1"), ("SKUPINA_DNU_ID", "1"), ("HODINA", "5"), ("MINUTA", "42"),
                ("ANOTACE", RenderSnapshot.Nested([[("TYP", "nz"), ("ZNAK", "×")]]))),
            RenderSnapshot.Row(("ZASTAVKA_ID", "1"), ("SKUPINA_DNU_ID", "1"), ("HODINA", "6"), ("MINUTA", "3"),
                ("ANOTACE", RenderSnapshot.Nested([[("TYP", "specialni"), ("ZNAK", "H")]]))),
            RenderSnapshot.Row(("ZASTAVKA_ID", "1"), ("SKUPINA_DNU_ID", "2"), ("HODINA", "6"), ("MINUTA", "30")),
            RenderSnapshot.Row(("ZASTAVKA_ID", "1"), ("SKUPINA_DNU_ID", "2"), ("HODINA", "23"), ("MINUTA", "55")),
        ]),
        ("POZNAMKY",
        [
            RenderSnapshot.Row(("TYP", "nz"), ("ZNAK", "×"), ("TEXT", "jede jen 31.12.")),
            RenderSnapshot.Row(("TYP", "specialni"), ("ZNAK", "H"), ("TEXT", "bezbariérově přístupné vozidlo")),
            RenderSnapshot.Row(("TYP", "text"), ("ZNAK", ""), ("TEXT", "Přeprava kol pouze ve vyhrazeném prostoru")),
        ]));

    public static StopListElement StopList() => new()
    {
        X = 5, Y = 5, Width = 90, Height = 60,
        HeaderStyle = Style(8, bold: true),
        DataStyle = Style(7),
        BorderColor = "#FF000000", BorderWidth = 0.2f,
    };

    public static DepartureGridElement DepartureGrid() => new()
    {
        X = 100, Y = 5, Width = 100, Height = 60,
        HeaderStyle = Style(8, bold: true),
        DataStyle = Style(7),
        BorderColor = "#FF000000", BorderWidth = 0.2f,
    };

    public static NotesLegendElement NotesLegend() => new()
    {
        X = 5, Y = 70, Width = 195, Height = 30,
        HeaderStyle = Style(8, bold: true),
        DataStyle = Style(7),
        BorderColor = "#FF000000", BorderWidth = 0.2f,
    };

    // ── Linkový JŘ: heterogenní řádky (pás skupin, spoje, platnost, NZ, přípoje) ──

    public static DataContext LineTableData() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "České Budějovice, aut.nádr."), ("TC", "1"),
                ("ZONY", "1"), ("MHD", "x"), ("PORADI", "1"), ("NASTUPISTE", "12")),
            RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "Hluboká nad Vltavou"), ("TC", "3"),
                ("ZONY", "2"), ("PORADI", "2")),
            RenderSnapshot.Row(("ID", "3"), ("NAZEV_ZASTAVKY", "Týn nad Vltavou"), ("TC", "7"),
                ("ZONY", "3"), ("PORADI", "3"), ("TYP", "prijezd")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1")),
            RenderSnapshot.Row(("CISLO_SPOJE", "3"), ("SKUPINA", "1"), ("NZ", "×")),
            RenderSnapshot.Row(("CISLO_SPOJE", "5"), ("SKUPINA", "2")),
        ]),
        ("SKUPINY_DNU",
        [
            RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NADPIS", "Sobota, nedele"), ("PORADI", "2")),
        ]));

    public static LineTableElement LineTable() => new()
    {
        X = 5, Y = 5, Width = 190, Height = 90,
        HeaderStyle = Style(7, bold: true),
        DataStyle = Style(6),
        BorderColor = "#FF000000", BorderWidth = 0.2f,
    };

    // ── Dynamická tabulka (obecná, ne JŘ) ────────────────────────────────────

    public static DataContext DynamicTableData() => RenderSnapshot.Data(
        ("POLOZKY",
        [
            RenderSnapshot.Row(("NAZEV", "Jízdenka základní"), ("POCET", "2"), ("CENA", "24.50")),
            RenderSnapshot.Row(("NAZEV", "Jízdenka zlevněná - dlouhý název položky pro zalomení"),
                ("POCET", "1"), ("CENA", "12.00")),
            RenderSnapshot.Row(("NAZEV", "Zavazadlo"), ("POCET", "3"), ("CENA", "6.00")),
        ]));

    public static DynamicTableElement DynamicTable() => new()
    {
        X = 5, Y = 5, Width = 120, Height = 50,
        DataSource = "POLOZKY",
        HeaderStyle = Style(8, bold: true),
        DataStyle = Style(7),
        AlternatingRowColor = "#FFF0F0F0",
        BorderColor = "#FF000000", BorderWidth = 0.2f,
        Columns =
        [
            new TableColumn { Header = "Položka", Field = "NAZEV", Width = 0 },
            new TableColumn { Header = "Počet", Field = "POCET", Width = 20 },
            new TableColumn { Header = "Cena", Field = "CENA", Width = 25, Format = "N2" },
        ],
    };
}
