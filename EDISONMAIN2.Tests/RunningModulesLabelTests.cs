using EDControls.EDViewModel;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>
/// HD 13132/6: menu „Spuštěné moduly" značilo položky vlastním titulkem legacy okna, takže dvě
/// různé agendy se shodným názvem (Zaměstnanci z MZDY2 vs. z Požadavků na volno) nešlo rozlišit
/// a `[IDFa]` v položce byl navíc zbytečný (okna jsou vždy z aktuální firmy). Spuštěné i Používané
/// proto nesou jeden popisek ze stromu: dlouhý název, je-li jedinečný, jinak krátký, je-li
/// jedinečný, jinak cesta stromem.
/// </summary>
public class RunningModulesLabelTests
{
    private static ExecutableModule Exe(string name, string exe, string form, string input = "", string longName = "", string family = "") =>
        new() { Name = name, ExecutablePath = exe, FormName = form, InputParam = input, LongName = longName, FamilyPath = family };

    // Cestu agendou skládá loader do FamilyPath (ParseTreeTests); tady ji Fam jen předá listům, které ji nemají.
    private static ModuleFamily Fam(string name, params ExecutableModule[] kids)
    {
        var f = new ModuleFamily { Name = name };
        foreach (var k in kids) f.NestedModules.Add(k.FamilyPath.Length > 0 ? k : new ExecutableModule(k) { FamilyPath = name });
        return f;
    }

    private static string Label(Dictionary<string, string> labels, string exe, string form, string input = "") =>
        labels[ModuleTreeState.ModuleKey(exe, form, input)];

    private static RunningModuleInfo Running(string title, string exe = "", string form = "", string input = "") =>
        new(title, () => { }) { ExeName = exe, FormFqn = form, InputParam = input };

    [Fact]
    public void BuildModuleLabels_UniqueShortName_IsShortName()
    {
        var labels = ModuleTreeState.BuildModuleLabels(
            [[Fam("Mzdy a personalistika", Exe("Zaměstnanci", "MZDY2.exe", "ALVA.MZDY2.formZam"))]]);

        Assert.Equal("Zaměstnanci", Assert.Single(labels).Value);
    }

    [Fact]
    public void BuildModuleLabels_UniqueLongName_Wins()
    {
        var labels = ModuleTreeState.BuildModuleLabels([[
            Fam("Zpracování dopravy", Exe("Fakturace", "FAKT.exe", "f", longName: "Fakturace - zpracování AD")),
        ]]);

        Assert.Equal("Fakturace - zpracování AD", Assert.Single(labels).Value);
    }

    // Celá cesta i s mezilehlou rodinou, ne jen agenda.
    [Fact]
    public void BuildModuleLabels_SameShortNameDifferentAgenda_UsesTreePath()
    {
        var labels = ModuleTreeState.BuildModuleLabels([[
            Fam("Mzdy a personalistika",
                Exe("Zaměstnanci", "MZDY2.exe", "ALVA.MZDY2.formZam", family: "Mzdy a personalistika › Pracovní poměr")),
            Fam("Požadavky na volno", Exe("Zaměstnanci", "VOLNO.exe", "ALVA.VOLNO.formZam")),
        ]]);

        Assert.Equal("Mzdy a personalistika › Pracovní poměr › Zaměstnanci", Label(labels, "MZDY2.exe", "ALVA.MZDY2.formZam"));
        Assert.Equal("Požadavky na volno › Zaměstnanci", Label(labels, "VOLNO.exe", "ALVA.VOLNO.formZam"));
    }

