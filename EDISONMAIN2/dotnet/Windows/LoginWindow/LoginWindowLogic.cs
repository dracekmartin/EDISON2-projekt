namespace EDISONMAIN2.Windows.LoginWindow
{
    internal enum LoginFocus { Default, Username, Password }

    /// <summary>Rozhodovací logika login oken bez WPF - okna jen mapují pole na volání.</summary>
    internal static class LoginWindowLogic
    {
        /// <summary>Předvyplněné heslo → fokus (a select) na jméno, ať přihlášení snadno
        /// převezme jiný uživatel; jen jméno → rovnou na heslo.</summary>
        internal static LoginFocus PickInitialFocus(string? initialUsername, string? initialPassword)
        {
            if (!string.IsNullOrEmpty(initialPassword)) return LoginFocus.Username;
            if (!string.IsNullOrEmpty(initialUsername)) return LoginFocus.Password;
            return LoginFocus.Default;
        }

        /// <summary>Server odmítl přesně to heslo, které bylo předvyplněné ze zapamatovaného -
        /// caller ho má zapomenout, ať se příště nevyplní znovu.</summary>
        internal static bool ShouldForgetRememberedPassword(string initialPassword, string enteredPassword) =>
            initialPassword.Length > 0 && enteredPassword == initialPassword;

        /// <summary>Klientská kontrola nového hesla (shodu server nekontroluje - dostává jedno
        /// heslo); null = poslat na server.</summary>
        internal static string? ValidateNewPassword(string newPassword, string confirmPassword)
        {
            if (newPassword.Length == 0) return "Zadejte nové heslo.";
            if (newPassword != confirmPassword) return "Nová hesla se neshodují.";
            return null;
        }
    }
}
