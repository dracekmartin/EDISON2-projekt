using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

/// <summary>
/// Jeden druh prvku tak, jak ho potřebuje paleta, nabídka vložení, továrna, jména pro uživatele,
/// agendy prvků, asistent a nápověda F1.
/// </summary>
/// <param name="Type">Typ v JSON šablony (<see cref="ElementTypes"/>) - nemění se bez migrace.</param>
/// <param name="Name">Jméno pro uživatele: paleta, nabídka, titulky oken, hlášky.</param>
/// <param name="Hint">Co prvek dělá - bublina palety a nápověda F1 za jménem.</param>
/// <param name="IconKey">Klíč ikony v <c>EDIcons.xaml</c>.</param>
/// <param name="Width">Výchozí šířka v mm.</param>
/// <param name="Height">Výchozí výška v mm; u prvku s výškou podle obsahu jen náhled při vkládání.</param>
/// <param name="AgendaKinds">Druhy agend, které prvek nabízejí (<see cref="KontraktDat.Zastavkove"/>,
/// <see cref="KontraktDat.Linkove"/>); prázdné = obecný prvek pro každou agendu.</param>
/// <param name="MenuPosition">Kam ho položí nabídka „Přidat prvek" (z palety jde tam, kam ho uživatel pustí).</param>
/// <param name="NewInstance">Nový prvek s výchozí šířkou a výškou (pozici a rámeček doplní katalog).</param>
public sealed record ElementCatalogEntry(
    string Type,
    Type ClrType,
    string Name,
    string Hint,
    string IconKey,
    float Width,
    float Height,
    string[] AgendaKinds,
    (float X, float Y) MenuPosition,
    Func<float, float, TemplateElement> NewInstance)
{
    public bool IsGeneric => AgendaKinds.Length == 0;
}

/// <summary>
/// Katalog prvků - jediný popis každého druhu prvku (revize 30. 10., `revize-sjednoceni.md` A1).
/// Paleta, nabídka vložení, továrna, <see cref="ElementNames"/>, <see cref="AgendaCapabilities"/>,
/// seznam pro asistenta a nápověda F1 se odvozují odtud; do 30. 10. se nový prvek zapisoval na 12
/// až 20 míst a dvě z nich se už rozešla (jméno v paletě, agendy Poznámek u asistenta).
/// </summary>
/// <remarks>
/// Ručně zůstává: třída prvku, <c>[JsonDerivedType]</c> na <see cref="TemplateElement"/> (atribut
/// potřebuje konstantu), formulář <c>{Typ}Edit.cls</c>, ikona a výška podle dat v
/// <c>SkiaHeightCalculator</c>. S katalogem je drží <c>ElementCatalogTests</c>.
/// Pořadí je pořadí v paletě a nabídce; hlavní tabulka sestavy jde před Poznámkami, které jsou doplněk.
/// </remarks>
public static class ElementCatalog
{
    private static readonly string[] Obecny = [];
    private static readonly string[] Zastavkovy = [KontraktDat.Zastavkove];
    private static readonly string[] Linkovy = [KontraktDat.Linkove];

