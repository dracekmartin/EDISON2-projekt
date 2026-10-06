using ALVA.LICENCE.EDLicence;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.EdisonMainWindow;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy filtračních helperů v <see cref="ModuleTreeState"/>: kontrolují,
/// že strom modulů respektuje licenční flag (<see cref="ExecutableModule.IsLicensed"/>)
/// a whitelist <see cref="Lic.Elements"/>, a že prázdné FAMILY se po filtraci ořezávají.
/// </summary>
public class ModuleTreeStateFilterTests
{
    private static Lic Licence(params string[] elements) =>
        new() { Elements = elements };

    private static ExecutableModule Exe(string id, bool licensed, string name = "M") =>
        new() { Name = name, ElementId = id, IsLicensed = licensed };

    private static AccessTable Access(params string[] allowed) =>
        new([.. allowed], isAllAccess: false);

    [Fact]
    public void IsAllowed_NotLicensed_AlwaysTrue_EvenWithoutLicence()
    {
        var exe = Exe(id: "100", licensed: false);

        Assert.True(ModuleTreeState.IsAllowed(exe, licence: null));
        Assert.True(ModuleTreeState.IsAllowed(exe, Licence()));
        Assert.True(ModuleTreeState.IsAllowed(exe, Licence("999")));
    }

    [Fact]
    public void IsAllowed_LicensedAndNullLicence_False()
    {
        var exe = Exe(id: "100", licensed: true);

        Assert.False(ModuleTreeState.IsAllowed(exe, licence: null));
    }

    [Fact]
    public void IsAllowed_LicensedAndElementInWhitelist_True()
    {
        var exe = Exe(id: "100", licensed: true);

        Assert.True(ModuleTreeState.IsAllowed(exe, Licence("50", "100", "200")));
    }

    [Fact]
    public void IsAllowed_LicensedAndElementNotInWhitelist_False()
    {
        var exe = Exe(id: "100", licensed: true);

        Assert.False(ModuleTreeState.IsAllowed(exe, Licence("50", "200")));
    }

    [Fact]
    public void IsAllowed_LicensedAndEmptyWhitelist_False()
    {
        var exe = Exe(id: "100", licensed: true);

        Assert.False(ModuleTreeState.IsAllowed(exe, Licence()));
    }

    [Fact]
    public void IsAllowed_LicensedAndNullElements_False()
    {
        // licence?.Elements?.Contains(...) ?? false - Elements == null spadne na false → drop.
        var exe = Exe(id: "100", licensed: true);

        Assert.False(ModuleTreeState.IsAllowed(exe, new Lic { Elements = null! }));
    }

    [Fact]
    public void IsAllowed_EmptyElementId_NotInAccess_StillAllowed()
    {
        // Legacy 4-polový EXE bez ElementId → AP gate se přeskočí (parita s FAMILY větví).
        var exe = Exe(id: "", licensed: false);

        Assert.True(ModuleTreeState.IsAllowed(exe, licence: null, Access("200", "300")));
    }

    [Fact]
    public void IsAllowed_Hidden_FalseEvenWithLicenceAndAccess()
    {
        // Hidden flag má přednost - modul se vyfiltruje bez ohledu na licenci či access.
        var exe = new ExecutableModule { ElementId = "100", IsLicensed = true, Hidden = true };

        Assert.False(ModuleTreeState.IsAllowed(exe, Licence("100")));
    }

    [Fact]
    public void FilterFamily_AllChildrenAllowed_ReturnsFamilyWithAll()
    {
        var family = new ModuleFamily { Name = "F", IsExpanded = true };
        family.NestedModules.Add(Exe("1", licensed: false));
        family.NestedModules.Add(Exe("2", licensed: false));

        var filtered = ModuleTreeState.FilterFamily(family, licence: null);

        Assert.NotNull(filtered);
        Assert.Equal("F", filtered.DisplayedName);
        Assert.True(filtered.IsExpanded);
        Assert.Equal(2, filtered.NestedModules.Count);
    }

