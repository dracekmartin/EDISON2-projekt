namespace EDControls.Tests;

public class SearchNormalizationTests
{
    [Theory]
    [InlineData("Pošta", "posta")]
    [InlineData("Řazení", "razeni")]
    [InlineData("ÁÉŮ", "aeu")]
    [InlineData("Objednávky", "objednavky")]
    [InlineData("bez diakritiky", "bez diakritiky")]
    public void NormalizeForSearch_StripsDiacriticsAndLowercases(string input, string expected)
        => Assert.Equal(expected, SearchNormalization.NormalizeForSearch(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NormalizeForSearch_NullOrEmpty_ReturnsEmpty(string? input)
        => Assert.Equal("", SearchNormalization.NormalizeForSearch(input));
}
