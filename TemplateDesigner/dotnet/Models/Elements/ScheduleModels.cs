using System.Text.Json.Serialization;

namespace TemplateDesigner.Models.Elements;

// ─── Enums ──────────────────────────────────────────────────────────────────

public enum TypPoznamky { Text, NegativniZnacka, SpecialniZnak }
public enum TypAnotace  { Text, NegativniZnacka, SpecialniZnak }

/// <summary>Směr plnění víc-sloupcové legendy poznámek.</summary>
public enum LegendWrap { PoSloupcich, PoRadcich }

/// <summary>Řazení poznámek v legendě.</summary>
public enum LegendRazeni { Vychozi, DleTypu, DleDelkyVzestupne, DleDelkySestupne }

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
    public List<AtributData> Atributy { get; set; } = [];
    public List<PoznamkaZastavkyData> PoznamkyZastavky { get; set; } = [];
    /// <summary>Časy jízdy per varianta trasy (dynamické „TrasaMin"): číslo, „&lt;" (projíždí) nebo "" (mimo trasu).
    /// Prázdné = jednovariantní linka (použije se skalární <see cref="CasJizdy"/>).</summary>
    public List<string> TrasaMin { get; set; } = [];

    /// <summary>Nástupiště per varianta trasy (zarovnané s <see cref="TrasaMin"/>): číslo stanoviště,
    /// odkud daná varianta z této zastávky odjíždí, nebo "" (varianta zde nestaví). Zdroj pro režim
    /// „Řádek pro každý označník" (KOVED #13048 bod 4).</summary>
    public List<string> NastupisteVariant { get; set; } = [];

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
}

/// <summary>Poznámka v legendě pod mřížkou.</summary>
public class PoznamkaData
{
    public TypPoznamky Typ { get; set; }
    public string Znak { get; set; } = "";
    public string Text { get; set; } = "";

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
