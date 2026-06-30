using EDControls.EDInputForm;

namespace EDControls.Tests;

/// <summary>Testy klientské validace Field — typová kontrola + Min/Max range (CheckRange přes GetTypeError).</summary>
public class FieldValidationTests
{
    [Theory]
    [InlineData("50", null)]                              // v rozsahu
    [InlineData("0", null)]                               // dolní mez (inkluzivní)
    [InlineData("100", null)]                             // horní mez (inkluzivní)
    [InlineData("-5", "Hodnota musí být ≥ 0")]
    [InlineData("150", "Hodnota musí být ≤ 100")]
    public void Number_CheckRange(string value, string? expectedError)
    {
        var f = new Field { FieldType = EFieldType.Number, Min = 0, Max = 100, Value = value };
        Assert.Equal(expectedError, f.GetTypeError());
    }

    [Fact]
    public void Number_NoRange_OnlyChecksType()
    {
        var f = new Field { FieldType = EFieldType.Number, Value = "999999" };
        Assert.Null(f.GetTypeError());
    }

    [Fact]
    public void Number_NonInteger_ReportsTypeError()
    {
        var f = new Field { FieldType = EFieldType.Number, Value = "12.5" };
        Assert.Equal("Musí být celé číslo", f.GetTypeError());
    }

    [Fact]
    public void Decimal_RangeUsesInvariantCulture()
    {
        var f = new Field { FieldType = EFieldType.Decimal, Min = 0.5m, Max = 9.5m, Value = "10.0" };
        Assert.Equal("Hodnota musí být ≤ 9.5", f.GetTypeError());
    }

    [Fact]
    public void EmptyValue_NoTypeError()
    {
        var f = new Field { FieldType = EFieldType.Number, Min = 5, Value = "" };
        Assert.Null(f.GetTypeError());   // prázdné řeší Required, ne typová validace
    }

    [Theory]
    [InlineData("1", null)]
    [InlineData("0", null)]
    [InlineData("2", "Musí být 0 nebo 1")]
    public void Bool_ChecksZeroOrOne(string value, string? expectedError)
    {
        var f = new Field { FieldType = EFieldType.Bool, Value = value };
        Assert.Equal(expectedError, f.GetTypeError());
    }
}
