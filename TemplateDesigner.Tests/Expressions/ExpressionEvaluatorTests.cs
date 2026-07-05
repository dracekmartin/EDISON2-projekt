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

    // ── Aritmetika (počítaná pole) — EvaluateToString ────────────────────────

    [Fact]
    public void Aritmetika_ZakladniOperace()
    {
        var d = Data(("A", "2"), ("B", "3"), ("C", "4"));
        Assert.Equal("6",  ExpressionEvaluator.EvaluateToString("{A} * {B}", d));
        Assert.Equal("5",  ExpressionEvaluator.EvaluateToString("{A} + {B}", d));
        Assert.Equal("-1", ExpressionEvaluator.EvaluateToString("{A} - {B}", d));
        Assert.Equal("2",  ExpressionEvaluator.EvaluateToString("{C} / {A}", d));
    }

    [Fact]
    public void Aritmetika_PrioritaAZavorky()
    {
        var d = Data(("A", "2"), ("B", "3"), ("C", "4"));
        Assert.Equal("14", ExpressionEvaluator.EvaluateToString("{A} + {B} * {C}", d));   // * před +
        Assert.Equal("20", ExpressionEvaluator.EvaluateToString("({A} + {B}) * {C}", d)); // závorky
    }

    [Fact]
    public void Aritmetika_CenaKratPocet_TypickeUziti() =>
        Assert.Equal("30", ExpressionEvaluator.EvaluateToString("{CENA} * {POCET}", Data(("CENA", "10"), ("POCET", "3"))));

    [Fact]
    public void Aritmetika_BezFormatu_ZadneFloatArtefakty()
    {
        // 0.1 * 3 = 0.30000000000000004 v IEEE-754 → nesmí prosáknout do buňky
        Assert.Equal("0.3", ExpressionEvaluator.EvaluateToString("{A} * {B}", Data(("A", "0.1"), ("B", "3"))));
        // 1 / 3 → zaokrouhleno na 10 des. míst, ne 17
        Assert.Equal("0.3333333333", ExpressionEvaluator.EvaluateToString("{A} / {B}", Data(("A", "1"), ("B", "3"))));
    }

    [Fact]
    public void Aritmetika_NaN_Infinity_PrazdnyVysledek()
    {
        // Pole s „NaN"/„Infinity" se nešíří do buňky jako text
        Assert.Equal("", ExpressionEvaluator.EvaluateToString("{A} + {B}", Data(("A", "NaN"), ("B", "1"))));
        Assert.Equal("", ExpressionEvaluator.EvaluateToString("{A} * {B}", Data(("A", "Infinity"), ("B", "2"))));
    }

    [Fact]
    public void Aritmetika_UnarniMinus()
    {
        Assert.Equal("-5", ExpressionEvaluator.EvaluateToString("-{X}", Data(("X", "5"))));
        Assert.Equal("2", ExpressionEvaluator.EvaluateToString("{A} - -{B}", Data(("A", "-1"), ("B", "3"))));
    }

    [Fact]
    public void Aritmetika_DeleniNulou_PrazdnyVysledek() =>
        Assert.Equal("", ExpressionEvaluator.EvaluateToString("{A} / {B}", Data(("A", "10"), ("B", "0"))));

    [Fact]
    public void Aritmetika_NeciselnyOperand_PrazdnyVysledek() =>
        Assert.Equal("", ExpressionEvaluator.EvaluateToString("{A} * {B}", Data(("A", "text"), ("B", "3"))));

    [Fact]
    public void Aritmetika_PlusNaText_Konkatenace() =>
        Assert.Equal("abcdef", ExpressionEvaluator.EvaluateToString("{A} + {B}", Data(("A", "abc"), ("B", "def"))));

    [Fact]
    public void EvaluateToString_PrazdnyNeboChybnyVyraz_Prazdno()
    {
        Assert.Equal("", ExpressionEvaluator.EvaluateToString(null, null));
        Assert.Equal("", ExpressionEvaluator.EvaluateToString("{A} *", Data(("A", "5"))));   // chyba parse → fail-soft
    }

    [Fact]
    public void Aritmetika_LzeKombinovatSPorovnanim() =>
        // porovnání má nižší prioritu než aritmetika
        Assert.True(ExpressionEvaluator.IsVisible("{A} * 2 > 10", Data(("A", "6"))));

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
