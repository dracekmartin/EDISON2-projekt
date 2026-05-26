using ALWA.SYS;
using EDControls;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
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
    public void ParseTree_LegacyFormat_DefaultsElementIdAndIsLicensed()
    {
        // Old 4-field EXE row (without elementId/isLicensed) — backwards compat
        var data = new CVar();
        data["1"] = new CList("EXE", "Legacy", "legacy.exe", "LegacyForm");

        var tree = ModuleTreeGetter.ParseTree(data);

        var mod = Assert.IsType<ExecutableModule>(Assert.Single(tree));
        Assert.Equal("", mod.ElementId);
        Assert.False(mod.IsLicensed);
    }

    [Fact]
    public void ParseTree_FullFormat_ReadsElementIdAndIsLicensed()
    {
        var data = new CVar();
        data["1"] = new CList("EXE", "Plán dopravy", "MHD2PlanDopravy.exe",
                              "ALVA.MHD2.PlanDopravy.formMHD2PlanDopravyMain", "260001", "1");

        var tree = ModuleTreeGetter.ParseTree(data);

        var mod = Assert.IsType<ExecutableModule>(Assert.Single(tree));
        Assert.Equal("260001", mod.ElementId);
        Assert.True(mod.IsLicensed);
    }

    [Fact]
    public void ParseTree_FullFormatLicensedZero_IsLicensedFalse()
    {
        var data = new CVar();
        data["1"] = new CList("EXE", "Příplatky", "MHD2Ciselniky.exe",
                              "ALVA.MHD2.Ciselniky.formMHD2CiselnikyPriplatky", "210024", "0");

        var tree = ModuleTreeGetter.ParseTree(data);

        var mod = Assert.IsType<ExecutableModule>(Assert.Single(tree));
        Assert.Equal("210024", mod.ElementId);
        Assert.False(mod.IsLicensed);
    }

    [Fact]
    public void ParseTree_FiveFields_OnlyElementIdReadIsLicensedDefault()
    {
        // Intermediate format with elementId but no isLicensed (e.g. partial migration)
        var data = new CVar();
        data["1"] = new CList("EXE", "Mid", "mid.exe", "MidForm", "999");

        var tree = ModuleTreeGetter.ParseTree(data);

        var mod = Assert.IsType<ExecutableModule>(Assert.Single(tree));
        Assert.Equal("999", mod.ElementId);
        Assert.False(mod.IsLicensed);
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
    public void ParseTree_FullFormatWithInputParamHiddenColor()
    {
        var data = new CVar();
        data["1"] = new CList("EXE", "Úlohy", "EDISONMAIN.exe", "ALVA.EDISONMAIN.formTaskList",
                              "2011", "0", "AdminMode=1", "0", "#FFA500");

        var tree = ModuleTreeGetter.ParseTree(data);

        var mod = Assert.IsType<ExecutableModule>(Assert.Single(tree));
        Assert.Equal("AdminMode=1", mod.InputParam);
        Assert.False(mod.Hidden);
        Assert.Equal("#FFA500", mod.Color);
    }

    [Fact]
    public void ParseTree_HiddenOne_SetsHiddenTrue()
    {
        var data = new CVar();
        data["1"] = new CList("EXE", "Skrytý", "x.exe", "X", "1", "0", "", "1");

        var tree = ModuleTreeGetter.ParseTree(data);

        var mod = Assert.IsType<ExecutableModule>(Assert.Single(tree));
        Assert.True(mod.Hidden);
    }

    [Fact]
    public void ParseTree_LegacyFormat_HiddenAndColorDefault()
    {
        // 7-field row (před S8/S9) — Hidden/Color musí default na false / "".
        var data = new CVar();
        data["1"] = new CList("EXE", "Legacy", "l.exe", "L", "1", "1", "input");

        var tree = ModuleTreeGetter.ParseTree(data);

        var mod = Assert.IsType<ExecutableModule>(Assert.Single(tree));
        Assert.False(mod.Hidden);
        Assert.Equal("", mod.Color);
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

    [Fact]
    public void GetModuleTree_DefaultParam_SendsMainAsTreeName()
    {
        var mockMLRest = MockTreeCall(new CVar());

        new ModuleTreeGetter().GetModuleTree(mockMLRest);

        mockMLRest.Received(1).QueryOrThrow(
            "ALVA.EDISONMAIN2.API.Tree", "GetTree",
            Arg.Is<CVar>(c => c["tree"].Value!.ToString() == "main"));
    }

    [Theory]
    [InlineData("main")]
    [InlineData("system")]
    [InlineData("partners")]
    public void GetModuleTree_TreeNameParam_ForwardedAsCVarInput(string treeName)
    {
        var mockMLRest = MockTreeCall(new CVar());

        new ModuleTreeGetter().GetModuleTree(mockMLRest, treeName);

        mockMLRest.Received(1).QueryOrThrow(
            "ALVA.EDISONMAIN2.API.Tree", "GetTree",
            Arg.Is<CVar>(c => c["tree"].Value!.ToString() == treeName));
    }
}
