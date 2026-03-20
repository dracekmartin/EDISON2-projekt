using ALWA.SYS;
using EDControls;
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
    public void ParseTree_EmptyOutput_ReturnsEmpty()
    {
        var tree = ModuleTreeGetter.ParseTree(new CVar());

        Assert.Empty(tree);
    }

    [Fact]
    public void ParseTree_SkipsERR()
    {
        var data = new CVar();
        data["ERR"] = new CVar("metadata");

        var tree = ModuleTreeGetter.ParseTree(data);

        Assert.Empty(tree);
    }

    [Fact]
    public void ParseTree_SingleExecutable()
    {
        var data = new CVar();
        data["1"] = new CList("EXE", "Moje Okno", "MyApp.exe", "MyForm");

        var tree = ModuleTreeGetter.ParseTree(data);

        var mod = Assert.IsType<ExecutableModule>(Assert.Single(tree));
        Assert.Equal("Moje Okno", mod.DisplayedName);
        Assert.Equal("MyApp.exe", mod.ExecutablePath);
        Assert.Equal("MyForm", mod.FormName);
    }

    [Fact]
    public void ParseTree_FamilyWithChildren()
    {
        var data = new CVar();
        data["1"] = new CList("FAMILY", "Doprava", "1");
        data["2"] = new CList("EXE", "Linky", "linky.exe", "LinkyForm");
        data["3"] = new CList("EXE", "Spoje", "spoje.exe", "SpojeForm");
        data["4"] = new CList("FAMILY_END");

        var tree = ModuleTreeGetter.ParseTree(data);

        var fam = Assert.IsType<ModuleFamily>(Assert.Single(tree));
        Assert.Equal("Doprava", fam.DisplayedName);
        Assert.True(fam.IsExpanded);
        Assert.Equal(2, fam.NestedModules.Count);
        Assert.Equal("Linky", fam.NestedModules[0].DisplayedName);
        Assert.Equal("Spoje", fam.NestedModules[1].DisplayedName);
    }

    [Fact]
    public void ParseTree_CollapsedFamily()
    {
        var data = new CVar();
        data["1"] = new CList("FAMILY", "Archiv", "0");
        data["2"] = new CList("FAMILY_END");

        var tree = ModuleTreeGetter.ParseTree(data);

        var fam = Assert.IsType<ModuleFamily>(Assert.Single(tree));
        Assert.False(fam.IsExpanded);
    }

    [Fact]
    public void ParseTree_NestedFamilies()
    {
        var data = new CVar();
        data["1"] = new CList("FAMILY", "Vnější", "1");
        data["2"] = new CList("FAMILY", "Vnitřní", "1");
        data["3"] = new CList("EXE", "App", "app.exe", "Form");
        data["4"] = new CList("FAMILY_END");
        data["5"] = new CList("FAMILY_END");

        var tree = ModuleTreeGetter.ParseTree(data);

        var outer = Assert.IsType<ModuleFamily>(Assert.Single(tree));
        Assert.Equal("Vnější", outer.DisplayedName);
        var inner = Assert.IsType<ModuleFamily>(Assert.Single(outer.NestedModules));
        Assert.Equal("Vnitřní", inner.DisplayedName);
        var exe = Assert.IsType<ExecutableModule>(Assert.Single(inner.NestedModules));
        Assert.Equal("App", exe.DisplayedName);
    }

    [Fact]
    public void ParseTree_MixedTopLevel()
    {
        var data = new CVar();
        data["1"] = new CList("EXE", "Standalone", "standalone.exe", "");
        data["2"] = new CList("FAMILY", "Skupina", "1");
        data["3"] = new CList("EXE", "Child", "child.exe", "");
        data["4"] = new CList("FAMILY_END");

        var tree = ModuleTreeGetter.ParseTree(data);

        Assert.Equal(2, tree.Count);
        Assert.IsType<ExecutableModule>(tree[0]);
        var fam = Assert.IsType<ModuleFamily>(tree[1]);
        Assert.Single(fam.NestedModules);
    }

    [Fact]
    public void ParseTree_OrphanFamilyEnd_IsIgnored()
    {
        var data = new CVar();
        data["1"] = new CList("FAMILY_END");
        data["2"] = new CList("EXE", "App", "app.exe", "Form");

        var tree = ModuleTreeGetter.ParseTree(data);

        var mod = Assert.IsType<ExecutableModule>(Assert.Single(tree));
        Assert.Equal("App", mod.DisplayedName);
    }

    [Fact]
    public void ParseTree_UnclosedFamily_KeepsChildrenInside()
    {
        var data = new CVar();
        data["1"] = new CList("FAMILY", "Open", "1");
        data["2"] = new CList("EXE", "Child", "child.exe", "Form");

        var tree = ModuleTreeGetter.ParseTree(data);

        var fam = Assert.IsType<ModuleFamily>(Assert.Single(tree));
        Assert.Equal("Open", fam.DisplayedName);
        Assert.Single(fam.NestedModules);
    }

    [Fact]
    public void ParseTree_UnknownType_IsSkipped()
    {
        var data = new CVar();
        data["1"] = new CList("UNKNOWN", "Something");
        data["2"] = new CList("EXE", "App", "app.exe", "Form");

        var tree = ModuleTreeGetter.ParseTree(data);

        var mod = Assert.IsType<ExecutableModule>(Assert.Single(tree));
        Assert.Equal("App", mod.DisplayedName);
    }

    [Fact]
    public void GetModuleTree_EndToEnd_CallsMockAndParsesResult()
    {
        var data = new CVar();
        data["1"] = new CList("FAMILY", "Doprava", "1");
        data["2"] = new CList("EXE", "Linky", "linky.exe", "LinkyForm");
        data["3"] = new CList("FAMILY_END");
        var mockMLRest = MockTreeCall(data);

        var service = new ModuleTreeGetter();
        var tree = service.GetModuleTree(mockMLRest);

        var fam = Assert.IsType<ModuleFamily>(Assert.Single(tree));
        Assert.Equal("Doprava", fam.DisplayedName);
        var exe = Assert.IsType<ExecutableModule>(Assert.Single(fam.NestedModules));
        Assert.Equal("Linky", exe.DisplayedName);
        mockMLRest.Received(1).QueryOrThrow(
            "ALVA.EDISONMAIN2.API.Tree", "GetTree", Arg.Any<CVar>());
    }
}
