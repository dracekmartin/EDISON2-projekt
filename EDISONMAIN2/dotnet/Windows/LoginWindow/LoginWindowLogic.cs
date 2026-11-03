namespace EDISONMAIN2.Windows.LoginWindow
{
    internal enum LoginFocus { Username, Password, LoginButton }

    /// <summary>Rozhodovací logika login oken bez WPF - okna jen mapují pole na volání.</summary>
    internal static class LoginWindowLogic
    {
        /// <summary>Fokus na první věc, kterou uživatel udělá: bez jména (první start, přihlášení jiného
        /// uživatele) jméno, se jménem heslo, se zapamatovaným heslem rovnou tlačítko (stačí Enter).</summary>
        internal static LoginFocus PickInitialFocus(string? initialUsername, string? initialPassword)
        {
            if (string.IsNullOrEmpty(initialUsername)) return LoginFocus.Username;
            if (string.IsNullOrEmpty(initialPassword)) return LoginFocus.Password;
            return LoginFocus.LoginButton;
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
