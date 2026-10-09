using EDControls.ErrorReporting;

namespace EDControls.Tests;

/// <summary>
/// Detekce interní stanice pro hlášení chyb i strom shellu: doména, nebo registrový override
/// <c>HKCU\Software\Edison\MlineDomain</c> čtený jako legacy VB <c>CBool</c>.
/// </summary>
public class MlineEnvironmentTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("-1")]     // CBool: nenulové číslo (VB True == -1)
    [InlineData("42")]
    public void IsTruthyRegistryValue_Truthy(string value) =>
        Assert.True(MlineEnvironment.IsTruthyRegistryValue(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("abc")]    // ne bool ani číslo
    [InlineData("ano")]
    public void IsTruthyRegistryValue_Falsy(string? value) =>
        Assert.False(MlineEnvironment.IsTruthyRegistryValue(value));

    [Theory]
    [InlineData("main.m-line.cz", null, true)]
    [InlineData("MAIN.M-LINE.CZ", null, true)]   // bez ohledu na velikost písmen
    [InlineData("firma.local", "1", true)]        // override přebije cizí doménu
    [InlineData("firma.local", "-1", true)]       // stanice s -1 vidí interní uzly, hlášení musí být -internal
    [InlineData("firma.local", null, false)]
    [InlineData("", "0", false)]                  // mimo doménu (workgroup)
    [InlineData(null, null, false)]               // doménu nešlo zjistit
    public void Detect_DomainOrRegistryOverride(string? domain, string? registry, bool expected) =>
        Assert.Equal(expected, MlineEnvironment.Detect(() => domain, () => registry));
}
