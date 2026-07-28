using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>
/// <see cref="MlineDomainDetector.ParseVirtualDomainValue"/> replikuje VB <c>CBool</c> nad registry
/// hodnotou <c>MlineDomain</c>: <c>"1"</c>/<c>"True"</c>/nenulové číslo → true, jinak false.
/// Legacy parita — dev override viditelnosti mlineOnly uzlů závisí na těchto edge-casech.
/// </summary>
public class MlineDomainDetectorTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("True")]
    [InlineData("true")]
    [InlineData("-1")]     // CBool: nenulové číslo (VB True == -1)
    [InlineData("42")]
    public void ParseVirtualDomainValue_Truthy(string value)
    {
        Assert.True(MlineDomainDetector.ParseVirtualDomainValue(value));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("False")]
    [InlineData("false")]
    [InlineData("")]
    [InlineData("abc")]    // ne bool ani číslo → false
    public void ParseVirtualDomainValue_Falsy(string value)
    {
        Assert.False(MlineDomainDetector.ParseVirtualDomainValue(value));
    }

    [Fact]
    public void ParseVirtualDomainValue_Null_False()
    {
        Assert.False(MlineDomainDetector.ParseVirtualDomainValue(null));
    }
}
