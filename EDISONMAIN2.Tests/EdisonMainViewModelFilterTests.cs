using ALVA.LICENCE.EDLicence;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.EdisonMainWindow;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy filtračních helperů v <see cref="EdisonMainViewModel"/>: kontrolují,
/// že strom modulů respektuje licenční flag (<see cref="ExecutableModule.IsLicensed"/>)
/// a whitelist <see cref="Lic.Elements"/>, a že prázdné FAMILY se po filtraci ořezávají.
/// </summary>
public class EdisonMainViewModelFilterTests
{
    private static Lic Licence(params string[] elements) =>
        new() { Elements = elements };

    private static ExecutableModule Exe(string id, bool licensed, string name = "M") =>
        new() { DisplayedName = name, ElementId = id, IsLicensed = licensed };

    private static AccessTable Access(params string[] allowed) =>
        new([.. allowed], isAllAccess: false);

    // ---------- IsAllowed ---------------------------------------------------

    [Fact]
    public void IsAllowed_NotLicensed_AlwaysTrue_EvenWithoutLicence()
    {
        var exe = Exe(id: "100", licensed: false);

        Assert.True(EdisonMainViewModel.IsAllowed(exe, licence: null));
        Assert.True(EdisonMainViewModel.IsAllowed(exe, Licence()));
        Assert.True(EdisonMainViewModel.IsAllowed(exe, Licence("999")));
    }

    [Fact]
    public void IsAllowed_LicensedAndNullLicence_False()
    {
        var exe = Exe(id: "100", licensed: true);

        Assert.False(EdisonMainViewModel.IsAllowed(exe, licence: null));
    }

    [Fact]
    public void IsAllowed_LicensedAndElementInWhitelist_True()
    {
        var exe = Exe(id: "100", licensed: true);

        Assert.True(EdisonMainViewModel.IsAllowed(exe, Licence("50", "100", "200")));
    }

    [Fact]
    public void IsAllowed_LicensedAndElementNotInWhitelist_False()
    {
        var exe = Exe(id: "100", licensed: true);

        Assert.False(EdisonMainViewModel.IsAllowed(exe, Licence("50", "200")));
    }

    [Fact]
    public void IsAllowed_LicensedAndEmptyWhitelist_False()
    {
        var exe = Exe(id: "100", licensed: true);

        Assert.False(EdisonMainViewModel.IsAllowed(exe, Licence()));
    }

    [Fact]
    public void IsAllowed_Hidden_FalseEvenWithLicenceAndAccess()
    {
        // Hidden flag má přednost — modul se vyfiltruje bez ohledu na licenci či access.
        var exe = new ExecutableModule { ElementId = "100", IsLicensed = true, Hidden = true };

        Assert.False(EdisonMainViewModel.IsAllowed(exe, Licence("100")));
    }

    [Fact]
    public void IsAllowed_NotHidden_DefaultsToFalse_PassesThrough()
    {
        // Default Hidden=false → projde licenčním gate.
        var exe = new ExecutableModule { ElementId = "100", IsLicensed = false, Hidden = false };

        Assert.True(EdisonMainViewModel.IsAllowed(exe, licence: null));
    }

    // ---------- FilterFamily -----------------------------------------------

    [Fact]
    public void FilterFamily_AllChildrenAllowed_ReturnsFamilyWithAll()
    {
        var family = new ModuleFamily { DisplayedName = "F", IsExpanded = true };
        family.NestedModules.Add(Exe("1", licensed: false));
        family.NestedModules.Add(Exe("2", licensed: false));

        var filtered = EdisonMainViewModel.FilterFamily(family, licence: null);

        Assert.NotNull(filtered);
        Assert.Equal("F", filtered.DisplayedName);
        Assert.True(filtered.IsExpanded);
        Assert.Equal(2, filtered.NestedModules.Count);
    }

    [Fact]
    public void FilterFamily_SomeChildrenAllowed_ReturnsFamilyWithSubset()
    {
        var family = new ModuleFamily { DisplayedName = "F" };
        family.NestedModules.Add(Exe("100", licensed: true, name: "Hidden"));
        family.NestedModules.Add(Exe("200", licensed: true, name: "Visible"));

        var filtered = EdisonMainViewModel.FilterFamily(family, Licence("200"));

        Assert.NotNull(filtered);
        var only = Assert.Single(filtered.NestedModules);
        Assert.Equal("Visible", only.DisplayedName);
    }

    [Fact]
    public void FilterFamily_AllChildrenHidden_ReturnsNull()
    {
        var family = new ModuleFamily { DisplayedName = "F" };
        family.NestedModules.Add(Exe("100", licensed: true));
        family.NestedModules.Add(Exe("200", licensed: true));

        var filtered = EdisonMainViewModel.FilterFamily(family, Licence("999"));

        Assert.Null(filtered);
    }

