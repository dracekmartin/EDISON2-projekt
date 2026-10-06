using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

public class AccessLoaderTests
{
    private static IMLRest MockAccessCall(int apId, params string[] allowedIds)
    {
        var payload = new object[allowedIds.Length + 1];
        payload[0] = apId.ToString();
        for (int i = 0; i < allowedIds.Length; i++)
            payload[i + 1] = allowedIds[i];
        var mock = MLRestMocks.Create();
        mock.QueryOrThrow("ALVA.SYS.Login", "GetEdisonAccess", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output(payload));
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
        Assert.False(access.Contains("1"));   // apId (první datový řádek) není element
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

    [Fact]
    public void ApId3_IsAdminFalse_StillAllAccess()
    {
        // Server-side flag apId=3 (SuperUser) přebíjí client isAdmin - IsAllAccess
        // se nastavuje pouze podle serveru, ne podle client-side hodnoty.
        var mlRest = MockAccessCall(apId: 3);
        var access = AccessLoader.Load(mlRest, licence: null, isAdmin: false);

        Assert.True(access.IsAllAccess);
        Assert.True(access.Contains("any-id"));
    }

    [Fact]
    public void NonAdmin_NoExplicitIds_EmptyAccess()
    {
        var mlRest = MockAccessCall(apId: 1);
        var access = AccessLoader.Load(mlRest, licence: null, isAdmin: false);

        Assert.False(access.IsAllAccess);
        Assert.False(access.Contains("0"));
        Assert.False(access.Contains("8"));
    }

    [Fact]
    public void EmptyOrNullIds_InOutput_AreSkipped()
    {
        // Server občas vrátí null/empty entries (např. mezery v allowedIds). Loader je smaže
        // - nikdy by AccessTable.Contains("") neměl trefovat.
        var mlRest = MLRestMocks.Create();
        mlRest.QueryOrThrow("ALVA.SYS.Login", "GetEdisonAccess", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output("1", "8", null, "", "12"));
        var access = AccessLoader.Load(mlRest, licence: null, isAdmin: false);

        Assert.True(access.Contains("8"));
        Assert.True(access.Contains("12"));
        Assert.False(access.Contains(""));
    }

    [Fact]
    public void MissingApId_FallsBackToZero()
    {
        // output[1] null → int.Parse(... ?? "0") = 0 ≠ ApIdAdmin → IsAllAccess=false.
        var mlRest = Substitute.For<IMLRest>();
        mlRest.QueryOrThrow("ALVA.SYS.Login", "GetEdisonAccess", Arg.Any<object[]>())
            .Returns(new object[] { "", null!, "8" });
        var access = AccessLoader.Load(mlRest, licence: null, isAdmin: false);

        Assert.False(access.IsAllAccess);
        Assert.True(access.Contains("8"));
    }
}
