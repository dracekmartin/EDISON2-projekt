using System;
using System.Threading.Tasks;
using System.Windows.Input;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.LoginWindow;

namespace EDISONMAIN2.Windows.UserProfileWindow
{
    /// <summary>
    /// „Vlastnosti uživatele" — hostuje EDInputForm (schema
    /// <c>ALVA.EDISONMAIN2.EDInputForm.UserProfile</c>). Data (přehled, osobní/centrální
    /// export adresář) prefilluje a ukládá code-behind přes <see cref="UserProfileService"/>;
    /// VM drží jen akce tlačítek formuláře (změna hesla, certifikáty) a OK→save most.
    /// Nahrazuje legacy WinForms <c>formEDISONMAINUserEdit</c>.
    /// </summary>
    class UserProfileViewModel : EDViewModel
    {
        public UserProfileViewModel()
        {
            ChangePasswordCommand   = Command(OpenChangePassword);
            ShowCertificatesCommand = CommandAsync(ShowCertificates);
        }

        /// <summary>
        /// Code-behind nastaví: uloží formulář (BeforeSave validace + persist) a vrátí true
        /// při úspěchu. OK zavře okno jen když save projde.
        /// </summary>
        public Func<bool>? SaveRequested { get; set; }

        public ICommand ChangePasswordCommand { get; }
        public ICommand ShowCertificatesCommand { get; }

        private void OpenChangePassword()
        {
            bool? changed = WindowNavigator.ShowDialog(new ChangePasswordWindow(MLRest!, MLRest!.LoginName));
            if (changed == true) PopupDisplayer.Info("Heslo bylo změněno.");
        }

        private async Task ShowCertificates()
        {
            using (ActionInProgress())
                await WindowNavigator.ShowLegacyAsDialogAsync("USERS.exe", "ALVA.USERS.formUSERSCertificates");
        }

        protected override void OnOk()
        {
            if (SaveRequested?.Invoke() ?? false) CloseDialog(true);
        }
    }
}
