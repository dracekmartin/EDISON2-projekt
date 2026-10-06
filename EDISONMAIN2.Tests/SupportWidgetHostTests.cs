using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Tests;

[Collection(SharedStaticStateCollection.Name)]
public class SupportWidgetHostTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData(" True ")]
    public void IsEnabled_AcceptsOneAndTrue(string value)
    {
        Assert.True(SupportWidgetHost.IsEnabled(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("yes")]
    public void IsEnabled_RejectsEverythingElse(string? value)
    {
        Assert.False(SupportWidgetHost.IsEnabled(value));
    }

    [Fact]
    public void ScreenshotMode_FollowsEnvironmentVariable()
    {
        var original = Environment.GetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable, "1");
            Assert.True(SupportWidgetHost.ScreenshotMode);

            Environment.SetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable, null);
            Assert.False(SupportWidgetHost.ScreenshotMode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable, original);
        }
    }
}
