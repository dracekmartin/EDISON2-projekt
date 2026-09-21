namespace EDISONMAIN2.Tests;

/// <summary>
/// Kontrakt argumentů „Připojit k databázi": producent (<c>ToArguments</c>) a konzument
/// (<c>Parse</c> při startu nové instance) musí zůstat párové; cizí tokeny startu se ignorují.
/// </summary>
public class ConnectionOverrideTests
{
    [Fact]
    public void Parse_NoConnectionArgs_Null()
    {
        Assert.Null(ConnectionOverride.Parse([]));
        Assert.Null(ConnectionOverride.Parse(["reason=2"]));
    }

    [Fact]
    public void Parse_ServerAndRestPort_CaseInsensitive()
    {
        var parsed = ConnectionOverride.Parse(["SERVER=10.0.0.5", "RestPort=57770"]);

        Assert.Equal(new ConnectionOverride("10.0.0.5", 57770), parsed);
    }

    [Theory]
    [InlineData("server=10.0.0.5")]
    [InlineData("restport=57770")]
    [InlineData("server=10.0.0.5", "restport=abc")]
    [InlineData("server=", "restport=57770")]
    [InlineData("server=10.0.0.5", "restport=0")]
    public void Parse_MissingOrInvalidPart_Null(params string[] args)
    {
        Assert.Null(ConnectionOverride.Parse(args));
    }

    [Fact]
    public void ToArguments_RoundTripsThroughParse()
    {
        var original = new ConnectionOverride("cache.firma.cz", 12345);

        Assert.Equal(original, ConnectionOverride.Parse(original.ToArguments()));
    }
}
