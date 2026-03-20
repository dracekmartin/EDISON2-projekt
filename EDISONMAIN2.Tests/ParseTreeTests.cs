using ALWA.SYS;
using EDControls;
using EDISONMAIN2.Services;
using EDISONMAIN2.Services.Data;
using EDISONMAIN2.Services.Modules;
using NSubstitute;

namespace EDISONMAIN2.Tests;

public class ParseTreeTests
{
    private static IMLRest MockTreeCall(CVar returnData)
    {
        var mock = Substitute.For<IMLRest>();
        mock.QueryOrThrow("ALVA.EDISONMAIN2.API.Tree", "GetTree", Arg.Any<CVar>())
            .Returns(returnData);
        return mock;
    }

    [Fact]
    public void GetTree_EmptyOutput_ReturnsEmpty()
    {
        var mlRest = MockTreeCall(new CVar());

        var treeData = mlRest.QueryOrThrow("ALVA.EDISONMAIN2.API.Tree", "GetTree", new CVar());
        var tree = EdisonMainDataService.ParseTree(treeData);

        Assert.Empty(tree);
    }

    [Fact]
    public void GetTree_SkipsERR()
    {
        var data = new CVar();
        data["ERR"] = new CVar("metadata");
        var mlRest = MockTreeCall(data);

        var treeData = mlRest.QueryOrThrow("ALVA.EDISONMAIN2.API.Tree", "GetTree", new CVar());
        var tree = EdisonMainDataService.ParseTree(treeData);

        Assert.Empty(tree);
    }

    [Fact]
    public void GetTree_SingleExecutable()
    {
        var data = new CVar();
        data["1"] = new CList("EXE", "Moje Okno", "MyApp.exe", "MyForm");
        var mlRest = MockTreeCall(data);

        var treeData = mlRest.QueryOrThrow("ALVA.EDISONMAIN2.API.Tree", "GetTree", new CVar());
        var tree = EdisonMainDataService.ParseTree(treeData);

        var mod = Assert.IsType<ExecutableModule>(Assert.Single(tree));
        Assert.Equal("Moje Okno", mod.DisplayedName);
        Assert.Equal("MyApp.exe", mod.ExecutablePath);
        Assert.Equal("MyForm", mod.FormName);
    }

    [Fact]
    public void GetTree_FamilyWithChildren()
    {
        var data = new CVar();
        data["1"] = new CList("FAMILY", "Doprava", "1");
        data["2"] = new CList("EXE", "Linky", "linky.exe", "LinkyForm");
        data["3"] = new CList("EXE", "Spoje", "spoje.exe", "SpojeForm");
        data["4"] = new CList("FAMILY_END");
        var mlRest = MockTreeCall(data);

        var treeData = mlRest.QueryOrThrow("ALVA.EDISONMAIN2.API.Tree", "GetTree", new CVar());
        var tree = EdisonMainDataService.ParseTree(treeData);

        var fam = Assert.IsType<ModuleFamily>(Assert.Single(tree));
        Assert.Equal("Doprava", fam.DisplayedName);
        Assert.True(fam.IsExpanded);
        Assert.Equal(2, fam.NestedModules.Count);
        Assert.Equal("Linky", fam.NestedModules[0].DisplayedName);
        Assert.Equal("Spoje", fam.NestedModules[1].DisplayedName);
    }

    [Fact]
    public void GetTree_CollapsedFamily()
    {
        var data = new CVar();
        data["1"] = new CList("FAMILY", "Archiv", "0");
        data["2"] = new CList("FAMILY_END");
        var mlRest = MockTreeCall(data);

        var treeData = mlRest.QueryOrThrow("ALVA.EDISONMAIN2.API.Tree", "GetTree", new CVar());
        var tree = EdisonMainDataService.ParseTree(treeData);

        var fam = Assert.IsType<ModuleFamily>(Assert.Single(tree));
        Assert.False(fam.IsExpanded);
    }

    [Fact]
    public void GetTree_NestedFamilies()
    {
        var data = new CVar();
        data["1"] = new CList("FAMILY", "Vnější", "1");
        data["2"] = new CList("FAMILY", "Vnitřní", "1");
        data["3"] = new CList("EXE", "App", "app.exe", "Form");
        data["4"] = new CList("FAMILY_END");
        data["5"] = new CList("FAMILY_END");
        var mlRest = MockTreeCall(data);

        var treeData = mlRest.QueryOrThrow("ALVA.EDISONMAIN2.API.Tree", "GetTree", new CVar());
        var tree = EdisonMainDataService.ParseTree(treeData);

        var outer = Assert.IsType<ModuleFamily>(Assert.Single(tree));
        Assert.Equal("Vnější", outer.DisplayedName);
        var inner = Assert.IsType<ModuleFamily>(Assert.Single(outer.NestedModules));
        Assert.Equal("Vnitřní", inner.DisplayedName);
        var exe = Assert.IsType<ExecutableModule>(Assert.Single(inner.NestedModules));
        Assert.Equal("App", exe.DisplayedName);
    }

    [Fact]
    public void GetTree_MixedTopLevel()
    {
        var data = new CVar();
        data["1"] = new CList("EXE", "Standalone", "standalone.exe", "");
        data["2"] = new CList("FAMILY", "Skupina", "1");
        data["3"] = new CList("EXE", "Child", "child.exe", "");
        data["4"] = new CList("FAMILY_END");
        var mlRest = MockTreeCall(data);

        var treeData = mlRest.QueryOrThrow("ALVA.EDISONMAIN2.API.Tree", "GetTree", new CVar());
        var tree = EdisonMainDataService.ParseTree(treeData);

        Assert.Equal(2, tree.Count);
        Assert.IsType<ExecutableModule>(tree[0]);
        var fam = Assert.IsType<ModuleFamily>(tree[1]);
        Assert.Single(fam.NestedModules);
    }

    [Fact]
    public void GetModuleTree_EndToEnd_CallsMockAndParsesResult()
    {
        var data = new CVar();
        data["1"] = new CList("FAMILY", "Doprava", "1");
        data["2"] = new CList("EXE", "Linky", "linky.exe", "LinkyForm");
        data["3"] = new CList("FAMILY_END");
        var mockMLRest = MockTreeCall(data);

        var connParams = new ConnectionParams("localhost", 0, "", "", 0);
        var service = new EdisonMainDataService(connParams);
        var tree = service.GetModuleTree(mockMLRest);

        var fam = Assert.IsType<ModuleFamily>(Assert.Single(tree));
        Assert.Equal("Doprava", fam.DisplayedName);
        var exe = Assert.IsType<ExecutableModule>(Assert.Single(fam.NestedModules));
        Assert.Equal("Linky", exe.DisplayedName);
        mockMLRest.Received(1).QueryOrThrow(
            "ALVA.EDISONMAIN2.API.Tree", "GetTree", Arg.Any<CVar>());
    }
}
