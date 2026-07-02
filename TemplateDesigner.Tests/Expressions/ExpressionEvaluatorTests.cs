using TemplateDesigner.Models;
using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner.Tests.Expressions;

public class ExpressionEvaluatorTests
{
    private static DataContext Data(params (string key, object? value)[] pairs) =>
        new(pairs.ToDictionary(p => p.key, p => p.value));

    // ── Prázdný / null výraz ─────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PrazdnyVyraz_JeVzdyViditelny(string? expression)
    {
        Assert.True(ExpressionEvaluator.IsVisible(expression, null));
    }

    // ── Porovnání ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("{POCET} > 0", "5", true)]
    [InlineData("{POCET} > 0", "0", false)]
    [InlineData("{POCET} >= 5", "5", true)]
    [InlineData("{POCET} < 10", "5", true)]
    [InlineData("{POCET} <= 4", "5", false)]
    [InlineData("{POCET} == 5", "5", true)]
    [InlineData("{POCET} != 5", "5", false)]
    public void NumerickaPorovnani(string expression, string pocet, bool expected)
    {
        Assert.Equal(expected, ExpressionEvaluator.IsVisible(expression, Data(("POCET", pocet))));
    }

    [Fact]
    public void NumerickePorovnani_PorovnavaCiselne_NeLexikograficky()
    {
        // Lexikograficky "10" < "9"; numericky 10 > 9
        Assert.True(ExpressionEvaluator.IsVisible("{POCET} > 9", Data(("POCET", "10"))));
    }

    [Theory]
    [InlineData("{TYP} == 'MHD'", "MHD", true)]
    [InlineData("{TYP} == 'MHD'", "mhd", true)]   // case-insensitive
    [InlineData("{TYP} == 'MHD'", "AD", false)]
    [InlineData("{TYP} != \"AD\"", "MHD", true)]
    public void StringovaPorovnani(string expression, string typ, bool expected)
    {
        Assert.Equal(expected, ExpressionEvaluator.IsVisible(expression, Data(("TYP", typ))));
    }

    // ── Logické operátory ────────────────────────────────────────────────────

    [Theory]
    [InlineData("{A} == 1 && {B} == 1", "1", "1", true)]
    [InlineData("{A} == 1 && {B} == 1", "1", "0", false)]
    [InlineData("{A} == 1 || {B} == 1", "0", "1", true)]
    [InlineData("{A} == 1 || {B} == 1", "0", "0", false)]
    [InlineData("!({A} == 1)", "1", "", false)]
    [InlineData("!({A} == 1)", "0", "", true)]
    [InlineData("({A} == 1 || {B} == 1) && !({A} == 1 && {B} == 1)", "1", "0", true)]  // XOR
    [InlineData("({A} == 1 || {B} == 1) && !({A} == 1 && {B} == 1)", "1", "1", false)]
    public void LogickeOperatory(string expression, string a, string b, bool expected)
    {
        Assert.Equal(expected, ExpressionEvaluator.IsVisible(expression, Data(("A", a), ("B", b))));
    }

    // ── Truthiness proměnné bez porovnání ────────────────────────────────────

    [Theory]
    [InlineData("", false)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("cokoliv", true)]
    public void SamotnaPromenna_TruthinessKonverze(string value, bool expected)
    {
        Assert.Equal(expected, ExpressionEvaluator.IsVisible("{FLAG}", Data(("FLAG", value))));
    }

    [Fact]
    public void NeexistujiciPromenna_JeFalse()
    {
        Assert.False(ExpressionEvaluator.IsVisible("{NEEXISTUJE}", Data(("JINA", "1"))));
    }

    // ── Fail-open chování ────────────────────────────────────────────────────

    [Theory]
    [InlineData("{POCET} >")]        // neúplný výraz
    [InlineData("{NEUZAVRENA")]      // neuzavřená závorka
    [InlineData("POCET > 0")]        // proměnná bez {}
    public void NevalidniVyraz_FailOpen_Viditelny(string expression)
    {
        Assert.True(ExpressionEvaluator.IsVisible(expression, Data(("POCET", "5"))));
    }

    // ── Validate ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{POCET} > 0")]
    [InlineData("{A} == 'x' && !{B}")]
    public void Validate_ValidniVyraz_VraciNull(string? expression)
    {
        Assert.Null(ExpressionEvaluator.Validate(expression));
    }

    [Theory]
    [InlineData("{POCET} >")]
    [InlineData("{NEUZAVRENA")]
    [InlineData("POCET > 0")]
    [InlineData("({A} == 1")]
    public void Validate_NevalidniVyraz_VraciChybovouZpravu(string expression)
    {
        var error = ExpressionEvaluator.Validate(expression);
        Assert.NotNull(error);
        Assert.NotEmpty(error);
    }
}
