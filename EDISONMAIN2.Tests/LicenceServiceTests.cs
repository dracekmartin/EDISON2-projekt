using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy fallback licence "Nezadána" (legacy frmNoMDI.GetActiveLicense) - instalace bez
/// vystavené licence se nesmí zamknout: jeden login musí projít a admin musí vidět
/// EDISON (nastavení, import licence) ve Správě systému.
/// </summary>
public class LicenceServiceTests
{
    [Fact]
    public void FallbackLicence_MirrorsLegacyDefault()
    {
        var lic = LicenceService.CreateFallbackLicence();

        Assert.Equal("1.003", lic.Version);
        Assert.Equal("Nezadána", lic.Type);
        Assert.Equal(1, lic.CountsLOGIN);   // VerifyLic(0) by vždy vrátil -1 → nikdo by se nepřihlásil
        Assert.Equal(new[] { "0", "2002" }, lic.Elements);
    }

    [Fact]
    public void FallbackLicence_AdminSeesEdisonSettings_NotOtherModules()
    {
        // End-to-end invariant zamčené instalace: fallback licence + admin fallback
        // v AccessLoaderu → EDISON (2002, licensed=0) viditelný, ostatní ne.
        var lic = LicenceService.CreateFallbackLicence();
        var mlRest = MLRestMocks.Create();
        mlRest.QueryOrThrow("ALVA.SYS.Login", "GetEdisonAccess", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output("1"));   // žádná AP
        var access = AccessLoader.Load(mlRest, lic, isAdmin: true);

        var edison = new ExecutableModule { DisplayedName = "EDISON", ElementId = "2002", IsLicensed = false };
        var orgStruktura = new ExecutableModule { DisplayedName = "Organizační struktura", ElementId = "147002", IsLicensed = false };
        var users = new ExecutableModule { DisplayedName = "Uživatelé", ElementId = "5001", IsLicensed = false };

        Assert.True(ModuleTreeState.IsAllowed(edison, lic, access));
        Assert.False(ModuleTreeState.IsAllowed(orgStruktura, lic, access));
        // Uživatelé se bez licence nepřidávají (legacy AddAdminRights: jen při Type ≠ "Nezadána").
        Assert.False(ModuleTreeState.IsAllowed(users, lic, access));

        // Na úrovni složek (Správa systému): Společná evidence (package 0, ve fallback
        // Elements i v admin access) zůstane s EDISONem; Kmenová evidence (package 1)
        // je mimo licenci i AP → celá skrytá včetně Správce úloh z admin fallbacku.
        var spolecna = new ModuleFamily { DisplayedName = "Společná evidence", ElementId = "0", IsLicensed = true };
        spolecna.NestedModules.Add(edison);
        spolecna.NestedModules.Add(orgStruktura);
        var kmenova = new ModuleFamily { DisplayedName = "Kmenová evidence", ElementId = "1", IsLicensed = true };
        kmenova.NestedModules.Add(new ExecutableModule { DisplayedName = "Správce úloh", ElementId = "40001", IsLicensed = false });

        var filtered = ModuleTreeState.FilterFamily(spolecna, lic, access);
        Assert.NotNull(filtered);
        Assert.Equal("EDISON", Assert.Single(filtered.NestedModules).DisplayedName);
        Assert.Null(ModuleTreeState.FilterFamily(kmenova, lic, access));
    }
}
