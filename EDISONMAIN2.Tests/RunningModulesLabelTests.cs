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
    private static ExecutableModule Exe(string name, string exe, string form, string input = "") =>
        new() { DisplayedName = name, ExecutablePath = exe, FormName = form, InputParam = input };

    private static ModuleFamily Fam(string name, params IModule[] kids)
    {
        var f = new ModuleFamily { DisplayedName = name };
        foreach (var k in kids) f.NestedModules.Add(k);
        return f;
    }

    private static RunningModuleInfo Running(string title, string exe = "", string form = "", string input = "") =>
        new(title, () => { }) { ExeName = exe, FormFqn = form, InputParam = input };

    // ---------- BuildModulePaths ----------

    [Fact]
    public void BuildModulePaths_PrefixesModuleWithItsFamily()
    {
        var paths = EdisonMainViewModel.BuildModulePaths(
            [[Fam("Mzdy a personalistika", Exe("Zaměstnanci", "MZDY2.exe", "ALVA.MZDY2.formZam"))]]);

        Assert.Equal("Mzdy a personalistika › Zaměstnanci", Assert.Single(paths).Value);
    }

    [Fact]
    public void BuildModulePaths_JoinsNestedFamilies()
    {
        var paths = EdisonMainViewModel.BuildModulePaths(
            [[Fam("MHD", Fam("Plánování", Exe("Turnusy", "MHD2.exe", "f")))]]);

        Assert.Equal("MHD › Plánování › Turnusy", Assert.Single(paths).Value);
    }

    [Fact]
    public void BuildModulePaths_RootLevelModule_HasNoPrefix()
    {
        var paths = EdisonMainViewModel.BuildModulePaths([[Exe("Nastavení", "SET.exe", "f")]]);

        Assert.Equal("Nastavení", Assert.Single(paths).Value);
    }

    [Fact]
    public void BuildModulePaths_SameNameDifferentAgenda_KeptApart()
    {
        var paths = EdisonMainViewModel.BuildModulePaths([[
            Fam("Mzdy a personalistika", Exe("Zaměstnanci", "MZDY2.exe", "ALVA.MZDY2.formZam")),
            Fam("Zaměstnanci",           Exe("Zaměstnanci", "VOLNO.exe", "ALVA.VOLNO.formZam")),
        ]]);

        Assert.Equal(2, paths.Count);
        Assert.Contains("Mzdy a personalistika › Zaměstnanci", paths.Values);
        Assert.Contains("Zaměstnanci › Zaměstnanci", paths.Values);
    }

    // Týž modul figuruje ve víc stromech (a v Oblíbených) - duplicitní klíč nesmí shodit build mapy.
    [Fact]
    public void BuildModulePaths_DuplicateModule_FirstWins()
    {
        var paths = EdisonMainViewModel.BuildModulePaths([
            [Fam("Hlavní nabídka", Exe("Turnusy", "MHD2.exe", "f"))],
            [Fam("Oblíbené",       Exe("Turnusy", "MHD2.exe", "f"))],
        ]);

        Assert.Equal("Hlavní nabídka › Turnusy", Assert.Single(paths).Value);
    }

    // InputParam je součást identity - jedna exe+forma se dvěma parametry jsou dva moduly.
    [Fact]
    public void BuildModulePaths_InputParamDistinguishesModules()
    {
        var paths = EdisonMainViewModel.BuildModulePaths([[
            Fam("Doprava",
                Exe("Plánování", "DS.exe", "f"),
                Exe("Plánování (admin)", "DS.exe", "f", "AdminMode=1")),
        ]]);

        Assert.Equal(2, paths.Count);
    }

    // ---------- StripIdfaSuffix ----------

    [Theory]
    [InlineData("Zaměstnanci [CKSCHWAN]", "Zaměstnanci")]
    [InlineData("Zaměstnanci [CK_SCHWAN]", "Zaměstnanci")]
    [InlineData("Zaměstnanci", "Zaměstnanci")]
    [InlineData("", "")]
    [InlineData("Sestava [2026] za rok [CK_DP]", "Sestava [2026] za rok")]   // jen koncový suffix
    [InlineData("[CK_DP]", "[CK_DP]")]                                       // celý název = závorka → nechat
    public void StripIdfaSuffix_RemovesTrailingBracketOnly(string title, string expected) =>
        Assert.Equal(expected, EdisonMainViewModel.StripIdfaSuffix(title));

    // ---------- ResolveRunningLabel ----------

    [Fact]
    public void ResolveRunningLabel_KnownModule_UsesAgendaPath()
    {
        var paths = EdisonMainViewModel.BuildModulePaths(
            [[Fam("Mzdy a personalistika", Exe("Zaměstnanci", "MZDY2.exe", "ALVA.MZDY2.formZam"))]]);

        var label = EdisonMainViewModel.ResolveRunningLabel(
            Running("Zaměstnanci [CKSCHWAN]", "MZDY2.exe", "ALVA.MZDY2.formZam"), paths);

        Assert.Equal("Mzdy a personalistika › Zaměstnanci", label);
    }

    [Fact]
    public void ResolveRunningLabel_UnknownModule_FallsBackToTitleWithoutIdfa()
    {
        var label = EdisonMainViewModel.ResolveRunningLabel(
            Running("Něco jiného [CK_SCHWAN]", "X.exe", "ALVA.X.form"), new Dictionary<string, string>());

        Assert.Equal("Něco jiného", label);
    }

    // WPF okna shellu nemají exe ani formu → jen titulek bez suffixu, který jim přidal WindowNavigator.
    [Fact]
    public void ResolveRunningLabel_WpfWindow_StripsShellSuffix()
    {
        var label = EdisonMainViewModel.ResolveRunningLabel(
            Running("EDInputForm [CK_SCHWAN]"), new Dictionary<string, string>());

        Assert.Equal("EDInputForm", label);
    }
}