    [Fact]
    public void FilterFamily_SomeChildrenAllowed_ReturnsFamilyWithSubset()
    {
        var family = new ModuleFamily { Name = "F" };
        family.NestedModules.Add(Exe("100", licensed: true, name: "Hidden"));
        family.NestedModules.Add(Exe("200", licensed: true, name: "Visible"));

        var filtered = ModuleTreeState.FilterFamily(family, Licence("200"));

        Assert.NotNull(filtered);
        var only = Assert.Single(filtered.NestedModules);
        Assert.Equal("Visible", only.DisplayedName);
    }

    [Fact]
    public void FilterFamily_AllChildrenHidden_ReturnsNull()
    {
        var family = new ModuleFamily { Name = "F" };
        family.NestedModules.Add(Exe("100", licensed: true));
        family.NestedModules.Add(Exe("200", licensed: true));

        var filtered = ModuleTreeState.FilterFamily(family, Licence("999"));

        Assert.Null(filtered);
    }

    [Fact]
    public void FilterFamily_NestedFamilyAllChildrenHidden_OuterRemoved()
    {
        var inner = new ModuleFamily { Name = "Inner" };
        inner.NestedModules.Add(Exe("100", licensed: true));
        var outer = new ModuleFamily { Name = "Outer" };
        outer.NestedModules.Add(inner);

        var filtered = ModuleTreeState.FilterFamily(outer, Licence("999"));

        // Inner zmizel (žádné dítě nepovoleno) → Outer taky (nic nezbylo)
        Assert.Null(filtered);
    }

    [Fact]
    public void FilterFamily_NestedFamilyKeepsOuterIfAnyDescendantAllowed()
    {
        var inner = new ModuleFamily { Name = "Inner" };
        inner.NestedModules.Add(Exe("100", licensed: true));
        inner.NestedModules.Add(Exe("200", licensed: false));
        var outer = new ModuleFamily { Name = "Outer" };
        outer.NestedModules.Add(inner);

        var filtered = ModuleTreeState.FilterFamily(outer, Licence("999"));

        Assert.NotNull(filtered);
        var preservedInner = Assert.IsType<ModuleFamily>(Assert.Single(filtered.NestedModules));
        Assert.Equal("Inner", preservedInner.DisplayedName);
        var preservedExe = Assert.IsType<ExecutableModule>(Assert.Single(preservedInner.NestedModules));
        Assert.Equal("200", preservedExe.ElementId);
    }

    [Fact]
    public void IsAllowed_NotLicensed_ButElementNotInAccess_False()
    {
        // Access gate platí i pro nelicencované moduly - bez ElementId v AccessTable se nezobrazí.
        var exe = Exe(id: "100", licensed: false);

        Assert.False(ModuleTreeState.IsAllowed(exe, licence: null, Access("200", "300")));
    }

    [Fact]
    public void IsAllowed_NotLicensed_AndElementInAccess_True()
    {
        var exe = Exe(id: "100", licensed: false);

        Assert.True(ModuleTreeState.IsAllowed(exe, licence: null, Access("100", "200")));
    }

    [Fact]
    public void IsAllowed_LicensedAndInWhitelist_ButNotInAccess_False()
    {
        // Licence whitelist projde, ale aplikační access ho zařízne - obě vrstvy musí povolit.
        var exe = Exe(id: "100", licensed: true);

        Assert.False(ModuleTreeState.IsAllowed(exe, Licence("100"), Access("200")));
    }

    [Fact]
    public void IsAllowed_LicensedAndInWhitelist_AndInAccess_True()
    {
        var exe = Exe(id: "100", licensed: true);

        Assert.True(ModuleTreeState.IsAllowed(exe, Licence("100"), Access("100")));
    }

    [Fact]
    public void IsAllowed_AllAccess_BypassesAccessCheck()
    {
        // Admin (apId=3) → IsAllAccess → access vrstva propustí cokoliv (licence stále platí).
        var allAccess = new AccessTable([], isAllAccess: true);
        var exe = Exe(id: "100", licensed: false);

        Assert.True(ModuleTreeState.IsAllowed(exe, licence: null, allAccess));
    }

