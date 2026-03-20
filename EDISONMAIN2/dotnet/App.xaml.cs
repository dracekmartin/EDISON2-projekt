using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using ALVA.EDISONAPI;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Services;
using EDISONMAIN2.Services.Modules;
using EDISONMAIN2.Services.Processes;
using EDISONMAIN2.Windows.EdisonMainWindow;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var services = new EDServices(null, new ReflectionWindowNavigator(), new PopupDisplayer());
            EDServices.Initialize(services);

            // TODO (Priority 5): load server/port from config rather than hardcoding
            string server = "192.168.16.1";
            int port = 57770;

            var loginRest = new MLRest(server, port, "", "");

            // Fetch companies first — Caché Login method needs non-empty Idfa
            string errState = "";
            var companies = GetCompanyList(ref errState, loginRest, false);
            if (!string.IsNullOrEmpty(errState))
                throw new InvalidOperationException(errState);

            var defaultCompany = companies.FirstOrDefault(c => c.IsDefault);
            if (defaultCompany.Id == null) defaultCompany = companies[0];

            // Set Idfa so ALVA.SYS.Login.Login can resolve user's startupForm
            loginRest.Idfa = defaultCompany.Id;

            LoginResult login;
#if DEBUG
            login = new LoginResult(167, "martin.hubata", "martin.hubata", true, "", false, true, "");
#else
            // Login — prevent auto-shutdown when dialog closes (no other window is open yet)
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var loginWindow = new Windows.LoginWindow.LoginWindow(loginRest);
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            login = loginWindow.Result!;
#endif

            // Create the real MLRest connection with company context
            var connParams = new ConnectionParams(server, port, login.UserName, login.FullName, login.Mlp);
            var mlRest = new MLRestAdapter(new MLRest(server, port, defaultCompany.Database, defaultCompany.Id)
            {
                LoginName = login.UserName,
                FullName  = login.FullName,
                Mlp       = login.Mlp.ToString()
            });

            var treeGetter = new ModuleTreeGetter();
            var processLauncher = new ProcessLauncher(connParams);
            var viewModel = new EdisonMainViewModel(companies, mlRest, treeGetter, processLauncher);

            var wnd = new EdisonMainWindow { DataContext = viewModel };
            wnd.Show();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            new PopupDisplayer().Error(e.Exception);
            e.Handled = true;
        }
    }

}
