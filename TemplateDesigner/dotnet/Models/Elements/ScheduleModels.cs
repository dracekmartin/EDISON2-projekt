using System.Text.Json.Serialization;

namespace TemplateDesigner.Models.Elements;

// ─── Enums ──────────────────────────────────────────────────────────────────

public enum TypPoznamky { Text, NegativniZnacka, SpecialniZnak }
public enum TypAnotace  { Text, NegativniZnacka, SpecialniZnak }

/// <summary>Směr plnění víc-sloupcové legendy poznámek.</summary>
public enum LegendWrap { PoSloupcich, PoRadcich }

/// <summary>Řazení poznámek v legendě.</summary>
public enum LegendRazeni { Vychozi, DleTypu, DleDelkyVzestupne, DleDelkySestupne }

/// <summary>Jak blok legendy zachází s výlukovými poznámkami linky (#13093) - klient si pokládá dva
/// bloky nad sebe, jeden „Bez výluk" a druhý „Jen výluky".</summary>
public enum VylukyFiltr { Vcetne, Bez, Jen }

/// <summary>
/// Režim časové osy odjezdů v DepartureGrid. Paralelní s legacy „Časová osa odjezdů" comboboxem.
/// </summary>
public enum TimeAxisMode
{
    /// <summary>Souvislá bez mezer: jen hodiny s daty (default, kompaktní).</summary>
    Compact,
    /// <summary>Souvislá s mezerami: od první hodiny s daty do poslední, včetně prázdných hodin v rozsahu.</summary>
    DataBounded,
    /// <summary>Daná intervalem: HODINY_OD..HODINY_DO (uživatel zadá). Podporuje wrap přes půlnoc (Do &lt; Od).</summary>
    Range,
    /// <summary>Plný den (všech 24 hodin 0–23).</summary>
    Full
}



// ─── Runtime POCO modely (neserializují se do JSON šablony) ─────────────────

/// <summary>Zastávka z kolekce ZASTAVKY.</summary>
public class ZastavkaData
{
    public string Id { get; set; } = "";
    public string Popis { get; set; } = "";
    public int CasJizdy { get; set; }
    public int Poradi { get; set; }
    public bool JeAktualni { get; set; }
    public int Odsazeni { get; set; }
    public List<string> Zony { get; set; } = [];
    public bool NaZnameni { get; set; }
    public string Nastupiste { get; set; } = "";   // nástupiště/označník zastávky (AD: J-věta #3; MHD: Oznacnik)
    public string NastupisteSpoju { get; set; } = ""; // nástupiště dle spojů (CSV dle četnosti, první = nejčetnější); "" = bez per-spoj dat
    public string Sloupek { get; set; } = "";      // sloupek z číselníku zastávky (AD: J-věta #12); "" = nevyplněný nebo MHD
    public string SloupekSpoju { get; set; } = ""; // sloupky dle spojů (CSV dle četnosti, první = nejčetnější)
    public string SloupekPopis { get; set; } = ""; // popis sloupku z číselníku („směr Brno přes UH")
    public List<AtributData> Atributy { get; set; } = [];
    public List<PoznamkaZastavkyData> PoznamkyZastavky { get; set; } = [];
    /// <summary>Časy jízdy per varianta trasy (dynamické „TrasaMin"): číslo, „&lt;" (projíždí) nebo "" (mimo trasu).
    /// Prázdné = jednovariantní linka (použije se skalární <see cref="CasJizdy"/>).</summary>
    public List<string> TrasaMin { get; set; } = [];

    /// <summary>Nástupiště per varianta trasy (zarovnané s <see cref="TrasaMin"/>): číslo stanoviště,
    /// odkud daná varianta z této zastávky odjíždí, nebo "" (varianta zde nestaví). Zdroj pro režim
    /// „Řádek pro každý označník" (KOVED #13048 bod 4).</summary>
    public List<string> NastupisteVariant { get; set; } = [];

    /// <summary>Sloupek každé varianty trasy (zarovnané s <c>TrasaMin</c>) - obdoba
    /// <see cref="NastupisteVariant"/> pro sloupek.</summary>
    public List<string> SloupekVariant { get; set; } = [];