    // Ve stromu vidí uživatel krátké názvy - „Doklady" je nejednoznačné, i když druhý modul má dlouhý název.
    [Fact]
    public void BuildModuleLabels_ShortNameSharedWithLongNamedModule_StillAmbiguous()
    {
        var labels = ModuleTreeState.BuildModuleLabels([[
            Fam("Sklady",    Exe("Doklady", "SKLAD.exe", "f")),
            Fam("Fakturace", Exe("Doklady", "FAKT.exe", "f", longName: "Doklady fakturace")),
        ]]);

        Assert.Equal("Sklady › Doklady", Label(labels, "SKLAD.exe", "f"));
        Assert.Equal("Doklady fakturace", Label(labels, "FAKT.exe", "f"));
    }

    // Legacy dlouhé názvy se opakují (ISYBUS Jízdní řády u nové i obsolete verze) - tehdy rozliší krátký.
    [Fact]
    public void BuildModuleLabels_DuplicateLongName_FallsBackToUniqueShortName()
    {
        var labels = ModuleTreeState.BuildModuleLabels([[
            Fam("ISYBUS",
                Exe("Jízdní řády (nové)", "ISYJR2.exe", "f", longName: "ISYBUS Jízdní řády"),
                Exe("Jízdní řády (obsolete)", "ISYJR.exe", "f", longName: "ISYBUS Jízdní řády")),
        ]]);

        Assert.Equal("Jízdní řády (nové)", Label(labels, "ISYJR2.exe", "f"));
        Assert.Equal("Jízdní řády (obsolete)", Label(labels, "ISYJR.exe", "f"));
    }

    // Číselné řady visí v osmi agendách jako týž modul (a znovu v Oblíbených) - nejednoznačnost to není.
    [Fact]
    public void BuildModuleLabels_SameModuleInSeveralPlaces_CountsOnce()
    {
        var labels = ModuleTreeState.BuildModuleLabels([
            [Fam("Sklady", Exe("Číselné řady", "ESUTILITY.exe", "f")), Fam("Fakturace", Exe("Číselné řady", "ESUTILITY.exe", "f"))],
            [Fam("Oblíbené", Exe("Číselné řady", "ESUTILITY.exe", "f"))],
        ]);

        Assert.Equal("Číselné řady", Assert.Single(labels).Value);
    }

    // InputParam je součást identity - jedna exe+forma se dvěma parametry jsou dva moduly.
    [Fact]
    public void BuildModuleLabels_InputParamDistinguishesModules()
    {
        var labels = ModuleTreeState.BuildModuleLabels([[
            Fam("Doprava",
                Exe("Plánování", "DS.exe", "f"),
                Exe("Plánování (admin)", "DS.exe", "f", "AdminMode=1")),
        ]]);

        Assert.Equal(2, labels.Count);
        Assert.Equal("Plánování (admin)", Label(labels, "DS.exe", "f", "AdminMode=1"));
    }

    // Dekorace (mlineOnly, launchovatelnost) patří k zobrazení listu, do popisku ne.
    [Fact]
    public void BuildModuleLabels_IgnoresDisplaySuffixes()
    {
        var fam = Fam("Interní", Exe("Turnusy", "MHD2.exe", "f"));
        fam.NestedModules[0] = new ExecutableModule((ExecutableModule)fam.NestedModules[0]) { DisplaySuffix = " - chybí exe" };

        Assert.Equal("Turnusy", Assert.Single(ModuleTreeState.BuildModuleLabels([[fam]])).Value);
    }

    // Strom ořezaný licencí může dlouhý název jednoho modulu srazit s krátkým jiného - pak rozhodne cesta.
    [Fact]
    public void BuildModuleLabels_LongNameEqualToOtherShortName_BothGetTreePath()
    {
        var labels = ModuleTreeState.BuildModuleLabels([[
            Fam("Autobusová doprava", Exe("Turnusy AD", "ADTURNUSY.exe", "f", longName: "Turnusy")),
            Fam("MHD", Exe("Turnusy", "MHDTURNUSY.exe", "f")),
        ]]);

        Assert.Equal("Autobusová doprava › Turnusy AD", Label(labels, "ADTURNUSY.exe", "f"));
        Assert.Equal("MHD › Turnusy", Label(labels, "MHDTURNUSY.exe", "f"));
    }

