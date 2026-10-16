using ALWA.SYS;
using EDControls;
using EDISONMAIN2.Modules;

namespace EDISON2.IntegrationTests;

/// <summary>Strom modulů: skutečný <c>GetTree</c> (CVar + <c>$lb</c>) do produkčního <see cref="ModuleTreeLoader"/>.</summary>
[Trait("Category", "Integration")]
public class ModuleTreeTests
{
    private static readonly string[] TreeNames = ["main", "system", "partners"];

    // Jako SessionDataLoader: stromy souběžně, každý na vlastním klonu přes RestCallRunner.
    [DevServerFact]
    public async Task GetModuleTree_AllTreesInParallel_ParseNonEmpty()
    {
        var rest = DevServer.Connect();
        var runner = new RestCallRunner(() => rest);
        var loader = new ModuleTreeLoader();

        var trees = await Task.WhenAll(TreeNames.Select(name => runner.RunAsync(r => loader.GetModuleTree(r, name))));

        Assert.All(trees, tree => Assert.NotEmpty(tree));
        Assert.All(trees.SelectMany(tree => tree), module => Assert.False(string.IsNullOrEmpty(module.DisplayedName)));
    }

    // ParseTree neznámý tag tiše zahodí a krátký řádek doplní defaulty; posun polí hlídá jen surový výstup.
    [DevServerTheory]
    [InlineData("main")]
    [InlineData("system")]
    [InlineData("partners")]
    public void GetTree_RawRows_MatchParserLayout(string treeName)
    {
        var input = new CVar();
        input["tree"] = new CVar(treeName);

        CVar raw = DevServer.Connect().QueryOrThrow("ALVA.EDISONMAIN2.API.Tree", "GetTree", input);

        Assert.Contains(raw, kvp => kvp.Key != "ERR");
        var depth = 0;
        foreach (KeyValuePair<string, CVar> kvp in raw)
        {
            if (kvp.Key == "ERR") continue;
            CList row = kvp.Value;
            var tag = row.GetString(1);
            switch (tag)
            {
                case "FAMILY":
                    Assert.Equal(6, row.Count);
                    AssertFlags(row, 3, 5, 6);
                    depth++;
                    break;
                case "EXE":
                    Assert.Equal(11, row.Count);
                    AssertFlags(row, 6, 8, 10);
                    break;
                case "FAMILY_END":
                    Assert.True(--depth >= 0, $"Řádek {kvp.Key}: FAMILY_END bez otevřené FAMILY.");
                    break;
                default:
                    Assert.Fail($"Řádek {kvp.Key}: neznámý tag '{tag}', ParseTree by ho zahodil.");
                    break;
            }
        }
        Assert.Equal(0, depth);
    }

    private static void AssertFlags(CList row, params int[] positions)
    {
        foreach (var position in positions)
            Assert.Contains(row.GetString(position), new[] { "0", "1" });
    }
}
