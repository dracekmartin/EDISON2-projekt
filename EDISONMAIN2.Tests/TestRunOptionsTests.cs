namespace EDISONMAIN2.Tests;

public class TestRunOptionsTests
{
    [Theory]
    [InlineData("e2e=1")]
    [InlineData("E2E=1")]
    public void Parse_TokenAmongConnectionArgs_ReturnsOptions(string token)
    {
        Assert.NotNull(TestRunOptions.Parse(["server=host", "restport=57770", token]));
    }

    [Fact]
    public void Parse_WithoutToken_ReturnsNull()
    {
        Assert.Null(TestRunOptions.Parse(["server=host", "restport=57770", "e2e=0", "idfa=CB_DP"]));
    }

    [Fact]
    public void Parse_IdfaToken_SetsStartCompany()
    {
        Assert.Equal("CB_DP", TestRunOptions.Parse(["e2e=1", "idfa=CB_DP"])!.StartIdfa);
        Assert.Null(TestRunOptions.Parse(["e2e=1"])!.StartIdfa);
    }

    [Fact]
    public void Parse_PreShellPopupToken_OnlyWithTestRun()
    {
        Assert.True(TestRunOptions.Parse(["e2e=1", "preshellpopup=1"])!.PreShellPopup);
        Assert.False(TestRunOptions.Parse(["e2e=1"])!.PreShellPopup);
        Assert.Null(TestRunOptions.Parse(["preshellpopup=1"]));
    }

    [Fact]
    public void Parse_RealLoginToken_OnlyWithTestRun()
    {
        Assert.True(TestRunOptions.Parse(["e2e=1", "login=1"])!.RealLogin);
        Assert.False(TestRunOptions.Parse(["e2e=1"])!.RealLogin);
        Assert.Null(TestRunOptions.Parse(["login=1"]));
    }
}
