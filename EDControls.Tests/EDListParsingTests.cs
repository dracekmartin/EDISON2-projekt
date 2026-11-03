using System.Globalization;
using System.Text.Json;
using System.Windows.Media;
using ALWA.SYS;
using EDControls.EDList;

namespace EDControls.Tests;

/// <summary>
/// Wire vrstva EDListu (LoadList řádky, filter vstup, dispatch formátování) -
/// kontrakt z docs/wire-formaty.md, dosud hlídaný jen z Caché strany.
/// </summary>
public class EDListParsingTests
{
    private static IReadOnlyList<ListColumn> Columns() =>
    [
        new ListColumn { Key = "active", Type = ListColumnType.Bool },
        new ListColumn { Key = "start", Type = ListColumnType.HoursMinutes },
        new ListColumn { Key = "name", Type = ListColumnType.Text },
    ];

    private static CVar Output(params string?[] rowJsons)
    {
        var output = new CVar();
        for (int i = 0; i < rowJsons.Length; i++)
            output[(i + 1).ToString()] = new CVar(rowJsons[i] ?? "");
        return output;
    }

    [Fact]
    public void ParseRows_RowWithoutId_SilentlyDropped()
    {
        // Wire kontrakt: každý řádek musí mít "id" - bez něj se řádek zahodí (žádná chyba).
        var rows = EDListParsing.ParseRows(
            Output("""{"name":"bez id"}""", """{"id":"7","name":"s id"}"""), Columns()).ToList();

        var row = Assert.Single(rows);
        Assert.Equal("7", row.Id);
    }

    [Fact]
    public void ParseRows_SkipsErrKeyAndBlankJson()
    {
        var output = Output("""{"id":"1","name":"A"}""", "   ", null);
        output["ERR"] = new CVar("0~OK");   // MLRest metadata, ne data

        var rows = EDListParsing.ParseRows(output, Columns()).ToList();

        Assert.Equal("1", Assert.Single(rows).Id);
    }

    /// <summary>Pojmenovaný výstup serveru (souhrn, stránkování) nesmí shodit starší klient.</summary>
    [Fact]
    public void ParseRows_NamedOutputKey_Skipped()
    {
        var output = Output("""{"id":"1","name":"A"}""");
        output["total"] = new CVar("142");

        var rows = EDListParsing.ParseRows(output, Columns()).ToList();

        Assert.Equal("1", Assert.Single(rows).Id);
    }

    /// <summary>Server zapíše číslo nebo bool, kdykoli volající plní pole z $order nebo výpočtu.</summary>
    [Fact]
    public void ParseRows_NumberBoolAndNullValues_ReadAsWireStrings()
    {
        var rows = EDListParsing.ParseRows(
            Output("""{"id":1,"pocet":142.5,"aktivni":true,"zruseno":false,"name":null}"""), Columns()).ToList();

        var row = Assert.Single(rows);
        Assert.Equal("1", row.Id);
        Assert.Equal("142.5", row["pocet"]);
        Assert.Equal("1", row["aktivni"]);
        Assert.Equal("0", row["zruseno"]);
        Assert.Null(row["name"]);
    }

    /// <summary>Popisek sloupce z $order (rok) zapíše %ToJSON jako číslo.</summary>
    [Fact]
    public void ParseColumns_NumberLabel_ReadAsText()
    {
        var output = new CVar();
        output["1"] = new CVar("""[{"key":"r2026","label":2026,"type":"NUMBER"}]""");

        var column = Assert.Single(EDListParsing.ParseColumns(output));

        Assert.Equal("2026", column.Label);
        Assert.Equal(ListColumnType.Number, column.Type);
    }

    /// <summary>Ikona u textu: server pošle iconKey jen u sloupce, který ji má; chybějící = bez ikony.</summary>
    [Fact]
    public void ParseColumns_IconKey()
    {
        var output = new CVar();
        output["1"] = new CVar("""[{"key":"name","type":"TEXT","iconKey":"ikona"},{"key":"agenda","type":"TEXT"}]""");

        var columns = EDListParsing.ParseColumns(output);

        Assert.Equal(["ikona", ""], columns.Select(c => c.IconKey));
    }

