using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy fallback licence "Nezadána" (legacy frmNoMDI.GetActiveLicense) — instalace bez
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

        Assert.True(EdisonMainViewModel.IsAllowed(edison, lic, access));
        Assert.False(EdisonMainViewModel.IsAllowed(orgStruktura, lic, access));
        // Uživatelé se bez licence nepřidávají (legacy AddAdminRights: jen při Type ≠ "Nezadána").
        Assert.False(EdisonMainViewModel.IsAllowed(users, lic, access));
    }
}
