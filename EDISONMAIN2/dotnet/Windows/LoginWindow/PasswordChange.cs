using System;
using System.IO;

namespace EDISONMAIN2.Windows.LoginWindow
{
    /// <summary>
    /// Změna hesla přihlášeného uživatele za běhu shellu, z menu i z Vlastností uživatele. Vynucená změna
    /// při loginu jde mimo: heslo se tam ještě neukládá.
    /// </summary>
    internal static class PasswordChange
    {
        /// <summary>Dialog změny hesla (true = server heslo změnil) a po změně zapamatované heslo serveru.</summary>
        internal static bool Run(Func<bool> showDialog, string server, int port, string userName, ILocalPrefs prefs)
        {
            if (!showDialog()) return false;
            try
            {
                // Save převezme blob, který mezitím do sdíleného local.json uložila jiná instance shellu.
                prefs.Save();
                // Blob serveru patří jménu z loginu, po Přihlásit jiného uživatele tedy nemusí být přihlášeného.
                if (PasswordProtector.Unprotect(prefs.RememberedPasswordFor(server, port), userName) is not null)
                {
                    prefs.SetRememberedPassword(server, port, null);
                    prefs.Save();
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Heslo na serveru už je změněné. Starý blob odmítne příští login a zahodí ho sám.
            }
            return true;
        }
    }
}
