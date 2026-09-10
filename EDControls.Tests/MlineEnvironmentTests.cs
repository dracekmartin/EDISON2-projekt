using EDControls.ErrorReporting;

namespace EDControls.Tests;

/// <summary>Parsování registry override <c>HKCU\Software\Edison\MlineDomain</c> - legacy CBool parita.</summary>
public class MlineEnvironmentTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("True")]
    public void IsTruthyRegistryValue_Truthy(string value) =>
        Assert.True(MlineEnvironment.IsTruthyRegistryValue(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("ano")]
    public void IsTruthyRegistryValue_Falsy(string? value) =>
        Assert.False(MlineEnvironment.IsTruthyRegistryValue(value));
}