    // Shodnou cestu (týž název v téže rodině s jiným parametrem, kořen ve dvou záložkách) rozliší pořadí ve stromu.
    [Fact]
    public void BuildModuleLabels_SameTreePath_NumberedInTreeOrder()
    {
        var labels = ModuleTreeState.BuildModuleLabels([
            [Fam("Jízdní řády", Exe("Návrhář sestav", "TISKJR.exe", "f", "Agenda=AD"), Exe("Návrhář sestav", "TISKJR.exe", "f", "Agenda=MHD"))],
            [Exe("Nastavení", "SET.exe", "f")],
            [Exe("Nastavení", "PARTNER.exe", "f")],
        ]);

        Assert.Equal("Jízdní řády › Návrhář sestav", Label(labels, "TISKJR.exe", "f", "Agenda=AD"));
        Assert.Equal("Jízdní řády › Návrhář sestav (2)", Label(labels, "TISKJR.exe", "f", "Agenda=MHD"));
        Assert.Equal("Nastavení", Label(labels, "SET.exe", "f"));
        Assert.Equal("Nastavení (2)", Label(labels, "PARTNER.exe", "f"));
    }

    // Pořadové číslo nesmí trefit název jiného modulu.
    [Fact]
    public void BuildModuleLabels_Numbering_SkipsTakenLabel()
    {
        var labels = ModuleTreeState.BuildModuleLabels([
            [Exe("Nastavení", "SET.exe", "f"), Fam("Doprava", Exe("Nastavení (2)", "X.exe", "f"))],
            [Exe("Nastavení", "PARTNER.exe", "f")],
        ]);

        Assert.Equal("Nastavení", Label(labels, "SET.exe", "f"));
        Assert.Equal("Nastavení (2)", Label(labels, "X.exe", "f"));
        Assert.Equal("Nastavení (3)", Label(labels, "PARTNER.exe", "f"));
    }

    // Cesta převedeného modulu (v kořeni = jen název) se kryje s nekolidujícím popiskem jiného - číslo
    // dostane převedený modul, i když je ve stromu dřív.
    [Fact]
    public void BuildModuleLabels_EscalatedPathEqualToOtherLabel_EscalatedOneNumbered()
    {
        var labels = ModuleTreeState.BuildModuleLabels([[
            Exe("Turnusy", "A.exe", "f", longName: "Turnusy AD"),
            Fam("Autobusová doprava", Exe("Turnusy AD", "B.exe", "f")),
            Fam("MHD", Exe("Plán", "C.exe", "f", longName: "Turnusy")),
        ]]);

        Assert.Equal("Turnusy", Label(labels, "C.exe", "f"));
        Assert.Equal("Turnusy (2)", Label(labels, "A.exe", "f"));
        Assert.Equal("Autobusová doprava › Turnusy AD", Label(labels, "B.exe", "f"));
    }

    // Stub na dlaždici ukazuje svou cestu - modul se stejným popiskem musí ustoupit.
    [Fact]
    public void BuildModuleLabels_LabelEqualToStubPath_Escalates()
    {
        var labels = ModuleTreeState.BuildModuleLabels([[
            Exe("EDISON AI", "", ""),
            Fam("Nástroje", Exe("Asistent", "AI.exe", "f", longName: "EDISON AI")),
        ]]);

        Assert.Equal("Nástroje › Asistent", Assert.Single(labels).Value);
    }

    // Stub (EDISON AI) má klíč „||" jako každé WPF okno - popisek by dostala všechna okna shellu.
    // Ve stromu ho ale uživatel vidí, takže jeho název jedinečnost ostatních ruší dál.
    [Fact]
    public void BuildModuleLabels_ModuleWithoutExe_NotLabelledButCounted()
    {
        var labels = ModuleTreeState.BuildModuleLabels([[
            Fam("Kmenová evidence", Exe("EDISON AI", "", ""), Exe("Události", "", "")),
            Fam("Sklady", Exe("Události", "SKLAD.exe", "f")),
        ]]);

        Assert.Equal("Sklady › Události", Assert.Single(labels).Value);
        Assert.Equal("Uživatelský profil", EdisonMainViewModel.ResolveRunningLabel(Running("Uživatelský profil"), labels));
    }

