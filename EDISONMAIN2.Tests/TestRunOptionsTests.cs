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
}