    /// <summary>Zdrojový řádek dat této zastávky - pro vyhodnocení řádkových podmínek (zvýraznění)
    /// nad poli řádku. Neserializuje se.</summary>
    [JsonIgnore] public DataContext? SourceRow { get; set; }
}

/// <summary>Atribut zastávky (ikona/symbol).</summary>
public class AtributData
{
    public string Znak { get; set; } = "";
    public string Popis { get; set; } = "";
}

/// <summary>Poznámka vázaná přímo k zastávce.</summary>
public class PoznamkaZastavkyData
{
    public string Znak { get; set; } = "";
    public TypPoznamky Typ { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>Skupina dnů (typ provozu - pracovní dny, soboty, neděle...).</summary>
public class SkupinaDnuData
{
    public string Id { get; set; } = "";
    public string Nadpis { get; set; } = "";
    public string Nadpis2 { get; set; } = "";
    public int Poradi { get; set; }
}

/// <summary>Jeden odjezd z kolekce ODJEZDY (flat záznam).</summary>
public class OdjezdData
{
    public string ZastavkaId { get; set; } = "";
    public string SkupinaDnuId { get; set; } = "";
    public int Hodina { get; set; }
    public int Minuta { get; set; }
    public string? ForeColor { get; set; }
    public string? BackColor { get; set; }
    public List<AnotaceData> Anotace { get; set; } = [];
}

/// <summary>Anotace u minuty odjezdu (horní index).</summary>
public class AnotaceData
{
    public TypAnotace Typ { get; set; }
    public string Znak { get; set; } = "";

    /// <summary>
    /// Volitelná barva textu znaku anotace (hex „#FFRRGGBB", null = default render).
    /// Pochází z Caché <c>PoznamkaSpoj.TiskJRMHDVozoveBarvaZnakuRGB</c> přes <c>AddAnotace</c>.
    /// Konzistentní s odpovídající barvou poznámky v legendě.
    /// </summary>
    public string? ForeColor { get; set; }

    /// <summary>
    /// Volitelná barva pozadí (hex „#FFRRGGBB", null = default render).
    /// Wire-format zatím rendering ignoruje (rezervováno).
    /// </summary>
    public string? BackColor { get; set; }

    /// <summary>Kategorie z Caché („odjezd-atribut", „bezbarier", „rucni-komentar"…) - stejný tag,
    /// jaký nese odpovídající řádek legendy. Řídí, jestli se značka **přeloží na glyf**: značky MHD
    /// jsou v datech už jako pozice ve fontu, takže se přeložit nesmí. Prázdná u agend bez kategorií.</summary>
    public string Kategorie { get; set; } = "";
}

/// <summary>Poznámka v legendě pod mřížkou.</summary>
public class PoznamkaData
{
    public TypPoznamky Typ { get; set; }
    public string Znak { get; set; } = "";
    public string Text { get; set; } = "";

    /// <summary>Kategorie z Caché („vyluka“, „poznamka-linky-tarifni“, „atribut-zastavky“…) - filtr-tag pro
    /// prvek legendy. Prázdná u agend, které kategorie neposílají (MHD).</summary>
    public string Kategorie { get; set; } = "";

    /// <summary>
    /// Volitelná barva textu znaku (hex „#FFRRGGBB", null = default render).
    /// Pochází z Caché <c>PoznamkaSpoj.TiskJRMHDVozoveBarvaZnakuRGB</c> přes <c>BuildPoznamka</c>.
    /// </summary>
    public string? ForeColor { get; set; }

    /// <summary>
    /// Volitelná barva pozadí (hex „#FFRRGGBB", null = default render).
    /// Wire-format zatím rendering ignoruje (rezervováno, klient zatím chce jen barvu znaku).
    /// </summary>
    public string? BackColor { get; set; }
}

// ─── Generický per-column styling ────────────────────────────────────────────
// InlineTextStyle a CellStyle nahrazeny univerzální třídou TextStyle (viz Models/TextStyle.cs).

/// <summary>
/// Kompletní vizuální styl sloupce = záhlaví + datové buňky.
/// </summary>
public class ColumnVisualStyle
{
    public TextStyle? Header { get; set; }
    public TextStyle? Data { get; set; }

    public ColumnVisualStyle Clone() => new()
    {
        Header = Header?.Clone(),
        Data = Data?.Clone()
    };

    /// <summary>Naškáluje rozměry per-sloupcových stylů (font/padding) při škálování šablony.</summary>
    public void ApplyScale(float scale)
    {
        Header?.ApplyScale(scale);
        Data?.ApplyScale(scale);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty => (Header is null || Header.IsEmpty) && (Data is null || Data.IsEmpty);
}

// ─── Per-element column models ───────────────────────────────────────────────

/// <summary>Konfigurovatelný sloupec tabulky zastávek (StopListElement).</summary>
public class StopColumn
{
    public string Key { get; set; } = "";
    public string Header { get; set; } = "";
    /// <summary>Pevná šířka v mm. Null = auto (rovnoměrně sdílí zbývající šířku elementu po pevných sloupcích).
    /// Default null (ne 10f) - jinak by se auto (null) po uložení ztratilo (serializer WhenWritingNull
    /// vynechá klíč → po reloadu by initializer vrátil 10f). Výchozí sloupce nastavují Width explicitně.</summary>
    public float? Width { get; set; }
    public bool Visible { get; set; } = true;
    public TextHorizontalAlignment Alignment { get; set; } = TextHorizontalAlignment.Left;

    /// <summary>Per-column vizuální styl (záhlaví + data). Null = dědit z elementu.</summary>
    public ColumnVisualStyle? Style { get; set; }

    public StopColumn Clone() => new()
    {
        Key = Key,
        Header = Header,
        Width = Width,
        Visible = Visible,
        Alignment = Alignment,
        Style = Style?.Clone()
    };
}

/// <summary>Nastavení sloupce skupiny dnů v DepartureGridElement (serializuje se do šablony).</summary>
public class DepartureGroupColumnStyle
{
    public string SkupinaDnuId { get; set; } = "";
    public bool IsVisible { get; set; } = true;
    public ColumnVisualStyle Style { get; set; } = new();

    /// <summary>Per-group zebra barva (alternating rows). Null = použít globální AlternatingRowColor.</summary>
    public string? AlternatingRowColor { get; set; }

    /// <summary>Override nadpisu skupiny dnů v šabloně. Prázdné/null = nadpis z dat
    /// (<see cref="SkupinaDnuData.Nadpis"/>); neprázdné → přebije ho. Řeší
    /// <see cref="Rendering.ColumnHeader.Resolve"/> jednotně s ostatními tabulkovými komponentami.</summary>
    public string? HeaderOverride { get; set; }

    /// <summary>Pevná šířka skupiny v mm (override). Null = auto (z obsahu minut + proporční dofill).
    /// Pinnutá skupina se nezahrnuje do proporčního přerozdělení zbývající šířky.</summary>
    public float? WidthOverride { get; set; }

    public DepartureGroupColumnStyle Clone() => new()
    {
        SkupinaDnuId = SkupinaDnuId,
        IsVisible = IsVisible,
        Style = Style.Clone(),
        AlternatingRowColor = AlternatingRowColor,
        HeaderOverride = HeaderOverride,
        WidthOverride = WidthOverride
    };
}

/// <summary>
/// Definice vlastního sloupce vývěsky - KDO do sloupce patří (na rozdíl od
/// <see cref="DepartureGroupColumnStyle"/>, který říká, jak sloupec vypadá). Kaskáda podmínek:
/// dny → značky (jen s / bez) → (období, etapa 2); nevyplněná podmínka neomezuje.
/// Serializuje se do šablony; na server jde jako JSON parametr DEFINICE_SLOUPCU
/// (<see cref="DepartureGridElement.BuildColumnsDefinitionWireJson"/>).
/// </summary>
public class DepartureColumnDefinition
{
    /// <summary>Stabilní id sloupce - vrací se z dat jako SKUPINA_DNU_ID, takže na něm visí
    /// i per-sloupcové styly (<see cref="DepartureGroupColumnStyle.SkupinaDnuId"/>).
    /// Při přeskládání sloupců se nemění, jinak by styly přeskočily jinam.</summary>
    public string Id { get; set; } = "";

    /// <summary>Nadpis sloupce (první řádek záhlaví).</summary>
    public string Header { get; set; } = "";

    /// <summary>Druhý řádek nadpisu. Prázdné = jednořádkové záhlaví.</summary>
    public string Header2 { get; set; } = "";

    /// <summary>Dny v týdnu jako znaky 1-7 a "+" (svátek). Prázdné = dny neomezují.
    /// Platnost spoje "X" server počítá jako pracovní dny 1-5.</summary>
    public string Days { get; set; } = "";

    /// <summary>Jen spoje s těmito značkami (CSV čísel). Prázdné = neomezuje.</summary>
    public string IncludeMarks { get; set; } = "";

    /// <summary>Bez spojů s těmito značkami (CSV čísel) - spoj se značkou se do sloupce
    /// nezařadí, i když by dny seděl. Prázdné = neomezuje.</summary>
    public string ExcludeMarks { get; set; } = "";

    /// <summary>Značky, které se v tomto sloupci netisknou u minut (CSV čísel); spoj zůstává.
    /// Per-sloupcová náhrada linkového seznamu „nevypisovat značku ve sloupci".</summary>
    public string SuppressedMarks { get; set; } = "";

    public DepartureColumnDefinition Clone() => new()
    {
        Id = Id,
        Header = Header,
        Header2 = Header2,
        Days = Days,
        IncludeMarks = IncludeMarks,
        ExcludeMarks = ExcludeMarks,
        SuppressedMarks = SuppressedMarks
    };
}

/// <summary>
/// Resolves __INDEX_0, __INDEX_1, ... positional keys to actual SkupinaDnuId.
/// Import uses __INDEX_ keys because runtime IDs aren't known at import time.
/// This method maps them by position to the actual group IDs.
/// </summary>
public static class GroupColumnIndexResolver
{
    /// <summary>Prefix pro poziční klíče přiřazené při importu.</summary>
    public const string IndexKeyPrefix = "__INDEX_";

    /// <summary>
    /// Builds a style lookup dictionary, resolving __INDEX_ keys to actual IDs.
    /// </summary>
    public static Dictionary<string, DepartureGroupColumnStyle> BuildStyleLookup(
        IReadOnlyList<DepartureGroupColumnStyle> groupColumns,
        IReadOnlyList<string> actualGroupIds)
        => BuildLookup(groupColumns, actualGroupIds, gc => gc);

    /// <summary>
    /// Builds a font-size-only lookup from group column styles, resolving __INDEX_ keys.
    /// </summary>
    public static Dictionary<string, float?> BuildHeaderFontLookup(
        IReadOnlyList<DepartureGroupColumnStyle> groupColumns,
        IReadOnlyList<string> actualGroupIds)
        => BuildLookup(groupColumns, actualGroupIds, gc => gc.Style?.Header?.FontSize);

    private static Dictionary<string, T> BuildLookup<T>(
        IReadOnlyList<DepartureGroupColumnStyle> groupColumns,
        IReadOnlyList<string> actualGroupIds,
        Func<DepartureGroupColumnStyle, T> selector)
    {
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var gc in groupColumns)
            result[gc.SkupinaDnuId] = selector(gc);

        var indexEntries = groupColumns
            .Where(gc => gc.SkupinaDnuId.StartsWith(IndexKeyPrefix, StringComparison.Ordinal))
            .OrderBy(gc => gc.SkupinaDnuId)
            .ToList();
        for (int i = 0; i < Math.Min(indexEntries.Count, actualGroupIds.Count); i++)
        {
            if (!result.ContainsKey(actualGroupIds[i]))
                result[actualGroupIds[i]] = selector(indexEntries[i]);
        }

        return result;
    }
}