    [Fact]
    public void FilterFamily_NestedFamilyAllChildrenHidden_OuterRemoved()
    {
        var inner = new ModuleFamily { DisplayedName = "Inner" };
        inner.NestedModules.Add(Exe("100", licensed: true));
        var outer = new ModuleFamily { DisplayedName = "Outer" };
        outer.NestedModules.Add(inner);

        var filtered = EdisonMainViewModel.FilterFamily(outer, Licence("999"));

        // Inner zmizel (žádné dítě nepovoleno) → Outer taky (nic nezbylo)
        Assert.Null(filtered);
    }

    [Fact]
    public void FilterFamily_NestedFamilyKeepsOuterIfAnyDescendantAllowed()
    {
        var inner = new ModuleFamily { DisplayedName = "Inner" };
        inner.NestedModules.Add(Exe("100", licensed: true));
        inner.NestedModules.Add(Exe("200", licensed: false));
        var outer = new ModuleFamily { DisplayedName = "Outer" };
        outer.NestedModules.Add(inner);

        var filtered = EdisonMainViewModel.FilterFamily(outer, Licence("999"));

        Assert.NotNull(filtered);
        var preservedInner = Assert.IsType<ModuleFamily>(Assert.Single(filtered.NestedModules));
        Assert.Equal("Inner", preservedInner.DisplayedName);
        var preservedExe = Assert.IsType<ExecutableModule>(Assert.Single(preservedInner.NestedModules));
        Assert.Equal("200", preservedExe.ElementId);
    }

    // ---------- IsAllowed — aplikační access (3. vrstva) -------------------

    [Fact]
    public void IsAllowed_NotLicensed_ButElementNotInAccess_False()
    {
        // Access gate platí i pro nelicencované moduly — bez ElementId v AccessTable se nezobrazí.
        var exe = Exe(id: "100", licensed: false);

        Assert.False(EdisonMainViewModel.IsAllowed(exe, licence: null, Access("200", "300")));
    }

    [Fact]
    public void IsAllowed_NotLicensed_AndElementInAccess_True()
    {
        var exe = Exe(id: "100", licensed: false);

        Assert.True(EdisonMainViewModel.IsAllowed(exe, licence: null, Access("100", "200")));
    }

    [Fact]
    public void IsAllowed_LicensedAndInWhitelist_ButNotInAccess_False()
    {
        // Licence whitelist projde, ale aplikační access ho zařízne — obě vrstvy musí povolit.
        var exe = Exe(id: "100", licensed: true);

        Assert.False(EdisonMainViewModel.IsAllowed(exe, Licence("100"), Access("200")));
    }

    [Fact]
    public void IsAllowed_LicensedAndInWhitelist_AndInAccess_True()
    {
        var exe = Exe(id: "100", licensed: true);

        Assert.True(EdisonMainViewModel.IsAllowed(exe, Licence("100"), Access("100")));
    }

    [Fact]
    public void IsAllowed_AllAccess_BypassesAccessCheck()
    {
        // Admin (apId=3) → IsAllAccess → access vrstva propustí cokoliv (licence stále platí).
        var allAccess = new AccessTable([], isAllAccess: true);
        var exe = Exe(id: "100", licensed: false);

        Assert.True(EdisonMainViewModel.IsAllowed(exe, licence: null, allAccess));
    }

    [Fact]
    public void FilterFamily_AccessHidesAllChildren_FamilyDropped()
    {
        var family = new ModuleFamily { DisplayedName = "F" };
        family.NestedModules.Add(Exe("100", licensed: false));
        family.NestedModules.Add(Exe("101", licensed: false));

        Assert.Null(EdisonMainViewModel.FilterFamily(family, licence: null, Access("999")));
    }

    // ---------- TryFilter (router) -----------------------------------------

    [Fact]
    public void TryFilter_Exe_RoutesToIsAllowed()
    {
        var exe = Exe("100", licensed: true);

        Assert.NotNull(EdisonMainViewModel.TryFilter(exe, Licence("100")));
        Assert.Null(EdisonMainViewModel.TryFilter(exe, Licence("200")));
    }

    [Fact]
    public void TryFilter_Family_RoutesToFilterFamily()
    {
        var family = new ModuleFamily { DisplayedName = "F" };
        family.NestedModules.Add(Exe("100", licensed: true));

        Assert.NotNull(EdisonMainViewModel.TryFilter(family, Licence("100")));
        Assert.Null(EdisonMainViewModel.TryFilter(family, Licence("999")));
    }
}