    [Fact]
    public void FilterFamily_AccessHidesAllChildren_FamilyDropped()
    {
        var family = new ModuleFamily { Name = "F" };
        family.NestedModules.Add(Exe("100", licensed: false));
        family.NestedModules.Add(Exe("101", licensed: false));

        Assert.Null(ModuleTreeState.FilterFamily(family, licence: null, Access("999")));
    }

    // Legacy BuildTreeXML kontroloval licenci i accessTable na každém uzlu včetně
    // skupin - složka mimo licenci/AP skryje celou větev i s povolenými dětmi.

    [Fact]
    public void FilterFamily_LicensedFamilyNotInWhitelist_DroppedWithAllowedChildren()
    {
        var family = new ModuleFamily { Name = "Kmenová evidence", ElementId = "1", IsLicensed = true };
        family.NestedModules.Add(Exe("40001", licensed: false));

        Assert.Null(ModuleTreeState.FilterFamily(family, Licence("999")));
        Assert.Null(ModuleTreeState.FilterFamily(family, licence: null));
    }

    [Fact]
    public void FilterFamily_LicensedFamilyInWhitelist_Kept()
    {
        var family = new ModuleFamily { Name = "F", ElementId = "1", IsLicensed = true };
        family.NestedModules.Add(Exe("40001", licensed: false));

        var filtered = ModuleTreeState.FilterFamily(family, Licence("1"));

        Assert.NotNull(filtered);
        Assert.Equal("1", filtered.ElementId);
        Assert.True(filtered.IsLicensed);
    }

    [Fact]
    public void FilterFamily_FamilyNotInAccess_DroppedWithAllowedChildren()
    {
        // Případ „Správce úloh": 40001 v accessTable (admin fallback), ale package 1 ne
        // → celá složka skrytá, dítě se nesmí zobrazit.
        var family = new ModuleFamily { Name = "Kmenová evidence", ElementId = "1" };
        family.NestedModules.Add(Exe("40001", licensed: false));

        Assert.Null(ModuleTreeState.FilterFamily(family, licence: null, Access("40001")));
    }

    [Fact]
    public void FilterFamily_FamilyInAccess_KeptWithAllowedChildren()
    {
        var family = new ModuleFamily { Name = "F", ElementId = "1" };
        family.NestedModules.Add(Exe("40001", licensed: false));

        var filtered = ModuleTreeState.FilterFamily(family, licence: null, Access("1", "40001"));

        Assert.NotNull(filtered);
        Assert.Single(filtered.NestedModules);
    }

    [Fact]
    public void FilterFamily_EmptyElementId_SkipsAccessGate()
    {
        // Syntetické složky (bez package ID) access gate nemají - filtrují se jen přes děti.
        var family = new ModuleFamily { Name = "F", ElementId = "" };
        family.NestedModules.Add(Exe("100", licensed: false));

        var filtered = ModuleTreeState.FilterFamily(family, licence: null, Access("100"));

        Assert.NotNull(filtered);
        Assert.Equal("100", Assert.IsType<ExecutableModule>(Assert.Single(filtered.NestedModules)).ElementId);
    }

    [Fact]
    public void FilterFamily_AllAccess_BypassesFamilyAccessGate()
    {
        var allAccess = new AccessTable([], isAllAccess: true);
        var family = new ModuleFamily { Name = "F", ElementId = "1" };
        family.NestedModules.Add(Exe("100", licensed: false));

        var filtered = ModuleTreeState.FilterFamily(family, licence: null, allAccess);

        Assert.NotNull(filtered);
        Assert.Equal("100", Assert.IsType<ExecutableModule>(Assert.Single(filtered.NestedModules)).ElementId);
    }

    // mlineOnly (legacy frmMain.vb:1204/1227): mimo doménu M-line se uzel vůbec nezobrazí,
    // v doméně dostane suffix " [### M-line ###]". Platí pro listy i složky.

    [Fact]
    public void TryFilter_MlineOnlyExe_OutsideDomain_Hidden()
    {
        var exe = new ExecutableModule { Name = "Doklady OLD", ElementId = "92101", MlineOnly = true };

        Assert.Null(ModuleTreeState.TryFilter(exe, licence: null, access: null, isMlineDomain: false));
    }

