using System.Globalization;
using EDControls.EDList;

namespace EDControls.Tests;

public class EDListFormattingTests
{
    // ─── FormatHHMM ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("830", "08:30")]
    [InlineData("1245", "12:45")]
    [InlineData("600", "06:00")]
    [InlineData("0", "00:00")]
    [InlineData("2359", "23:59")]
    public void FormatHHMM_ValidInput_ReturnsFormatted(string input, string expected)
    {
        Assert.Equal(expected, EDList.EDList.FormatHHMM(input));
    }

    [Fact]
    public void FormatHHMM_NonNumeric_ReturnsRaw()
    {
        Assert.Equal("abc", EDList.EDList.FormatHHMM("abc"));
    }

    [Fact]
    public void FormatHHMM_Null_ReturnsNull()
    {
        Assert.Null(EDList.EDList.FormatHHMM(null));
    }

    // ─── FormatDate ─────────────────────────────────────────────────────────────

    [Fact]
    public void FormatDate_ValidIsoDate_FormatsCorrectly()
    {
        string? result = EDList.EDList.FormatDate("2024-03-15", "dd.MM.yyyy");
        Assert.Equal("15.03.2024", result);
    }

    [Fact]
    public void FormatDate_ValidIsoDateTime_FormatsCorrectly()
    {
        string? result = EDList.EDList.FormatDate("2024-03-15T08:30:00", "dd.MM.yyyy HH:mm");
        Assert.Equal("15.03.2024 08:30", result);
    }

    [Fact]
    public void FormatDate_InvalidString_ReturnsRaw()
    {
        Assert.Equal("not-a-date", EDList.EDList.FormatDate("not-a-date", "dd.MM.yyyy"));
    }

    [Fact]
    public void FormatDate_Null_ReturnsNull()
    {
        Assert.Null(EDList.EDList.FormatDate(null, "dd.MM.yyyy"));
    }

    [Fact]
    public void FormatDate_Empty_ReturnsEmpty()
    {
        Assert.Equal("", EDList.EDList.FormatDate("", "dd.MM.yyyy"));
    }

    // ─── FormatNumber ───────────────────────────────────────────────────────────

    [Fact]
    public void FormatNumber_ValidNumber_FormatsCorrectly()
    {
        string expected = 1234.5.ToString("N2", CultureInfo.CurrentCulture);
        Assert.Equal(expected, EDList.EDList.FormatNumber("1234.5", "N2"));
    }

    [Fact]
    public void FormatNumber_EmptyFormat_ReturnsRaw()
    {
        Assert.Equal("1234.5", EDList.EDList.FormatNumber("1234.5", ""));
    }

    [Fact]
    public void FormatNumber_InvalidNumber_ReturnsRaw()
    {
        Assert.Equal("abc", EDList.EDList.FormatNumber("abc", "N2"));
    }

    [Fact]
    public void FormatNumber_Null_ReturnsNull()
    {
        Assert.Null(EDList.EDList.FormatNumber(null, "N2"));
    }

    // ─── ParseColumns ───────────────────────────────────────────────────────────

    [Fact]
    public void ParseColumns_EmptyOutput_ReturnsEmpty()
    {
        var output = new object[] { "0" };
        var cols = EDList.EDList.ParseColumns(output);
        Assert.Empty(cols);
    }

    [Fact]
    public void ParseColumns_SingleColumn_ParsesAllProperties()
    {
        string json = """[{"key":"rz","label":"RZ","type":"TEXT","format":"","width":110,"align":"LEFT","sortable":true,"visible":true,"isLink":false}]""";
        var output = new object[] { "1", json };

        var cols = EDList.EDList.ParseColumns(output);

        var col = Assert.Single(cols);
        Assert.Equal("rz", col.Key);
        Assert.Equal("RZ", col.Label);
        Assert.Equal(EListColumnType.Text, col.Type);
        Assert.Equal(110, col.Width);
        Assert.Equal(EListColumnAlign.Left, col.Align);
        Assert.True(col.Sortable);
        Assert.True(col.Visible);
        Assert.False(col.IsLink);
    }

    [Fact]
    public void ParseColumns_MultipleTypes_ParsedCorrectly()
    {
        string json = """
        [
            {"key":"a","label":"A","type":"NUMBER","format":"N2","width":80,"align":"RIGHT","sortable":false,"visible":true,"isLink":false},
            {"key":"b","label":"B","type":"BOOL","format":"","width":60,"align":"CENTER","sortable":false,"visible":true,"isLink":false},
            {"key":"c","label":"C","type":"DATE","format":"dd.MM.yyyy","width":100,"align":"","sortable":true,"visible":false,"isLink":true},
            {"key":"d","label":"D","type":"HHMM","format":"","width":75,"align":"","sortable":false,"visible":true,"isLink":false},
            {"key":"e","label":"E","type":"COLOR","format":"","width":60,"align":"","sortable":false,"visible":true,"isLink":false}
        ]
        """;
        var output = new object[] { "5", json };
        var cols = EDList.EDList.ParseColumns(output);

        Assert.Equal(5, cols.Count);
        Assert.Equal(EListColumnType.Number, cols[0].Type);
        Assert.Equal("N2", cols[0].Format);
        Assert.Equal(EListColumnType.Bool, cols[1].Type);
        Assert.Equal(EListColumnType.Date, cols[2].Type);
        Assert.True(cols[2].IsLink);
        Assert.False(cols[2].Visible);
        Assert.Equal(EListColumnType.HoursMinutes, cols[3].Type);
        Assert.Equal(EListColumnType.Color, cols[4].Type);
    }
}
