using System;
using ALWA.SYS;

namespace EDISONMAIN2.Services
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

    public static class LoginService
    {
        /// <summary>
        /// Calls ALVA.SYS.Login.Login on Caché and returns user info on success.
        /// Uses object[] because the Caché method uses numeric subscripts.
        /// </summary>
        public static LoginResult Login(MLRest mlRest, string username, string password)
        {
            if (string.IsNullOrEmpty(username)) throw new LoginException("Prázdné jméno uživatele.");
            if (string.IsNullOrEmpty(password)) throw new LoginException("Prázdné heslo.");

            var output = mlRest.Query("ALVA.SYS.Login", "Login", new object[] { username, password });
            mlRest.RestStatus.ThrowIfError();

            var data = output[1]?.ToString() ?? "";
            var parts = data.Split(mlRest.Delimiter);
            int mlp = int.Parse(parts[0]);

            if (mlp <= 0) throw new LoginException(GetErrorMessage(mlp));

            return new LoginResult(
                Mlp: mlp,
                UserName: parts.Length > 10 ? parts[10] : username,
                FullName: parts.Length > 1 ? parts[1] : "",
                IsAdmin: parts.Length > 2 && parts[2] == "1",
                Directory: parts.Length > 3 ? parts[3] : "",
                ChangePasswordNeeded: parts.Length > 4 && parts[4] == "1",
                MayUpdate: parts.Length > 5 && parts[5] == "1",
                Email: parts.Length > 7 ? parts[7] : "");
        }

        private static string GetErrorMessage(int code) => code switch
        {
            -1   => "Uživatel neexistuje.",
            -101 => "Uživatel nemá přidělené ID.",
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