    public static IReadOnlyList<ElementCatalogEntry> All { get; } =
    [
        new(ElementTypes.Text, typeof(TextElement), "Text", "statický nebo s poli {NAZEV}",
            "EDIcons.TextElement", 80f, 8f, Obecny, (5f, 5f),
            (w, h) => new TextElement { Width = w, Height = h }),
        new(ElementTypes.Rectangle, typeof(RectangleElement), "Obdélník", "plocha nebo rámeček",
            "EDIcons.RectElement", 60f, 20f, Obecny, (5f, 5f),
            (w, h) => new RectangleElement { Width = w, Height = h }),
        new(ElementTypes.Line, typeof(LineElement), "Čára", "plná, čárkovaná nebo tečkovaná",
            "EDIcons.LineElement", 190f, 1f, Obecny, (0f, 10f),
            (w, h) => new LineElement { Width = w, Height = h }),
        new(ElementTypes.Image, typeof(ImageElement), "Obrázek", "ze souboru, velký se sám zmenší",
            "EDIcons.ImageElement", 40f, 30f, Obecny, (5f, 5f),
            (w, h) => new ImageElement { Width = w, Height = h }),
        new(ElementTypes.Barcode, typeof(BarcodeElement), "Kód (QR, čárový)",
            "QR, Code128, EAN-13/8, PDF417, DataMatrix; obsahem může být pole {POLE}",
            "EDIcons.QrCode", 30f, 30f, Obecny, (5f, 5f),
            (w, h) => new BarcodeElement { Width = w, Height = h }),
        // Jméno tabulek říká, odkud se berou řádky - v tom se liší, data mohou mít obě (rozhodnutí Jan 30. 10.).
        new(ElementTypes.DynamicTable, typeof(DynamicTableElement), "Tabulka s řádky z dat",
            "řádků je tolik, kolik je záznamů v datech",
            "EDIcons.TableElement", 190f, 30f, Obecny, (0f, 0f),
            (w, h) => new DynamicTableElement { Width = w, Height = h }),
        // Pevná výška, jinak by tabulka platila za prvek s výškou podle obsahu a nešlo by za ni táhnout
        // do výšky (12. 8.); výška roste s počtem řádků (3 po 6 mm).
        new(ElementTypes.Table, typeof(TableElement), "Pevná tabulka", "řádky i obsah zadáváte sami",
            "EDIcons.Grid", 90f, 18f, Obecny, (0f, 0f),
            (w, _) => { var t = TableElement.CreateGrid(3, 3); t.Width = w; t.Height = 3 * t.RowHeight; return t; }),
        new(ElementTypes.StopList, typeof(StopListElement), "Seznam zastávek", "sloupce vč. zóny, pořadí a označníku",
            "EDIcons.StopList", 60f, 60f, Zastavkovy, (0f, 0f),
            (w, h) => new StopListElement { Width = w, Height = h }),
        new(ElementTypes.DepartureGrid, typeof(DepartureGridElement), "Minuty odjezdů", "po skupinách dnů",
            "EDIcons.DepartureGrid", 120f, 60f, Zastavkovy, (0f, 0f),
            (w, h) => new DepartureGridElement { Width = w, Height = h }),
        // Výška podle obsahu: pevná výška by obsah zmenšila do fitu (písmo na 46 %), ne oříznula,
        // a uživatel na to přišel až z papíru (nález Jan 7. 9.). Výšku 100 / 25 mm prvek drží jen
        // pro rámeček výběru v návrháři.
        new(ElementTypes.LineTable, typeof(LineTableElement), "Tabulka spojů", "zastávky × spoje",
            "EDIcons.LineTable", 190f, 100f, Linkovy, (0f, 0f),
            (w, _) => new LineTableElement { Width = w }),
        // Poznámky má i linkový JŘ (od 3. 9., `B117`): do té doby si legendu skládal tabulkou nad
        // kolekcí POZNAMKY bez inverzních značek a řazení podle typu. Kontrakt poznámek je pro oba
        // druhy stejný; filtry kategorií (bezbariér, na znamení…) zůstávají u linkových nečinné.
        new(ElementTypes.NotesLegend, typeof(NotesLegendElement), "Poznámky",
            "značky, omezení a vysvětlivky pod jízdním řádem",
            "EDIcons.NotesLegend", 190f, 25f, [KontraktDat.Zastavkove, KontraktDat.Linkove], (0f, 0f),
            (w, _) => new NotesLegendElement { Width = w }),
    ];

    public static ElementCatalogEntry? Find(string type) =>
        All.FirstOrDefault(e => e.Type == type);

    public static ElementCatalogEntry? Find(TemplateElement element) =>
        All.FirstOrDefault(e => e.ClrType == element.GetType());

    /// <summary>
    /// Nový prvek na dané pozici s výchozí velikostí a rámečkem (model má výchozí stav bez rámečku
    /// kvůli starým šablonám). Neznámý typ dá text, jako dřív továrna.
    /// </summary>
    public static TemplateElement Create(string type, float x, float y)
    {
        var entry = Find(type) ?? Find(ElementTypes.Text)!;
        var el = entry.NewInstance(entry.Width, entry.Height);
        el.X = x;
        el.Y = y;
        el.BorderColor = "#FF000000";
        el.BorderWidth = 0.1f;
        return el;
    }

    /// <summary>Formulář prvku v Caché - konvence <c>{Typ}Edit</c> (panel vlastností i okno prvku).</summary>
    public static string FormClass(string type) => $"ALVA.TemplateDesigner.EDInputForm.{type}Edit";
}
