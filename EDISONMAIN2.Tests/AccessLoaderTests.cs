using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;

namespace EDISONMAIN2.Tests;

public class AccessLoaderTests
{
    private static IMLRest MockAccessCall(int apId, params string[] allowedIds)
    {
        var mock = Substitute.For<IMLRest>();
        var output = new object[allowedIds.Length + 2];
        output[0] = ""; // index 0 — ERR (legacy convention)
        output[1] = apId.ToString();
        for (int i = 0; i < allowedIds.Length; i++)
            output[i + 2] = allowedIds[i];
        mock.QueryOrThrow("ALVA.SYS.Login", "GetEdisonAccess", Arg.Any<object[]>())
            .Returns(output);
        return mock;
    }

    [Fact]
    public void Admin_apId3_IsAllAccess()
    {
        var mlRest = MockAccessCall(apId: 3);
        var access = AccessLoader.Load(mlRest, licence: null, isAdmin: true);

        Assert.True(access.IsAllAccess);
        Assert.True(access.Contains("any-random-id"));
        Assert.True(access.Contains("0"));
    }

    [Fact]
    public void NonAdmin_OnlyExplicitIds_Allowed()
    {
        var mlRest = MockAccessCall(apId: 1, "8", "12", "29");
        var access = AccessLoader.Load(mlRest, licence: null, isAdmin: false);

        Assert.False(access.IsAllAccess);
        Assert.True(access.Contains("8"));
        Assert.True(access.Contains("12"));
        Assert.True(access.Contains("29"));
        Assert.False(access.Contains("99999"));
    }

    [Fact]
    public void AdminRole_AddsFallbackElements()
    {
        // Server vrátil apId<3 (např. user není SuperUser), ale klient ho zná jako admin.
        // Admin fallback (frmMain.AddAdminRights:765-775) doplní SpolecnaEvidence + SettingsShow.
        var mlRest = MockAccessCall(apId: 1, "8");
        var access = AccessLoader.Load(mlRest, licence: null, isAdmin: true);

        Assert.True(access.Contains("0"));    // SpolecnaEvidence
        Assert.True(access.Contains("2002")); // formEDISONMAINSettingsShow
        // formUSERSMain + formTMList se přidávají jen při Type ≠ "Nezadána" → licence=null → ne
        Assert.False(access.Contains("5001"));
        Assert.False(access.Contains("40001"));
    }

    [Fact]
    public void AdminRole_WithLicence_AddsUsersAndTmList()
    {
        var licence = new Lic { Type = "Standard" };
        var mlRest = MockAccessCall(apId: 1);
        var access = AccessLoader.Load(mlRest, licence: licence, isAdmin: true);

        Assert.True(access.Contains("5001"));   // formUSERSMain
        Assert.True(access.Contains("40001"));  // formTMList
    }

    [Fact]
    public void AdminRole_NezadanaLicence_NoUsersOrTmList()
    {
        var licence = new Lic { Type = "Nezadána" };
        var mlRest = MockAccessCall(apId: 1);
        var access = AccessLoader.Load(mlRest, licence: licence, isAdmin: true);

        Assert.False(access.Contains("5001"));
        Assert.False(access.Contains("40001"));
    }
}