    [Fact]
    public void TryFilter_MlineOnlyExe_InDomain_VisibleWithSuffix()
    {
        var exe = new ExecutableModule { Name = "Doklady OLD", ElementId = "92101", MlineOnly = true };

        var filtered = ModuleTreeState.TryFilter(exe, licence: null, access: null, isMlineDomain: true);

        Assert.NotNull(filtered);
        Assert.Equal("Doklady OLD [### M-line ###]", filtered.DisplayedName);
    }

    // Suffix je jen pohled - cesta jde do ToolTipů a plochy, kde by se lišila od téhož modulu jinde.
    [Fact]
    public void TryFilter_MlineOnlyExe_InDomain_TreePathAndNameWithoutSuffix()
    {
        var exe = new ExecutableModule { Name = "Doklady OLD", ElementId = "92101", MlineOnly = true, FamilyPath = "Sklady" };

        var filtered = Assert.IsType<ExecutableModule>(ModuleTreeState.TryFilter(exe, licence: null, access: null, isMlineDomain: true));

        Assert.Equal("Sklady › Doklady OLD", filtered.TreePath);
        Assert.Equal("Doklady OLD", filtered.Name);
    }

    [Fact]
    public void TryFilter_NormalExe_InDomain_NoSuffix()
    {
        var exe = Exe("100", licensed: false, name: "Běžný");

        var filtered = ModuleTreeState.TryFilter(exe, licence: null, access: null, isMlineDomain: true);

        Assert.NotNull(filtered);
        Assert.Equal("Běžný", filtered.DisplayedName);
    }

    [Fact]
    public void FilterFamily_MlineOnly_OutsideDomain_DroppedWithChildren()
    {
        var family = new ModuleFamily { Name = "Interní", MlineOnly = true };
        family.NestedModules.Add(Exe("100", licensed: false));

        Assert.Null(ModuleTreeState.FilterFamily(family, licence: null, access: null, isMlineDomain: false));
    }

    [Fact]
    public void FilterFamily_MlineOnly_InDomain_KeptWithSuffix()
    {
        var family = new ModuleFamily { Name = "Interní", MlineOnly = true };
        family.NestedModules.Add(Exe("100", licensed: false));

        var filtered = ModuleTreeState.FilterFamily(family, licence: null, access: null, isMlineDomain: true);

        Assert.NotNull(filtered);
        Assert.Equal("Interní [### M-line ###]", filtered.DisplayedName);
        Assert.True(filtered.MlineOnly);
        Assert.Single(filtered.NestedModules);
    }

    [Fact]
    public void FilterFamily_MlineOnlyChild_OutsideDomain_OnlyChildDropped()
    {
        // mlineOnly dítě zmizí, sourozenec i rodina zůstávají.
        var family = new ModuleFamily { Name = "F" };
        family.NestedModules.Add(new ExecutableModule { Name = "Interní", ElementId = "1", MlineOnly = true });
        family.NestedModules.Add(Exe("2", licensed: false, name: "Veřejný"));

        var filtered = ModuleTreeState.FilterFamily(family, licence: null, access: null, isMlineDomain: false);

        Assert.NotNull(filtered);
        Assert.Equal("Veřejný", Assert.Single(filtered.NestedModules).DisplayedName);
    }

    [Fact]
    public void TryFilter_Exe_RoutesToIsAllowed()
    {
        var exe = Exe("100", licensed: true);

        Assert.NotNull(ModuleTreeState.TryFilter(exe, Licence("100")));
        Assert.Null(ModuleTreeState.TryFilter(exe, Licence("200")));
    }

    [Fact]
    public void TryFilter_Family_RoutesToFilterFamily()
    {
        var family = new ModuleFamily { Name = "F" };
        family.NestedModules.Add(Exe("100", licensed: true));

        Assert.NotNull(ModuleTreeState.TryFilter(family, Licence("100")));
        Assert.Null(ModuleTreeState.TryFilter(family, Licence("999")));
    }
}