    // Jedinečnost se počítá přes všechny záložky a popisek dostanou modul i menu Spuštěných z téže mapy.
    [Fact]
    public void Load_AssignsLabelsAcrossTabs_ToModulesAndMap()
    {
        var state = new ModuleTreeState(() => null, EdisonMainViewModelHarness.NeutralStation);

        state.Load(new Dictionary<string, IReadOnlyList<IModule>>
        {
            ["main"] = [Fam("Sklady", Exe("Události", "SKLAD.exe", "f"))],
            ["system"] = [Fam("EDISON", Exe("Události", "UDALOSTI.exe", "f"))],
            ["partners"] = [],
        }, licence: null, access: null, expansion: null);

        var main = (ExecutableModule)((ModuleFamily)state.MainMenuTree[0]).NestedModules[0];
        var system = (ExecutableModule)((ModuleFamily)state.SystemAdminTree[0]).NestedModules[0];
        Assert.Equal("Sklady › Události", main.DistinctName);
        Assert.Equal("EDISON › Události", system.DistinctName);
        Assert.Equal(main.DistinctName, state.ModuleLabels[ModuleTreeState.ModuleKey("SKLAD.exe", "f", "")]);
    }

    [Theory]
    [InlineData("Zaměstnanci [CKSCHWAN]", "Zaměstnanci")]
    [InlineData("Zaměstnanci [CK_SCHWAN]", "Zaměstnanci")]
    [InlineData("Zaměstnanci", "Zaměstnanci")]
    [InlineData("", "")]
    [InlineData("Sestava [2026] za rok [CK_DP]", "Sestava [2026] za rok")]   // jen koncový suffix
    [InlineData("[CK_DP]", "[CK_DP]")]                                       // celý název = závorka → nechat
    public void StripIdfaSuffix_RemovesTrailingBracketOnly(string title, string expected) =>
        Assert.Equal(expected, EdisonMainViewModel.StripIdfaSuffix(title));

    [Fact]
    public void ResolveRunningLabel_ModuleInTree_UsesTreeLabel()
    {
        var labels = ModuleTreeState.BuildModuleLabels([[
            Fam("Mzdy a personalistika", Exe("Zaměstnanci", "MZDY2.exe", "ALVA.MZDY2.formZam")),
            Fam("Požadavky na volno", Exe("Zaměstnanci", "VOLNO.exe", "ALVA.VOLNO.formZam")),
        ]]);

        var label = EdisonMainViewModel.ResolveRunningLabel(
            Running("Zaměstnanci [CKSCHWAN]", "MZDY2.exe", "ALVA.MZDY2.formZam"), labels);

        Assert.Equal("Mzdy a personalistika › Zaměstnanci", label);
    }

    [Fact]
    public void ResolveRunningLabel_UnknownModule_FallsBackToTitleWithoutIdfa()
    {
        var label = EdisonMainViewModel.ResolveRunningLabel(
            Running("Něco jiného [CK_SCHWAN]", "X.exe", "ALVA.X.form"), NoLabels);

        Assert.Equal("Něco jiného", label);
    }

    // WPF okna shellu nemají exe ani formu ani suffix s firmou - titulek jde do menu doslova.
    [Fact]
    public void ResolveRunningLabel_WpfWindow_UsesTitleVerbatim()
    {
        var label = EdisonMainViewModel.ResolveRunningLabel(Running("Uživatelský profil"), NoLabels);

        Assert.Equal("Uživatelský profil", label);
    }

    private static readonly Dictionary<string, string> NoLabels = [];
}
