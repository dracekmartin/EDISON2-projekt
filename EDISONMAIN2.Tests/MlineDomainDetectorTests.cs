using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
// (IsDomainMline testy mutují statické providery - sada běží bez paralelizace, viz AssemblyInfo)

namespace EDISONMAIN2.Tests;

/// <summary>
/// <see cref="MlineDomainDetector.ParseVirtualDomainValue"/> replikuje VB <c>CBool</c> nad registry
/// hodnotou <c>MlineDomain</c>: <c>"1"</c>/<c>"True"</c>/nenulové číslo → true, jinak false.
/// Legacy parita - dev override viditelnosti mlineOnly uzlů závisí na těchto edge-casech.
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

    [Theory]
    [InlineData("main.m-line.cz", false, true)]
    [InlineData("MAIN.M-LINE.CZ", false, true)]  // case-insensitive
    [InlineData("firma.local", true, true)]      // dev override přebije cizí doménu
    [InlineData("firma.local", false, false)]
    [InlineData("", false, false)]               // mimo doménu (workgroup)
    public void IsDomainMline_DomainOrVirtualFlag(string domain, bool virtualFlag, bool expected)
    {
        var originalDomain = MlineDomainDetector.DomainNameProvider;
        var originalFlag = MlineDomainDetector.VirtualDomainFlagProvider;
        try
        {
            MlineDomainDetector.DomainNameProvider = () => domain;
            MlineDomainDetector.VirtualDomainFlagProvider = () => virtualFlag;

            Assert.Equal(expected, MlineDomainDetector.IsDomainMline());
        }
        finally
        {
            MlineDomainDetector.DomainNameProvider = originalDomain;
            MlineDomainDetector.VirtualDomainFlagProvider = originalFlag;
        }
    }
}