    [Fact]
    public void ParseRows_FormatsValuesByColumnType()
    {
        var rows = EDListParsing.ParseRows(
            Output("""{"id":"1","active":"1","start":"830","name":"Ranní"}"""), Columns()).ToList();

        var row = Assert.Single(rows);
        Assert.Equal("Ano", row["active"]);
        Assert.Equal("08:30", row["start"]);
        Assert.Equal("Ranní", row["name"]);
    }

    [Fact]
    public void ParseRows_UnknownColumn_KeptRaw()
    {
        var rows = EDListParsing.ParseRows(
            Output("""{"id":"1","mimoSloupce":"142.5"}"""), Columns()).ToList();

        Assert.Equal("142.5", rows[0]["mimoSloupce"]);
    }

    [Fact]
    public void BuildLoadInput_NullFilter_EmptyInput()
    {
        var input = EDListParsing.BuildLoadInput(null);

        Assert.Null(input["filter"]["agendaCode"].Value);
    }

    [Fact]
    public void BuildLoadInput_FilterGoesUnderFilterSubscript()
    {
        // ^||ML("IN","filter","klíč") - Caché čte $g(^||ML("IN","filter","k")).
        var input = EDListParsing.BuildLoadInput(new Dictionary<string, string>
        {
            ["agendaCode"] = "MHD",
            ["obdobi"] = "2026-09",
        });

        Assert.Equal("MHD", input["filter"]["agendaCode"].Value?.ToString());
        Assert.Equal("2026-09", input["filter"]["obdobi"].Value?.ToString());
    }

    [Fact]
    public void FormatValue_NullRawOrUnknownColumn_PassThrough()
    {
        Assert.Null(EDListParsing.FormatValue(null, new ListColumn { Type = ListColumnType.Bool }));
        Assert.Equal("142.5", EDListParsing.FormatValue("142.5", null));
    }

    [Theory]
    [InlineData("1", "Ano")]
    [InlineData("0", "Ne")]
    [InlineData("", "Ne")]
    public void FormatValue_Bool_AnoNe(string raw, string expected)
    {
        Assert.Equal(expected, EDListParsing.FormatValue(raw, new ListColumn { Type = ListColumnType.Bool }));
    }

