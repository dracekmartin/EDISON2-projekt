using System.Text.Json;
using ALWA.SYS;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// <see cref="MLRestJson.CallAsync"/> je jediný wire kontrakt JSON-envelope volání: request se
/// serializuje pod sdílený klíč <c>"payload"</c>, response se čte ze stejného klíče. Obě strany
/// (Caché) se na klíči musí shodnout - proto ho fixujeme testem.
/// </summary>
public class MLRestJsonTests
{
    private record Req(int Id, string Name);
    private record Resp(bool Ok, string Message);

    private const string Cls = "ALVA.Some.Class";
    private const string Method = "Do";

    [Fact]
    public async Task CallAsync_SerializesRequestUnderPayloadKey()
    {
        var mock = Substitute.For<IMLRest>();
        CVar? captured = null;
        mock.QueryOrThrow(Cls, Method, Arg.Do<CVar>(c => captured = c))
            .Returns(Output("""{"Ok":true,"Message":"x"}"""));

        await MLRestJson.CallAsync<Req, Resp>(mock, Cls, Method, new Req(7, "abc"));

        var payload = captured!["payload"].Value?.ToString();
        var roundTripped = JsonSerializer.Deserialize<Req>(payload!);
        Assert.Equal(new Req(7, "abc"), roundTripped);
    }

    [Fact]
    public async Task CallAsync_DeserializesResponseFromPayloadKey()
    {
        var mock = Substitute.For<IMLRest>();
        mock.QueryOrThrow(Cls, Method, Arg.Any<CVar>())
            .Returns(Output("""{"Ok":true,"Message":"hello"}"""));

        var resp = await MLRestJson.CallAsync<Req, Resp>(mock, Cls, Method, new Req(1, "q"));

        Assert.True(resp.Ok);
        Assert.Equal("hello", resp.Message);
    }

    [Fact]
    public async Task CallAsync_MissingPayload_DefaultsToEmptyObject()
    {
        // OUT bez "payload" → json "{}" → prázdný Resp (default hodnoty), ne výjimka.
        var mock = Substitute.For<IMLRest>();
        mock.QueryOrThrow(Cls, Method, Arg.Any<CVar>()).Returns(new CVar());

        var resp = await MLRestJson.CallAsync<Req, Resp>(mock, Cls, Method, new Req(1, "q"));

        Assert.False(resp.Ok);
        Assert.Null(resp.Message);
    }

    [Fact]
    public async Task CallAsync_JsonNullPayload_Throws()
    {
        var mock = Substitute.For<IMLRest>();
        mock.QueryOrThrow(Cls, Method, Arg.Any<CVar>()).Returns(Output("null"));

        await Assert.ThrowsAsync<System.InvalidOperationException>(
            () => MLRestJson.CallAsync<Req, Resp>(mock, Cls, Method, new Req(1, "q")));
    }

    private static CVar Output(string payloadJson)
    {
        var cv = new CVar();
        cv["payload"] = new CVar(payloadJson);
        return cv;
    }
}
