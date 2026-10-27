using ALWA.SYS;
using EDControls;

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

    [DevServerFact]
    public void SessionWithoutLock_IsSessionEndNotTransport()
    {
        // Šev k MLRestFailure: skutečný text serveru, když session nemá ACTIVITY zámek. Před kontrolou zámku
        // server zapíše jen řádek historie aktivity do temp globálu pod testovací firmou (uklidí ho Activity.Update).
        var rest = DevServer.Connect(DevServer.WriteIdfa, DevServer.WriteMlp);
        rest.CacheLocks = 1;
        var input = new CVar();
        input["scope"] = new CVar("EDISON2.IT");
        input["key"] = new CVar("session-end");

        var ex = Assert.ThrowsAny<Exception>(() => rest.QueryOrThrow("ALVA.EDControls.UserData", "Load", input));

        Assert.True(MLRestFailure.IsSessionEnd(ex), "text konce session se změnil");
        Assert.False(MLRestFailure.IsTransport(ex));
    }
}
