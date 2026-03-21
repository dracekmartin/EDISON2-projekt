using System.Text.Json.Serialization;

namespace TemplateDesigner.Models.Elements;

// ─── Enums ──────────────────────────────────────────────────────────────────

public enum TypPoznamky { Text, NegativniZnacka, SpecialniZnak }
public enum TypAnotace  { Text, NegativniZnacka, SpecialniZnak }

public enum ScheduleDisplayMode { MinutesOnly, FullTime }

public enum TimeAxisMode { Compact, Full, Range }

public enum VerticalTextAlignment { Top, Center, Bottom }

// ─── Runtime POCO modely (neserializují se do JSON šablony) ─────────────────

/// <summary>Zastávka z kolekce ZASTAVKY.</summary>
public class ZastavkaData
{
    public string Id { get; set; } = "";
    public string Popis { get; set; } = "";
    public string TarifniCislo { get; set; } = "";
    public int CasJizdy { get; set; }
    public int Poradi { get; set; }
    public bool JeAktualni { get; set; }
    public int Odsazeni { get; set; }
    public List<string> Zony { get; set; } = [];
    public List<string> Prubehy { get; set; } = [];
    public bool ZakladniTrasa { get; set; } = true;
    public bool NaZnameni { get; set; }
    public List<AtributData> Atributy { get; set; } = [];
    public List<PoznamkaZastavkyData> PoznamkyZastavky { get; set; } = [];
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

/// <summary>Skupina dnů (typ provozu — pracovní dny, soboty, neděle...).</summary>
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
    public string ZkraceneOznaceniLinky { get; set; } = "";
    public string? ForeColor { get; set; }
    public string? BackColor { get; set; }
    public List<AnotaceData> Anotace { get; set; } = [];
}

/// <summary>Anotace u minuty odjezdu (horní index).</summary>
public class AnotaceData
{
    public TypAnotace Typ { get; set; }
    public string Znak { get; set; } = "";
}

/// <summary>Poznámka v legendě pod mřížkou.</summary>
public class PoznamkaData
{
    public TypPoznamky Typ { get; set; }
    public string Znak { get; set; } = "";
    public string Text { get; set; } = "";
}

// ─── Generický per-column styling ────────────────────────────────────────────

/// <summary>
/// Lehký textový styl pro inline segmenty (minuty, anotace).
/// Null property = dědit z element-level default.
/// </summary>
public class InlineTextStyle
{
    public string? FontFamily { get; set; }
    public float? FontSize { get; set; }
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    public string? TextColor { get; set; }

    public InlineTextStyle Clone() => (InlineTextStyle)MemberwiseClone();

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty =>
        FontFamily is null && FontSize is null && Bold is null
        && Italic is null && TextColor is null;
}

/// <summary>
/// Vizuální styl jedné zóny buňky (záhlaví NEBO data).
/// Null property = dědit z element-level default.
/// </summary>
public class CellStyle
{
    public string? FontFamily { get; set; }
    public float? FontSize { get; set; }
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    public string? TextColor { get; set; }
    public string? BackgroundColor { get; set; }
    public TextHorizontalAlignment? HAlignment { get; set; }
    public float? PaddingTop { get; set; }
    public float? PaddingRight { get; set; }
    public float? PaddingBottom { get; set; }
    public float? PaddingLeft { get; set; }

    public CellStyle Clone() => (CellStyle)MemberwiseClone();

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty =>
        FontFamily is null && FontSize is null && Bold is null
        && Italic is null && TextColor is null && BackgroundColor is null
        && HAlignment is null
        && PaddingTop is null && PaddingRight is null
        && PaddingBottom is null && PaddingLeft is null;
}

/// <summary>
/// Kompletní vizuální styl sloupce = záhlaví + datové buňky.
/// </summary>
public class ColumnVisualStyle
{
    public CellStyle? Header { get; set; }
    public CellStyle? Data { get; set; }

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
    public float Width { get; set; } = 10f;
    public bool Visible { get; set; } = true;
    public TextHorizontalAlignment Alignment { get; set; } = TextHorizontalAlignment.Left;

    public StopColumn Clone() => new()
    {
        Key = Key,
        Header = Header,
        Width = Width,
        Visible = Visible,
        Alignment = Alignment
    };
}

/// <summary>Nastavení sloupce skupiny dnů v DepartureGridElement (serializuje se do šablony).</summary>
public class DepartureGroupColumnStyle
{
    public string SkupinaDnuId { get; set; } = "";
    public bool IsVisible { get; set; } = true;
    public ColumnVisualStyle Style { get; set; } = new();

    public DepartureGroupColumnStyle Clone() => new()
    {
        SkupinaDnuId = SkupinaDnuId,
        IsVisible = IsVisible,
        Style = Style.Clone()
    };
}

/// <summary>Barevné pravidlo vyhodnocované přes ExpressionEvaluator.</summary>
public class ColorRule
{
    public string Condition { get; set; } = "";
    public string? ForeColor { get; set; }
    public string? BackColor { get; set; }

    public ColorRule Clone() => new()
    {
        Condition = Condition,
        ForeColor = ForeColor,
        BackColor = BackColor
    };
}
