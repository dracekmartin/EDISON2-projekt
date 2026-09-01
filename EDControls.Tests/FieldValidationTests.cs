using EDControls.EDInputForm;

namespace EDControls.Tests;

/// <summary>Testy klientské validace Field - typová kontrola + Min/Max range (CheckRange přes GetTypeError).</summary>
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
        var f = new Field { FieldType = FieldType.Number, Min = 0, Max = 100, Value = value };
        Assert.Equal(expectedError, f.GetTypeError());
    }

    [Fact]
    public void Number_NoRange_OnlyChecksType()
    {
        var f = new Field { FieldType = FieldType.Number, Value = "999999" };
        Assert.Null(f.GetTypeError());
    }

    [Fact]
    public void Number_NonInteger_ReportsTypeError()
    {
        var f = new Field { FieldType = FieldType.Number, Value = "12.5" };
        Assert.Equal("Musí být celé číslo", f.GetTypeError());
    }

    [Fact]
    public void Decimal_RangeUsesInvariantCulture()
    {
        var f = new Field { FieldType = FieldType.Decimal, Min = 0.5m, Max = 9.5m, Value = "10.0" };
        Assert.Equal("Hodnota musí být ≤ 9.5", f.GetTypeError());
    }

    [Fact]
    public void EmptyValue_NoTypeError()
    {
        var f = new Field { FieldType = FieldType.Number, Min = 5, Value = "" };
        Assert.Null(f.GetTypeError());   // prázdné řeší Required, ne typová validace
    }

    [Theory]
    [InlineData("1", null)]
    [InlineData("0", null)]
    [InlineData("2", "Musí být 0 nebo 1")]
    public void Bool_ChecksZeroOrOne(string value, string? expectedError)
    {
        var f = new Field { FieldType = FieldType.Bool, Value = value };
        Assert.Equal(expectedError, f.GetTypeError());
    }

    // Barva je volný text, takže bez typové kontroly propadl do hostitelské aplikace libovolný
    // vstup a spadlo to až při vykreslování té barvy (v TemplateDesigneru i po znovuotevření
    // šablony, protože se nesmysl uložil).

    [Theory]
    [InlineData("#FF3366")]
    [InlineData("#80FF3366")]
    [InlineData("FF3366")]        // mřížka nepovinná
    [InlineData("  #ff3366  ")]   // ořez okrajů, malá písmena
    public void Color_ValidHexHasNoError(string value)
    {
        var f = new Field { FieldType = FieldType.Color, Value = value };
        Assert.Null(f.GetTypeError());
    }

    [Theory]
    [InlineData("#ZZZZZZ")]
    [InlineData("#FF33")]
    [InlineData("modrá")]
    [InlineData("#")]
    public void Color_InvalidHexReportsError(string value)
    {
        var f = new Field { FieldType = FieldType.Color, Value = value };
        Assert.Equal("Neplatná barva - zadejte #RRGGBB nebo #AARRGGBB", f.GetTypeError());
    }

    [Theory]
    [InlineData("{BARVA_LINKY}")]
    [InlineData("  {BARVA_LINKY}  ")]
    public void Color_FieldReferenceIsAllowed(string value)
    {
        // Hostitelská aplikace může barvu vázat na data (barva podle záznamu) - odkaz na pole
        // je legitimní hodnota, ne překlep.
        var f = new Field { FieldType = FieldType.Color, Value = value };
        Assert.Null(f.GetTypeError());
    }

    [Theory]
    [InlineData("#FF33{X}")]
    [InlineData("{A}{B}")]
    public void Color_MixOfHexAndReferenceIsError(string value)
    {
        var f = new Field { FieldType = FieldType.Color, Value = value };
        Assert.NotNull(f.GetTypeError());
    }

    [Fact]
    public void Color_EmptyIsAllowed()
    {
        // Vymazání barvy je legitimní vstup - význam si určí hostitelská aplikace
        // (zděděná barva / nic nekreslit), není to typová chyba.
        var f = new Field { FieldType = FieldType.Color, Value = "" };
        Assert.Null(f.GetTypeError());
    }

    [Fact]
    public void Color_InvalidHexMarksFieldAsError()
    {
        // Zápis do Value rovnou nastaví ValidationError → pole zčervená hned při psaní.
        var f = new Field { FieldType = FieldType.Color, Value = "#FF3366" };
        Assert.False(f.HasError);

        f.Value = "nesmysl";
        Assert.True(f.HasError);

        f.Value = "#FF3366";
        Assert.False(f.HasError);
    }

    // ── Desetinná čárka ────────────────────────────────────────────────────────
    // Uživatel má na numerické klávesnici čárku, uvnitř i na drátě je hodnota vždy s tečkou.
    // Čárka je VŽDY desetinná, nikdy oddělovač tisíců (nález Jan 1. 9., #13158).

    [Theory]
    [InlineData("1,5", "1.5")]
    [InlineData("1.5", "1.5")]
    [InlineData("-2,5", "-2.5")]
    [InlineData("1,500", "1.500")]     // čárka = desetinná, ne tisíce
    [InlineData("12", "12")]
    [InlineData("", "")]
    public void Decimal_CarkaSeUlozíJakoTecka(string zadano, string ulozeno)
    {
        var f = new Field { FieldType = FieldType.Decimal, Value = zadano };
        Assert.Equal(ulozeno, f.Value);
        Assert.Null(f.GetTypeError());
    }

    [Fact]
    public void Decimal_CarkaNeniOddelovacTisicu()
    {
        // Dřív to procházelo jako 1500 a rozsah 0-10 tedy hlásil chybu; „1,500" znamená 1.5.
        var f = new Field { FieldType = FieldType.Decimal, Min = 0, Max = 10, Value = "1,500" };
        Assert.Null(f.GetTypeError());
    }

    [Fact]
    public void Decimal_TeckaTakyNeniOddelovacTisicu()
    {
        var f = new Field { FieldType = FieldType.Decimal, Min = 0, Max = 10, Value = "1.500" };
        Assert.Null(f.GetTypeError());
    }

    [Fact]
    public void Decimal_CarkaVRozsahuSePosuzujeJakoDesetinna()
    {
        var f = new Field { FieldType = FieldType.Decimal, Min = 2, Max = 10, Value = "1,5" };
        Assert.Equal("Hodnota musí být ≥ 2", f.GetTypeError());
    }

    [Fact]
    public void CeloCiselnePole_CarkaZustavaChybou()
    {
        // „1,5" v poli pro celé číslo nemá co dělat - čárka se převede a typ pak neprojde.
        var f = new Field { FieldType = FieldType.Number, Value = "1,5" };
        Assert.Equal("1.5", f.Value);
        Assert.Equal("Musí být celé číslo", f.GetTypeError());
    }

    [Fact]
    public void TextovePole_CarkuNechavaBytOnaJeSoucastTextu()
    {
        var f = new Field { FieldType = FieldType.Text, Value = "Praha, Brno" };
        Assert.Equal("Praha, Brno", f.Value);
    }

    [Fact]
    public void TypNastavenyPoHodnote_CarkuTakyPrenormalizuje()
    {
        // Pořadí plnění není zaručené u každého volajícího; hodnota nesmí zůstat s čárkou.
        var f = new Field { Value = "1,5" };
        f.FieldType = FieldType.Decimal;
        Assert.Equal("1.5", f.Value);
    }
}
