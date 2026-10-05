using EDControls.EDViewModel;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>
/// HD 13132/6: menu „Spuštěné moduly" značilo položky vlastním titulkem legacy okna, takže dvě
/// různé agendy se shodným názvem (Zaměstnanci z MZDY2 vs. z Požadavků na volno) nešlo rozlišit
/// a `[IDFa]` v položce byl navíc zbytečný (okna jsou vždy z aktuální firmy).
/// </summary>
public class RunningModulesLabelTests
{
    private static ExecutableModule Exe(string name, string exe, string form, string input = "", string longName = "") =>
        new() { DisplayedName = name, ExecutablePath = exe, FormName = form, InputParam = input, LongName = longName };

    private static ModuleFamily Fam(string name, params IModule[] kids)
    {
        var f = new ModuleFamily { DisplayedName = name };
        foreach (var k in kids) f.NestedModules.Add(k);
        return f;
    }

    private static RunningModuleInfo Running(string title, string exe = "", string form = "", string input = "") =>
        new(title, () => { }) { ExeName = exe, FormFqn = form, InputParam = input };

    [Fact]
    public void BuildModuleLabels_PrefixesModuleWithItsFamily()
    {
        var labels = ModuleTreeState.BuildModuleLabels(
            [[Fam("Mzdy a personalistika", Exe("Zaměstnanci", "MZDY2.exe", "ALVA.MZDY2.formZam"))]]);

        Assert.Equal("Mzdy a personalistika › Zaměstnanci", Assert.Single(labels).Value.Path);
    }

    [Fact]
    public void BuildModuleLabels_JoinsNestedFamilies()
    {
        var labels = ModuleTreeState.BuildModuleLabels(
            [[Fam("MHD", Fam("Plánování", Exe("Turnusy", "MHD2.exe", "f")))]]);

        Assert.Equal("MHD › Plánování › Turnusy", Assert.Single(labels).Value.Path);
    }

    [Fact]
    public void BuildModuleLabels_RootLevelModule_HasNoPrefix()
    {
        var labels = ModuleTreeState.BuildModuleLabels([[Exe("Nastavení", "SET.exe", "f")]]);

        Assert.Equal("Nastavení", Assert.Single(labels).Value.Path);
    }

    [Fact]
    public void BuildModuleLabels_SameNameDifferentAgenda_KeptApart()
    {
        var labels = ModuleTreeState.BuildModuleLabels([[
            Fam("Mzdy a personalistika", Exe("Zaměstnanci", "MZDY2.exe", "ALVA.MZDY2.formZam")),
            Fam("Zaměstnanci",           Exe("Zaměstnanci", "VOLNO.exe", "ALVA.VOLNO.formZam")),
        ]]);

        Assert.Equal(2, labels.Count);
        Assert.Contains("Mzdy a personalistika › Zaměstnanci", labels.Values.Select(l => l.Path));
        Assert.Contains("Zaměstnanci › Zaměstnanci", labels.Values.Select(l => l.Path));
    }

    // Týž modul figuruje ve víc stromech (a v Oblíbených) - duplicitní klíč nesmí shodit build mapy.
    [Fact]
    public void BuildModuleLabels_DuplicateModule_FirstWins()
    {
        var labels = ModuleTreeState.BuildModuleLabels([
            [Fam("Hlavní nabídka", Exe("Turnusy", "MHD2.exe", "f"))],
            [Fam("Oblíbené",       Exe("Turnusy", "MHD2.exe", "f"))],
        ]);

        Assert.Equal("Hlavní nabídka › Turnusy", Assert.Single(labels).Value.Path);
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
    }

    // Dlouhý název (legacy nameEx) se nese vedle cesty - obojí najednou, jeden průchod stromem.
    [Fact]
    public void BuildModuleLabels_CarriesLongNameNextToPath()
    {
        var labels = ModuleTreeState.BuildModuleLabels([[
            Fam("Zpracování dopravy", Exe("Fakturace", "FAKT.exe", "f", longName: "Fakturace - zpracování AD")),
        ]]);

        var label = Assert.Single(labels).Value;
        Assert.Equal("Zpracování dopravy › Fakturace", label.Path);
        Assert.Equal("Fakturace - zpracování AD", label.LongName);
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

    // Dlouhý název má přednost před cestou - je kratší a disambiguuje sám o sobě.
    [Fact]
    public void ResolveRunningLabel_ModuleWithLongName_UsesLongName()
    {
        var labels = ModuleTreeState.BuildModuleLabels([[
            Fam("Zpracování dopravy", Exe("Fakturace", "FAKT.exe", "f", longName: "Fakturace - zpracování AD")),
        ]]);

        var label = EdisonMainViewModel.ResolveRunningLabel(Running("Fakturace [CK_DP]", "FAKT.exe", "f"), labels);

        Assert.Equal("Fakturace - zpracování AD", label);
    }

    // Dlouhý název má v legacy datech jen pětina modulů - zbytek drží rozlišení cestou.
    [Fact]
    public void ResolveRunningLabel_ModuleWithoutLongName_UsesAgendaPath()
    {
        var labels = ModuleTreeState.BuildModuleLabels(
            [[Fam("Mzdy a personalistika", Exe("Zaměstnanci", "MZDY2.exe", "ALVA.MZDY2.formZam"))]]);

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

    private static readonly Dictionary<string, ModuleTreeState.ModuleLabel> NoLabels = [];
}
