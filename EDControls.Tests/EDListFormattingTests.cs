using System.Globalization;
using ALWA.SYS;
using EDControls.EDList;

namespace EDControls.Tests;

public class EDListFormattingTests
{

    [Theory]
    [InlineData("830", "08:30")]
    [InlineData("1245", "12:45")]
    [InlineData("600", "06:00")]
    [InlineData("0", "00:00")]
    [InlineData("2359", "23:59")]
    public void FormatHHMM_ValidInput_ReturnsFormatted(string input, string expected)
    {
        Assert.Equal(expected, EDListParsing.FormatHHMM(input));
    }

    [Fact]
    public void FormatHHMM_NonNumeric_ReturnsRaw()
    {
        Assert.Equal("abc", EDListParsing.FormatHHMM("abc"));
    }

    [Fact]
    public void FormatHHMM_Null_ReturnsNull()
    {
        Assert.Null(EDListParsing.FormatHHMM(null));
    }

    [Fact]
    public void FormatDate_ValidIsoDate_FormatsCorrectly()
    {
        string? result = EDListParsing.FormatDate("2024-03-15", "dd.MM.yyyy");
        Assert.Equal("15.03.2024", result);
    }

    [Fact]
    public void FormatDate_ValidIsoDateTime_FormatsCorrectly()
    {
        string? result = EDListParsing.FormatDate("2024-03-15T08:30:00", "dd.MM.yyyy HH:mm");
        Assert.Equal("15.03.2024 08:30", result);
    }

    [Fact]
    public void FormatDate_InvalidString_ReturnsRaw()
    {
        Assert.Equal("not-a-date", EDListParsing.FormatDate("not-a-date", "dd.MM.yyyy"));
    }

    [Fact]
    public void FormatDate_Null_ReturnsNull()
    {
        Assert.Null(EDListParsing.FormatDate(null, "dd.MM.yyyy"));
    }

    [Fact]
    public void FormatDate_Empty_ReturnsEmpty()
    {
        Assert.Equal("", EDListParsing.FormatDate("", "dd.MM.yyyy"));
    }

    [Fact]
    public void FormatNumber_ValidNumber_FormatsCorrectly()
    {
        string expected = 1234.5.ToString("N2", CultureInfo.CurrentCulture);
        Assert.Equal(expected, EDListParsing.FormatNumber("1234.5", "N2"));
    }

    [Fact]
    public void FormatNumber_EmptyFormat_ReturnsRaw()
    {
        Assert.Equal("1234.5", EDListParsing.FormatNumber("1234.5", ""));
    }

    [Fact]
    public void FormatNumber_InvalidNumber_ReturnsRaw()
    {
        Assert.Equal("abc", EDListParsing.FormatNumber("abc", "N2"));
    }

    [Fact]
    public void FormatNumber_Null_ReturnsNull()
    {
        Assert.Null(EDListParsing.FormatNumber(null, "N2"));
    }

    [Fact]
    public void ParseColumns_EmptyOutput_ReturnsEmpty()
    {
        var output = new CVar();
        var cols = EDListParsing.ParseColumns(output);
        Assert.Empty(cols);
    }

    [Fact]
    public void ParseColumns_SingleColumn_ParsesAllProperties()
    {
        string json = """[{"key":"rz","label":"RZ","type":"TEXT","format":"","width":110,"align":"LEFT","sortable":true,"visible":true}]""";
        var output = new CVar();
        output["1"] = new CVar(json);

        var cols = EDListParsing.ParseColumns(output);

        var col = Assert.Single(cols);
        Assert.Equal("rz", col.Key);
        Assert.Equal("RZ", col.Label);
        Assert.Equal(ListColumnType.Text, col.Type);
        Assert.Equal(110, col.Width);
        Assert.Equal(ListColumnAlign.Left, col.Align);
        Assert.True(col.Sortable);
        Assert.True(col.Visible);
    }

    [Fact]
    public void ParseColumns_MultipleTypes_ParsedCorrectly()
    {
        string json = """
        [
            {"key":"a","label":"A","type":"NUMBER","format":"N2","width":80,"align":"RIGHT","sortable":false,"visible":true},
            {"key":"b","label":"B","type":"BOOL","format":"","width":60,"align":"CENTER","sortable":false,"visible":true},
            {"key":"c","label":"C","type":"DATE","format":"dd.MM.yyyy","width":100,"align":"","sortable":true,"visible":false},
            {"key":"d","label":"D","type":"HHMM","format":"","width":75,"align":"","sortable":false,"visible":true},
            {"key":"e","label":"E","type":"COLOR","format":"","width":60,"align":"","sortable":false,"visible":true}
        ]
        """;
        var output = new CVar();
        output["1"] = new CVar(json);
        var cols = EDListParsing.ParseColumns(output);

        Assert.Equal(5, cols.Count);
        Assert.Equal(ListColumnType.Number, cols[0].Type);
        Assert.Equal("N2", cols[0].Format);
        Assert.Equal(ListColumnType.Bool, cols[1].Type);
        Assert.Equal(ListColumnType.Date, cols[2].Type);
        Assert.False(cols[2].Visible);
        Assert.Equal(ListColumnType.HoursMinutes, cols[3].Type);
        Assert.Equal(ListColumnType.Color, cols[4].Type);
    }
}
