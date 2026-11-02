using EDISONMAIN2.Modules;

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

    // Bez nastaveného okna server i parser vrátí 0, proto test okno nastaví. SetUserDefaultForm píše pod uživatele
    // v systémové databázi: neexistující MLP 999 by tam založil uzel bez jména, na kterém padají smyčky přes uživatele
    // (ALVA.SYS.Login, Login i ChangePassword). Proto MLP testovacího účtu a testovací firma.
    [TestAccountFact]
    public void DefaultWindowSetLoad_TestAccount_ParserReturnsSavedFormId()
    {
        var rest = DevServer.Connect(DevServer.WriteIdfa, AccountMlp());
        var original = DefaultWindowService.Load(rest);
        const int formId = 145009;
        try
        {
            DefaultWindowService.Set(rest, formId);

            var raw = rest.QueryOrThrow("ALVA.USERS.ApiEdison", "GetUserDefaultForm", new object[] { });
            // Parser nečíselnou hodnotu tiše přečte jako 0 (žádné výchozí okno).
            Assert.True(raw.Length > 1 && raw[1]?.ToString() == formId.ToString(), "GetUserDefaultForm nevrátil uložené id na indexu 1.");
            Assert.Equal(formId, DefaultWindowService.Load(rest));
        }
        finally
        {
            DefaultWindowService.Set(rest, original);
        }
    }

    // GetMLPFromUsername jen čte. Login by MLP vrátil taky, ale nuluje flagy 17 a 20. Oddělovač "~" je v metodě pevný.
    private static string AccountMlp()
    {
        var rest = DevServer.Connect();
        var raw = rest.QueryOrThrow("ALVA.SYSTEM.Main", "GetMLPFromUsername", new object[] { "USERNAME~" + DevServer.Account!.Login });
        var parts = (raw.Length > 1 ? raw[1]?.ToString() ?? "" : "").Split('~');
        Assert.True(parts.Length == 2 && parts[0] == "MLP" && int.TryParse(parts[1], out var mlp) && mlp > 0,
            "GetMLPFromUsername nevrátil MLP testovacího účtu.");
        return parts[1];
    }
}
