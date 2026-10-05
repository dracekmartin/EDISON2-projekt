using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.LoginWindow;

namespace EDISONMAIN2.Windows.UserProfileWindow
{
    /// <summary>Pole formuláře, jak je VM potřebuje; okno je mapuje nad <c>TheForm.Items</c>.</summary>
    internal interface IUserProfileForm
    {
        /// <summary>Server BeforeSave validace (<c>EDInputForm.Save</c>); false = chybu ukázal formulář sám.</summary>
        bool Save();

        /// <summary>Klientská re-validace - editovatelnost cesty podle přepínače „Použít osobní adresář".</summary>
        void Validate();

        string? Get(string key);

        /// <summary>Neznámý klíč tiše přeskočí.</summary>
        void Set(string key, string value);
    }

    /// <summary>
    /// „Vlastnosti uživatele" - hostuje EDInputForm (schema <c>ALVA.EDISONMAIN2.EDInputForm.UserProfile</c>).
    /// Data (přehled, osobní/centrální export adresář) drží klient přes <see cref="UserProfileService"/>, ne
    /// schema třída. Nahrazuje legacy WinForms <c>formEDISONMAINUserEdit</c>.
    /// </summary>
    class UserProfileViewModel : EDViewModel
    {
        private readonly string _defaultWindowDisplay;
        private readonly Func<string, bool> _directoryExists;

        /// <param name="defaultWindowDisplay">Název výchozího okna (server ho nezná - dodá shell z modulového stromu).</param>
        public UserProfileViewModel(string defaultWindowDisplay)
            : this(EDServices.Current, defaultWindowDisplay, Directory.Exists) { }

        internal UserProfileViewModel(EDServices services, string defaultWindowDisplay, Func<string, bool> directoryExists)
            : base(services)
        {
            _defaultWindowDisplay = defaultWindowDisplay;
            _directoryExists = directoryExists;
            ChangePasswordCommand   = Command(OpenChangePassword);
            ShowCertificatesCommand = CommandAsync(ShowCertificatesAsync);
        }

        /// <summary>Okno ho nastaví po <c>InitializeComponent</c>, dřív formulář neexistuje.</summary>
        internal IUserProfileForm? Form { get; set; }

        public ICommand ChangePasswordCommand { get; }
        public ICommand ShowCertificatesCommand { get; }

        /// <summary>Po Init schématu naplní hodnoty klientsky, pak re-validace nastaví editovatelnost cesty.</summary>
        internal void FillForm()
        {
            if (Form is not { } form || MLRest is not { } ml) return;

            var p = UserProfileService.Load(ml);
            form.Set("fullName", ml.FullName);
            form.Set("loginName", ml.LoginName);
            form.Set("mlp", ml.Mlp);
            form.Set("usePersonDir", p.UsePersonDir ? "1" : "0");
            form.Set("personDir", p.PersonDir);
            form.Set("centralDir", p.CentralExportDir);
            form.Set("defaultWindow", _defaultWindowDisplay);
            form.Validate();
        }

        protected override void OnOk()
        {
            if (Save()) CloseDialog(true);
        }

        // Server BeforeSave validace (neprázdnost) → klientská kontrola existence složky → persist.
        internal bool Save()
        {
            if (Form is not { } form || !form.Save()) return false;

            bool use = form.Get("usePersonDir") == "1";
            string dir = form.Get("personDir") ?? "";
            if (ValidateExportDir(use, dir) is { } error)
            {
                PopupDisplayer.Info(error);
                return false;
            }

            UserProfileService.SaveExportDir(Rest, use, dir);
            // Nově spouštěné legacy moduly dostanou adresář hned (legacy shell až po dalším loginu);
            // surová cesta bez příznaku „použít" - stejně ji vrací i login.
            _services.UserDirectory = dir;
            return true;
        }

        /// <summary>Klientský guard před persistem (server existenci složky nekontroluje); null = uložit.</summary>
        internal string? ValidateExportDir(bool usePersonDir, string personDir) =>
            usePersonDir && !_directoryExists(personDir) ? "Zadaný osobní adresář neexistuje." : null;

        private void OpenChangePassword()
        {
            bool? changed = WindowNavigator.ShowDialog(new ChangePasswordWindow(Rest, Rest.LoginName));
            if (changed == true) PopupDisplayer.Info("Heslo bylo změněno.");
        }

        private async Task ShowCertificatesAsync()
        {
            using (ActionInProgress())
                await WindowNavigator.ShowLegacyAsDialogAsync("USERS.exe", "ALVA.USERS.formUSERSCertificates");
        }
    }
}
