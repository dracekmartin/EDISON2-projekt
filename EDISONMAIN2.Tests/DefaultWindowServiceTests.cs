using EDControls;
using EDISONMAIN2.Modules;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>Výchozí okno: parsování id z GetUserDefaultForm a odesílaný payload SetUserDefaultForm.</summary>
public class DefaultWindowServiceTests
{
    private static IMLRest Mock(object[]? getDefaultWindow = null)
    {
        var ml = Substitute.For<IMLRest>();
        ml.Delimiter.Returns('~');
        ml.QueryOrThrow("ALVA.USERS.ApiEdison", "GetUserDefaultForm", Arg.Any<object[]>())
            .Returns(getDefaultWindow ?? new object[] { "", "0" });
        return ml;
    }

    [Theory]
    [InlineData("17", 17)]
    [InlineData("0", 0)]
    [InlineData("", 0)]
    public void Load_ParsesOutput(string value, int expected)
    {
        var ml = Mock(getDefaultWindow: new object[] { "", value });

        Assert.Equal(expected, DefaultWindowService.Load(ml));
    }

    [Fact]
    public void Load_EmptyOutput_Zero()
    {
        var ml = Mock(getDefaultWindow: new object[] { "" });

        Assert.Equal(0, DefaultWindowService.Load(ml));
    }

    [Fact]
    public void Set_SendsFormId()
    {
        var ml = Mock();

        DefaultWindowService.Set(ml, 23);

        ml.Received().QueryOrThrow("ALVA.USERS.ApiEdison", "SetUserDefaultForm",
            Arg.Is<object[]>(a => a.Length == 1 && (int)a[0] == 23));
    }
}
