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

    // Barvy záhlaví — null = použít výchozí z elementu
    public string? HeaderTextColor { get; set; }
    public string? HeaderBackgroundColor { get; set; }

    // Záhlaví — font/styl — null = výchozí z elementu
    public TextHorizontalAlignment? HeaderAlignment { get; set; }
    public VerticalTextAlignment? HeaderVerticalAlignment { get; set; }
    public float? HeaderFontSize { get; set; }
    public bool? HeaderBold { get; set; }
    public bool? HeaderItalic { get; set; }

    // Datový sloupec — null = výchozí z elementu
    public string? ColumnBackgroundColor { get; set; }
    public float? DataFontSize { get; set; }
    public bool? DataBold { get; set; }
    public bool? DataItalic { get; set; }

    public DepartureGroupColumnStyle Clone() => new()
    {
        SkupinaDnuId = SkupinaDnuId,
        IsVisible = IsVisible,
        HeaderTextColor = HeaderTextColor,
        HeaderBackgroundColor = HeaderBackgroundColor,
        HeaderAlignment = HeaderAlignment,
        HeaderVerticalAlignment = HeaderVerticalAlignment,
        HeaderFontSize = HeaderFontSize,
        HeaderBold = HeaderBold,
        HeaderItalic = HeaderItalic,
        ColumnBackgroundColor = ColumnBackgroundColor,
        DataFontSize = DataFontSize,
        DataBold = DataBold,
        DataItalic = DataItalic
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
