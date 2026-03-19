using ALWA.SYS;
using EDISONMAIN2.Services.Data;
using EDISONMAIN2.Services.Modules;

namespace EDISONMAIN2.Tests;

public class ParseTreeTests
{
    [Fact]
    public void ParseTree_EmptyOutput_ReturnsEmpty()
    {
        var data = new CVar();
        var tree = EdisonMainDataService.ParseTree(data);
        Assert.Empty(tree);
    }

    [Fact]
    public void ParseTree_SkipsERR()
    {
        var data = new CVar();
        data["ERR"] = new CVar("metadata");
        var tree = EdisonMainDataService.ParseTree(data);
        Assert.Empty(tree);
    }

    [Fact]
    public void ParseTree_SingleExecutable()
    {
        var data = new CVar();
        data["1"] = new CList("EXE", "Moje Okno", "MyApp.exe", "MyForm");

        var tree = EdisonMainDataService.ParseTree(data);

        var exe = Assert.Single(tree);
        var mod = Assert.IsType<ExecutableModule>(exe);
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

        var tree = EdisonMainDataService.ParseTree(data);

        var family = Assert.Single(tree);
        var fam = Assert.IsType<ModuleFamily>(family);
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

        var tree = EdisonMainDataService.ParseTree(data);

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

        var tree = EdisonMainDataService.ParseTree(data);

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

        var tree = EdisonMainDataService.ParseTree(data);

        Assert.Equal(2, tree.Count);
        Assert.IsType<ExecutableModule>(tree[0]);
        Assert.IsType<ModuleFamily>(tree[1]);
        var fam = (ModuleFamily)tree[1];
        Assert.Single(fam.NestedModules);
    }
}
