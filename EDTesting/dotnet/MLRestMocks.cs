using EDControls;
using NSubstitute;

namespace EDTesting;

/// <summary>
/// NSubstitute helpery pro mockování IMLRest v testech. Skrývají M-line legacy
/// invarianty (ERR field na pozici 0 v positional output) a nastavují dev defaulty.
/// </summary>
public static class MLRestMocks
{
    public const string DefaultIdfa = "CB_DP";
    public const string DefaultDataNamespace = "CB_DP";
    public const string DefaultMlp = "1";

    /// <summary>IMLRest mock s nastaveným Idfa / DataNamespace / Mlp.</summary>
    public static IMLRest Create(
        string idfa = DefaultIdfa,
        string dataNamespace = DefaultDataNamespace,
        string mlp = DefaultMlp)
    {
        var mock = Substitute.For<IMLRest>();
        mock.Idfa.Returns(idfa);
        mock.DataNamespace.Returns(dataNamespace);
        mock.Mlp.Returns(mlp);
        return mock;
    }

    /// <summary>
    /// Positional output array s ERR slotem (index 0 = "") + data.
    /// <c>Output("a", "b")</c> → <c>{ "", "a", "b" }</c>.
    /// </summary>
    public static object[] Output(params object?[] data)
    {
        var result = new object[data.Length + 1];
        result[0] = "";
        Array.Copy(data, 0, result, 1, data.Length);
        return result;
    }

    /// <summary>Positional output reprezentující server-side chybu (ERR slot vyplněn).</summary>
    public static object[] ErrorOutput(string errorMessage)
        => new object[] { errorMessage };

    /// <summary>
    /// Naskriptuje sekvenci výsledků pro <c>QueryOrThrow(className, methodName, object[])</c>.
    /// První call vrátí <paramref name="outputs"/>[0], druhý [1], … (NSubstitute chain Returns).
    /// </summary>
    public static void SetupQuerySequence(
        IMLRest mlRest, string className, string methodName, params object[][] outputs)
    {
        if (outputs.Length == 0)
            throw new ArgumentException("At least one output required.", nameof(outputs));
        if (outputs.Length == 1)
            mlRest.QueryOrThrow(className, methodName, Arg.Any<object[]>())
                  .Returns(outputs[0]);
        else
            mlRest.QueryOrThrow(className, methodName, Arg.Any<object[]>())
                  .Returns(outputs[0], outputs[1..]);
    }
}
