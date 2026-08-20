using System;
using EDControls;

namespace EDISONMAIN2.Windows.LoginWindow
{
    public record LoginResult(
        int Mlp,
        string UserName,
        string FullName,
        bool IsAdmin,
        string Directory,
        bool ChangePasswordNeeded,
        bool MayUpdate,
        string Email);

    public class LoginException(string message) : Exception(message);

    /// <summary>Výsledek změny hesla - úspěch, nebo chybová hláška k zobrazení uživateli.</summary>
    public record ChangePasswordResult(bool Success, string Message);

    public static class UserAuthenticator
    {
        /// <summary>
        /// Volá <c>ALVA.SYS.Login.Login</c> na Caché. Používá <c>object[]</c> protože server
        /// metoda čte přes numerické subscripty <c>^||ML("IN", N)</c>.
        /// </summary>
        public static LoginResult Login(IMLRest mlRest, string username, string password)
        {
            if (string.IsNullOrEmpty(username)) throw new LoginException("Prázdné jméno uživatele.");
            // Prázdné heslo se NEblokuje - pro LDAP-AUTO / Windows SSO je legitimní (jen jméno).
            // O přijatelnosti rozhoduje server podle login módu uživatele (feature #23):
            // EDISON/LDAP prázdné heslo odmítnou (-107), LDAP-AUTO ho přijme.

            var output = mlRest.QueryOrThrow("ALVA.SYS.Login", "Login", new object[] { username, password });

            var data = output[1]?.ToString() ?? "";
            var parts = data.Split(mlRest.Delimiter);
            // Nečíselný status (prázdná / poškozená odpověď serveru) → vlastní výjimka místo
            // syrové FormatException, ať caller zobrazí smysluplnou hlášku.
            if (!int.TryParse(parts[0], out int mlp))
                throw new LoginException("Neplatná odpověď serveru.");

            if (mlp <= 0) throw new LoginException(GetErrorMessage(mlp));

            return new LoginResult(
                Mlp: mlp,
                UserName: parts.Length > 10 ? parts[10] : username,
                FullName: parts.Length > 1 ? parts[1] : "",
                IsAdmin: parts.Length > 2 && IsTrue(parts[2]),
                Directory: parts.Length > 3 ? parts[3] : "",
                ChangePasswordNeeded: parts.Length > 4 && IsTrue(parts[4]),
                MayUpdate: parts.Length > 5 && IsTrue(parts[5]),
                Email: parts.Length > 7 ? parts[7] : "");
        }

        /// <summary>
        /// Boolean flagy v login response jsou VB-style: legacy USERS ukládá True jako -1
        /// (VB CInt(True)), takže server posílá "-1" i "1". Nenulové číslo = true.
        /// </summary>
        private static bool IsTrue(string value) => int.TryParse(value, out var v) && v != 0;

        /// <summary>
        /// Volá <c>ALVA.SYS.Login.ChangePassword</c> (IN: jméno, staré heslo, nové heslo).
        /// Výstup <c>OUT(1)="status'text"</c> - status 1 = OK, jinak chyba (server dodá text).
        /// </summary>
        public static ChangePasswordResult ChangePassword(IMLRest mlRest, string username, string oldPassword, string newPassword)
        {
            var output = mlRest.QueryOrThrow("ALVA.SYS.Login", "ChangePassword",
                new object[] { username, oldPassword, newPassword });

            var parts = (output[1]?.ToString() ?? "").Split(mlRest.Delimiter);
            int status = parts.Length > 0 && int.TryParse(parts[0], out var s) ? s : 99;
            string serverText = parts.Length > 1 ? parts[1] : "";

            return status == 1
                ? new ChangePasswordResult(true, "")
                : new ChangePasswordResult(false, ChangePasswordMessage(status, serverText));
        }

        // Statusy z ALVA.SYS.Login.ChangePassword: 2=staré neplatné, 3=nové nesplňuje formát,
        // 4=nemožné (uživatel neexistuje), 99=chyba. Server u 2/3 posílá vlastní text - preferuj ho.
        private static string ChangePasswordMessage(int status, string serverText) => status switch
        {
            2 => serverText.Length > 0 ? serverText : "Původní heslo je neplatné.",
            3 => serverText.Length > 0 ? serverText : "Nové heslo nesplňuje požadavky.",
            4 => "Změna hesla není možná (uživatel neexistuje).",
            _ => serverText.Length > 0 ? serverText : "Změna hesla selhala.",
        };

        // -1 je na serveru sběrný kód pro GeneralError, AccessDenied i InvalidUsernameOrPassword
        // (u LDAP účtů selhání bindu proti doméně) - nesmí tedy tvrdit, že uživatel neexistuje.
        // Neexistenci hlásí jedině -101 (UserDoesNotExist). Viz ALVA.SYS.Login:73-84.
        private static string GetErrorMessage(int code) => code switch
        {
            -1   => "Neplatné jméno nebo heslo, nebo selhalo ověření proti doméně.",
            -101 => "Uživatel s tímto jménem v EDISONu neexistuje.",
            -102 => "Uživatel není povolen v systému.",
            -103 => "Uživatel není povolen pro aplikaci EDISON.",
            -104 => "Uživatel není povolen v tomto čase.",
            -105 => "Uživatel je deaktivován.",
            -106 => "Automatické přihlášení není povoleno.",
            -107 => "Špatné heslo.",
            -108 => "Nutná změna hesla.",
            _    => $"Přihlášení selhalo (kód {code})."
        };
    }
}
