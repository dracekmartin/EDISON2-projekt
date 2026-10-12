using System;
using EDControls;
using EDControls.EDViewModel;

namespace EDISONMAIN2.Windows.LoginWindow
{
    internal enum LoginAttempt { Succeeded, Failed, RememberedPasswordRejected }

    /// <summary>
    /// Přihlašovací okno: ověření proti serveru, politika zapamatovaného hesla a progress fáze startupu.
    /// Režim startu shellu (<see cref="ForStartup"/>) předvyplní z <c>local.json</c> a po přihlášení
    /// zůstává otevřený jako progress podklad; přihlášení jiného uživatele (<see cref="ForReauth"/>)
    /// je modální dialog bez zapamatování (session se nepersistuje).
    /// </summary>
    internal sealed class LoginViewModel : EDViewModel
    {
        private readonly IMLRest _rest;
        private readonly ILocalPrefs? _prefs;
        private string _userName;
        private bool _rememberPassword;
        private string? _errorMessage;
        private string? _progressMessage;
        private bool _isInProgress;

        private LoginViewModel(EDServices services, IMLRest rest, ILocalPrefs? prefs,
                               string userName, string initialPassword) : base(services)
        {
            _rest = rest;
            _prefs = prefs;
            _userName = userName;
            InitialPassword = initialPassword;
            // Předvyplněné heslo → checkbox zaškrtnutý (uložení se má obnovit při dalším loginu).
            _rememberPassword = initialPassword.Length > 0;
            InitialFocus = LoginWindowLogic.PickInitialFocus(userName, initialPassword);
        }

        public static LoginViewModel ForStartup(EDServices services, IMLRest rest, ILocalPrefs prefs)
        {
            prefs.AdoptLegacyLogin(rest.Server, rest.Port);
            // Zapamatované heslo předvyplní jen když patří k naposledy přihlášenému jménu (DPAPI).
            var lastName = prefs.LoginNameFor(rest.Server, rest.Port) ?? "";
            var remembered = prefs.RememberedPasswordFor(rest.Server, rest.Port);
            return new(services, rest, prefs, lastName, PasswordProtector.Unprotect(remembered, lastName) ?? "");
        }

        public static LoginViewModel ForReauth(EDServices services, IMLRest rest) => new(services, rest, null, "", "");

        // Geometrii login okna řeší bootstrapper přes local.json (před loginem není kam ukládat) a pre-login
        // kliky nemají session pro telemetrii.
        protected override string? PersistedFormId => null;

        public string UserName { get => _userName; set => Set(ref _userName, value); }

        public bool RememberPassword { get => _rememberPassword; set => Set(ref _rememberPassword, value); }

        /// <summary>Checkbox jen při startu - reauth session nepersistuje, byl by mrtvý.</summary>
        public bool ShowRemember => _prefs is not null;

        /// <summary>Heslo do <c>PasswordBox</c> (nebindovatelný) při otevření okna.</summary>
        public string InitialPassword { get; }

        internal LoginFocus InitialFocus { get; }

        public string Idfa => _rest.Idfa ?? "";

        public string? ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (Set(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public string? ProgressMessage
        {
            get => _progressMessage;
            private set
            {
                if (Set(ref _progressMessage, value)) OnPropertyChanged(nameof(HasProgress));
            }
        }

        public bool HasProgress => !string.IsNullOrEmpty(ProgressMessage);

        /// <summary>Startup progress fáze - okno zůstává otevřené pod dobíhajícím bootstrapem a nesmí ho zavřít uživatel.</summary>
        public bool IsInProgress
        {
            get => _isInProgress;
            private set
            {
                if (Set(ref _isInProgress, value)) OnPropertyChanged(nameof(IsEditable));
            }
        }

        public bool IsEditable => !IsInProgress;

        public LoginResult? Result { get; private set; }

        /// <summary>Předvyplněné (zapamatované) heslo server odmítl - <see cref="ApplyRememberedPassword"/>
        /// ho zapomene, při zrušení ho musí uložit volající, jinak se příště vyplní znovu a login zbytečně padá.</summary>
        public bool RememberedPasswordRejected { get; private set; }

        /// <summary>Úspěšné přihlášení při startu - bootstrapper ukončí čekací frame a pokračuje.</summary>
        public event EventHandler? LoginSucceeded;

        // Heslo úspěšného přihlášení do ApplyRememberedPassword; drží ho i PasswordBox okna.
        private string _acceptedPassword = "";

        internal LoginAttempt Login(string password)
        {
            ErrorMessage = null;
            LoginResult result;
            try
            {
                result = UserAuthenticator.Login(_rest, UserName, password);
            }
            catch (LoginException ex)
            {
                ErrorMessage = ex.Message;
                if (!LoginWindowLogic.ShouldForgetRememberedPassword(InitialPassword, password))
                    return LoginAttempt.Failed;
                RememberedPasswordRejected = true;
                return LoginAttempt.RememberedPasswordRejected;
            }

            Result = result;
            if (_prefs is null)
            {
                CloseDialog(true);
                return LoginAttempt.Succeeded;
            }

            _acceptedPassword = password;
            IsInProgress = true;
            ProgressMessage = "Přihlašování…";
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
            return LoginAttempt.Succeeded;
        }

        /// <summary>
        /// Zapamatované heslo do prefs v paměti. Volat až po uložení geometrie okna (ukládá celé prefs):
        /// na disk smí blob jen spolu se jménem v SavePostLoginState, jinak by předčasný konec
        /// startupu (licence, změna hesla) nechal blob jiného jména a uživatel by o heslo přišel.
        /// </summary>
        internal void ApplyRememberedPassword()
        {
            if (_prefs is null) return;
            if (Result is { } result)
            {
                // Při vynucené změně hesla (feature #17) heslo neukládej - za chvíli se změní a zapamatované by bylo neplatné.
                _prefs.SetRememberedPassword(_rest.Server, _rest.Port, RememberPassword && !result.ChangePasswordNeeded
                    ? PasswordProtector.Protect(result.UserName, _acceptedPassword)
                    : null);
                _acceptedPassword = "";
            }
            else if (RememberedPasswordRejected)
            {
                _prefs.SetRememberedPassword(_rest.Server, _rest.Port, null);
            }
        }

        /// <summary>Modální dialog (reauth) zavírá <c>IsCancel</c> sám; startup okno běží přes Show.</summary>
        internal void Cancel()
        {
            if (_prefs is not null && !IsInProgress) CloseDialog(null);
        }

        internal void ShowProgress(string message) => ProgressMessage = message;

        internal void ShowError(string message) => ErrorMessage = message;

        /// <summary>Zavření bootstrapperem po vykreslení shellu - jediná cesta ven z progress fáze.</summary>
        internal void CompleteHandoff()
        {
            IsInProgress = false;
            CloseDialog(null);
        }
    }
}
