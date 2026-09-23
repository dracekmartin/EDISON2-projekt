using ALWA.SYS;

namespace EDISON2.IntegrationTests;

/// <summary>
/// Chybová cesta celým řetězem: <c>$$$CatchErrRest</c> (větev s <c>sys("C","App")</c>) →
/// <c>^||ML("ERR")</c> → HTTP → <c>RestStatus</c> → výjimka z <c>MLRestAdapter</c>.
/// </summary>
[Trait("Category", "Integration")]
public class ErrorPathTests
{
    [DevServerFact]
    public void QueryOrThrow_ServerMethodFails_ThrowsWithServerMessage()
    {
        var rest = DevServer.Connect(DevServer.WriteIdfa, DevServer.WriteMlp);
        var input = new CVar();
        input["key"] = new CVar("chybi-scope");

        var ex = Assert.ThrowsAny<Exception>(() => rest.QueryOrThrow("ALVA.EDControls.UserData", "Save", input));

        Assert.Contains("scope is required", ex.Message);
    }
}
