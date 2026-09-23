using EDISONMAIN2;
using EDISONMAIN2.Windows.UserProfileWindow;

namespace EDISON2.IntegrationTests;

/// <summary>Data session po loginu: legacy <c>object[]</c> odpovědi, jejichž první datový řádek je číslo.</summary>
[Trait("Category", "Integration")]
public class SessionDataTests
{
    [DevServerFact]
    public void AccessLoaderLoad_RealServer_ProfileIdIsNumber()
    {
        var rest = DevServer.Connect();
        var raw = rest.QueryOrThrow("ALVA.SYS.Login", "GetEdisonAccess", new object[] { });

        // AccessLoader čte index 1 přes int.Parse - chybějící nebo nečíselný řádek shodí načtení session.
        Assert.True(raw.Length > 1, "GetEdisonAccess nevrátil řádek s profilem přístupu.");
        Assert.True(int.TryParse(raw[1]?.ToString(), out var apId), "Profil přístupu (index 1) není číslo.");

        var access = AccessLoader.Load(rest, null, isAdmin: false);

        // 3 = APVse (superuser), AccessLoader ho mapuje na plný přístup.
        Assert.Equal(apId == 3, access.IsAllAccess);
    }

    [DevServerFact]
    public void LoadDefaultWindow_RealServer_ReturnsServerFormId()
    {
        var rest = DevServer.Connect();
        var raw = rest.QueryOrThrow("ALVA.USERS.ApiEdison", "GetUserDefaultForm", new object[] { });

        // Parser nečíselnou hodnotu tiše přečte jako 0 (žádné výchozí okno).
        Assert.True(raw.Length > 1, "GetUserDefaultForm nevrátil řádek s id okna.");
        Assert.True(int.TryParse(raw[1]?.ToString(), out var formId), "Id výchozího okna (index 1) není číslo.");

        Assert.Equal(formId, UserProfileService.LoadDefaultWindow(rest));
    }
}
