using EDControls.EDInputForm;

namespace EDControls.Tests;

/// <summary>
/// <see cref="EDNumericInput.IsValid"/> je paste-validace numerických polí: číslice, jeden vedoucí
/// <c>-</c>, v <c>Decimal</c> módu jeden oddělovač (<c>.</c> nebo <c>,</c>). Ostatní se odmítne.
/// </summary>
public class EDNumericInputTests
{
    [Theory]
    [InlineData("123", "Integer")]
    [InlineData("-5", "Integer")]
    [InlineData("0", "Integer")]
    [InlineData("", "Integer")]
    public void IsValid_Integer_AcceptsDigitsAndLeadingMinus(string text, string mode)
    {
        Assert.True(EDNumericInput.IsValid(text, mode));
    }

    [Theory]
    [InlineData("1.5", "Decimal")]
    [InlineData("1,5", "Decimal")]
    [InlineData("-3.14", "Decimal")]
    [InlineData(".5", "Decimal")]
    public void IsValid_Decimal_AcceptsSingleSeparator(string text, string mode)
    {
        Assert.True(EDNumericInput.IsValid(text, mode));
    }

    [Theory]
    [InlineData("12a3", "Integer")]      // písmeno
    [InlineData("1-2", "Integer")]       // minus jinde než na začátku
    [InlineData("1.5", "Integer")]       // oddělovač v Integer módu
    [InlineData("1.2.3", "Decimal")]     // dva oddělovače
    [InlineData("1.2,3", "Decimal")]     // dva různé oddělovače
    [InlineData("1 2", "Decimal")]       // mezera
    public void IsValid_Rejects(string text, string mode)
    {
        Assert.False(EDNumericInput.IsValid(text, mode));
    }
}
