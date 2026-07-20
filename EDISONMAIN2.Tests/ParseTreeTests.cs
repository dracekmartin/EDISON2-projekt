using ALWA.SYS;
using EDControls;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

public class ParseTreeTests
{
    private static IMLRest MockTreeCall(CVar returnData)
    {
        var mock = MLRestMocks.Create();
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
        // Starý 4-polový EXE řádek (bez elementId/isLicensed) — zpětná kompatibilita
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
        // Přechodný formát s elementId, ale bez isLicensed (např. částečná migrace)
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
    public void ParseTree_FamilyFullFormat_ReadsElementIdAndIsLicensed()
    {
        var data = new CVar();
        data["1"] = new CList("FAMILY", "Kmenová evidence", "0", "1", "1");
        data["2"] = new CList("FAMILY_END");

        var tree = ModuleTreeGetter.ParseTree(data);

        var fam = Assert.IsType<ModuleFamily>(Assert.Single(tree));
        Assert.Equal("1", fam.ElementId);
        Assert.True(fam.IsLicensed);
    }

    [Fact]
    public void ParseTree_FamilyLegacyFormat_DefaultsElementIdAndIsLicensed()
    {
        // Starý 3-polový FAMILY řádek (bez elementId/isLicensed) — zpětná kompatibilita.
        var data = new CVar();
        data["1"] = new CList("FAMILY", "Doprava", "1");
        data["2"] = new CList("FAMILY_END");

        var tree = ModuleTreeGetter.ParseTree(data);

        var fam = Assert.IsType<ModuleFamily>(Assert.Single(tree));
        Assert.Equal("", fam.ElementId);
        Assert.False(fam.IsLicensed);
    }

    [Fact]
    public void ParseTree_FamilyLicensedZero_IsLicensedFalse()
    {
        var data = new CVar();
        data["1"] = new CList("FAMILY", "Převody", "0", "11", "0");
        data["2"] = new CList("FAMILY_END");

        var tree = ModuleTreeGetter.ParseTree(data);

        var fam = Assert.IsType<ModuleFamily>(Assert.Single(tree));
        Assert.Equal("11", fam.ElementId);
        Assert.False(fam.IsLicensed);
        Assert.False(fam.MlineOnly);   // 5polový záznam (bez mlineOnly) → default false
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void ParseTree_FamilyMlineOnly_Parsed(string flag, bool expected)
    {
        var data = new CVar();
        data["1"] = new CList("FAMILY", "Interní", "0", "52", "1", flag);
        data["2"] = new CList("FAMILY_END");

        var tree = ModuleTreeGetter.ParseTree(data);

        var fam = Assert.IsType<ModuleFamily>(Assert.Single(tree));
        Assert.Equal(expected, fam.MlineOnly);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void ParseTree_ExeMlineOnly_Parsed(string flag, bool expected)
    {
        var data = new CVar();
        data["1"] = new CList("EXE", "Doklady OLD", "SKLADYMAIN.exe", "F", "92101", "0", "", "0", "", flag);

        var tree = ModuleTreeGetter.ParseTree(data);

        var mod = Assert.IsType<ExecutableModule>(Assert.Single(tree));
        Assert.Equal(expected, mod.MlineOnly);
    }

    [Fact]
    public void ParseTree_ExeNineFields_MlineOnlyDefaultsFalse()
    {
        // 9polový záznam (před mlineOnly) — zpětná kompatibilita.
        var data = new CVar();
        data["1"] = new CList("EXE", "X", "x.exe", "F", "1", "0", "", "0", "");

        var tree = ModuleTreeGetter.ParseTree(data);

        var mod = Assert.IsType<ExecutableModule>(Assert.Single(tree));
        Assert.False(mod.MlineOnly);
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
    public void ParseTree_RowOrderPreserved_TenPlusRows()
    {
        // CVar zachovává pořadí subscriptů z wire (Caché posílá numerické subscripty v numerickém
        // pořadí) — ParseTree je nesmí přeskládat na řetězcové řazení: "10" patří za "9", ne za "1".
        var data = new CVar();
        for (int i = 1; i <= 12; i++)
            data[i.ToString()] = new CList("EXE", "M" + i, "m" + i + ".exe", "F");

        var tree = ModuleTreeGetter.ParseTree(data);

        Assert.Equal(
            Enumerable.Range(1, 12).Select(i => "M" + i),
            tree.Select(m => m.DisplayedName));
    }

    [Fact]
    public void ParseTree_IsLicensedMinusOne_ParsesAsFalse()
    {
        // isLicensed je striktní == "1" — narozdíl od VB-style login booleanů (commit 02765e9),
        // kde -1 = true. Tady "-1" (ani jiné nenulové) NEznačí licencováno.
        var data = new CVar();
        data["1"] = new CList("EXE", "X", "x.exe", "F", "100", "-1");

        var tree = ModuleTreeGetter.ParseTree(data);

        var mod = Assert.IsType<ExecutableModule>(Assert.Single(tree));
        Assert.False(mod.IsLicensed);
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