    [Fact]
    public void FormatValue_DateAndDateTime_UseDefaultFormatsWhenUnset()
    {
        var date = EDListParsing.FormatValue("2026-09-08", new ListColumn { Type = ListColumnType.Date });
        var dateTime = EDListParsing.FormatValue("2026-09-08T14:05:00",
            new ListColumn { Type = ListColumnType.DateTime });

        Assert.Equal(new DateTime(2026, 9, 8).ToString("dd.MM.yyyy", CultureInfo.CurrentCulture), date);
        Assert.Equal(new DateTime(2026, 9, 8, 14, 5, 0).ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture),
            dateTime);
    }

    [Fact]
    public void FormatValue_NumberWithoutFormat_KeepsRawWithDot()
    {
        // Dokumentovaná past: NUMBER bez format se nijak neformátuje.
        Assert.Equal("142.5", EDListParsing.FormatValue("142.5", new ListColumn { Type = ListColumnType.Number }));
    }

    [Theory]
    [InlineData(ListColumnType.Number, ListColumnAlign.Right)]
    [InlineData(ListColumnType.HoursMinutes, ListColumnAlign.Right)]
    [InlineData(ListColumnType.Bool, ListColumnAlign.Center)]
    [InlineData(ListColumnType.Image, ListColumnAlign.Center)]
    [InlineData(ListColumnType.Color, ListColumnAlign.Center)]
    [InlineData(ListColumnType.Text, ListColumnAlign.Left)]
    [InlineData(ListColumnType.Date, ListColumnAlign.Left)]
    public void DefaultAlign_ByType(ListColumnType type, ListColumnAlign expected)
    {
        Assert.Equal(expected, EDListParsing.DefaultAlign(type));
    }

    /// <summary>Parita s <c>$$$ListColumnType*</c> v CACHE/EDList.inc (ListTest.TestWireConstantsMatchCSharp
    /// pinuje tytéž literály) - přejmenování na jedné straně build nechytí.</summary>
    [Theory]
    [InlineData("TEXT", ListColumnType.Text)]
    [InlineData("NUMBER", ListColumnType.Number)]
    [InlineData("DATE", ListColumnType.Date)]
    [InlineData("DATETIME", ListColumnType.DateTime)]
    [InlineData("BOOL", ListColumnType.Bool)]
    [InlineData("IMAGE", ListColumnType.Image)]
    [InlineData("COLOR", ListColumnType.Color)]
    [InlineData("HHMM", ListColumnType.HoursMinutes)]
    [InlineData("CODEBOOK", ListColumnType.Codebook)]
    [InlineData("number", ListColumnType.Number)]
    [InlineData("", ListColumnType.Text)]
    [InlineData(null, ListColumnType.Text)]
    [InlineData("DAET", ListColumnType.Text)]
    public void ParseType_WireLiterals(string? wire, ListColumnType expected)
    {
        Assert.Equal(expected, EDListParsing.ParseType(wire));
    }

    /// <summary>Nová hodnota enumu bez literálu by na serveru neměla jak vzniknout.</summary>
    [Fact]
    public void ColumnTypeWire_CoversEveryEnumValue()
    {
        Assert.Equal(Enum.GetValues<ListColumnType>().Order(), EDListParsing.ColumnTypeWire.Values.Order());
    }

    [Theory]
    [InlineData("LEFT", ListColumnAlign.Left)]
    [InlineData("CENTER", ListColumnAlign.Center)]
    [InlineData("RIGHT", ListColumnAlign.Right)]
    [InlineData("right", ListColumnAlign.Right)]
    [InlineData("", ListColumnAlign.Auto)]
    [InlineData("RIHGT", ListColumnAlign.Auto)]
    public void ParseAlign_WireLiterals(string wire, ListColumnAlign expected)
    {
        Assert.Equal(expected, EDListParsing.ParseAlign(wire));
    }

    /// <summary>COLOR sloupec bere hex jako formulář (nové seznamy) i legacy R;G;B (stávající LoadList).</summary>
    [Fact]
    public void ColorConverter_ParsesHexAndSemicolonTriplet_ElseTransparent()
    {
        var converter = new ListColorToBrushConverter();

        var brush = Assert.IsType<SolidColorBrush>(
            converter.Convert("255;128;0", typeof(Brush), null!, CultureInfo.InvariantCulture));
        Assert.Equal(Color.FromRgb(255, 128, 0), brush.Color);
        var hex = Assert.IsType<SolidColorBrush>(
            converter.Convert("#80FF8000", typeof(Brush), null!, CultureInfo.InvariantCulture));
        Assert.Equal(Color.FromArgb(0x80, 255, 128, 0), hex.Color);

        Assert.Same(Brushes.Transparent,
            converter.Convert("255;128", typeof(Brush), null!, CultureInfo.InvariantCulture));
        Assert.Same(Brushes.Transparent,
            converter.Convert("zelená", typeof(Brush), null!, CultureInfo.InvariantCulture));
        Assert.Same(Brushes.Transparent,
            converter.Convert(42, typeof(Brush), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void JsonBool_AcceptsBoolAndNumber_DefaultsOtherwise()
    {
        using var doc = JsonDocument.Parse("""{"t":true,"f":false,"one":1,"zero":0,"s":"true"}""");
        var el = doc.RootElement;

        Assert.True(el.Bool("t"));
        Assert.False(el.Bool("f"));
        Assert.True(el.Bool("one"));
        Assert.False(el.Bool("zero"));
        Assert.False(el.Bool("s"));                       // string není bool - default
        Assert.True(el.Bool("chybí", defaultValue: true));
    }
}
