using System.IO;
using EDISONMAIN2.Windows.LoginWindow;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Změna hesla za běhu shellu a zapamatované heslo (menu i Vlastnosti uživatele jdou stejnou cestou).
/// Část testů jede přes skutečný local.json, který sdílí víc instancí shellu.
/// </summary>
[Collection(SharedStaticStateCollection.Name)]
public class PasswordChangeTests
{
    private const string Server = "dev";
    private const int Port = 57770;

    private static ILocalPrefs PrefsRemembering(string blob)
    {
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.SetRememberedPassword(Server, Port, blob);
        return prefs;
    }

    private static bool Change(bool changed, string userName, ILocalPrefs prefs) =>
        PasswordChange.Run(() => changed, Server, Port, userName, prefs);

    /// <summary>local.json v temp adresáři se zapamatovaným heslem <paramref name="rememberedUser"/> na serveru testu.</summary>
    private static void WithSharedPrefs(string rememberedUser, Action body)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"edprefs_{Guid.NewGuid():N}");
        var saved = LocalPrefs.FilePath;
        try
        {
            LocalPrefs.FilePath = Path.Combine(dir, "local.json");
            var start = LocalPrefs.Load();
            start.SetLoginName(Server, Port, rememberedUser);
            start.SetRememberedPassword(Server, Port, PasswordProtector.Protect(rememberedUser, "stare"));
            start.Save();
            body();
        }
        finally
        {
            LocalPrefs.FilePath = saved;
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    // Druhá instance shellu (Spustit další EDISON) se na tentýž server přihlásí a zapamatuje si heslo.
    private static void OtherInstanceRemembers(string user)
    {
        var other = LocalPrefs.Load();
        other.SetLoginName(Server, Port, user);
        other.SetRememberedPassword(Server, Port, PasswordProtector.Protect(user, "jeho"));
        other.Save();
    }

    [Fact]
    public void Changed_ForgetsOwnRememberedPassword()
    {
        // Jinak by příští start předvyplnil staré heslo a login spadl na „Špatné heslo".
        var prefs = PrefsRemembering(PasswordProtector.Protect("novak", "stare"));

        Assert.True(Change(true, "novak", prefs));

        Assert.Null(prefs.RememberedPasswordFor(Server, Port));
        prefs.Received().Save();
    }

    [Fact]
    public void Changed_KeepsOtherUsersRememberedPassword()
    {
        // Po Přihlásit jiného uživatele patří zapamatované heslo serveru uživateli z loginu.
        var blob = PasswordProtector.Protect("svoboda", "jeho");
        var prefs = PrefsRemembering(blob);

        Assert.True(Change(true, "novak", prefs));

        Assert.Equal(blob, prefs.RememberedPasswordFor(Server, Port));
    }

    [Fact]
    public void Changed_KeepsRememberedPasswordOfOtherServer()
    {
        // Heslo se změnilo jen v databázi tohoto serveru.
        var prefs = PrefsRemembering(PasswordProtector.Protect("novak", "stare"));
        var otherServer = PasswordProtector.Protect("novak", "jine");
        prefs.SetRememberedPassword("rel", Port, otherServer);

        Change(true, "novak", prefs);

        Assert.Null(prefs.RememberedPasswordFor(Server, Port));
        Assert.Equal(otherServer, prefs.RememberedPasswordFor("rel", Port));
    }

    [Fact]
    public void Cancelled_KeepsRememberedPassword()
    {
        var blob = PasswordProtector.Protect("novak", "stare");
        var prefs = PrefsRemembering(blob);

        Assert.False(Change(false, "novak", prefs));

        Assert.Equal(blob, prefs.RememberedPasswordFor(Server, Port));
        prefs.DidNotReceive().Save();
    }

    [Fact]
    public void Changed_OwnBlobSavedByOtherInstance_IsForgotten()
    {
        WithSharedPrefs("svoboda", () =>
        {
            var shell = LocalPrefs.Load();
            OtherInstanceRemembers("novak");

            Change(true, "novak", shell);

            Assert.Null(LocalPrefs.Load().RememberedPasswordFor(Server, Port));
        });
    }

    [Fact]
    public void Changed_ForeignBlobSavedByOtherInstance_Stays()
    {
        WithSharedPrefs("novak", () =>
        {
            var shell = LocalPrefs.Load();
            OtherInstanceRemembers("svoboda");

            Change(true, "novak", shell);

            Assert.Equal("jeho", PasswordProtector.Unprotect(LocalPrefs.Load().RememberedPasswordFor(Server, Port), "svoboda"));
        });
    }

    [Fact]
    public void LocalPrefsNotWritable_ChangeStillReportedAsDone()
    {
        // Server heslo už změnil, selhaný zápis local.json z toho nesmí udělat chybu změny.
        var prefs = PrefsRemembering(PasswordProtector.Protect("novak", "stare"));
        prefs.When(p => p.Save()).Do(_ => throw new IOException("soubor drží jiný proces"));

        Assert.True(Change(true, "novak", prefs));
    }
}
