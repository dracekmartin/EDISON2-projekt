using System;
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

            // TODO (Priority 5): load these from config rather than hardcoding defaults
            var connParams = new ConnectionParams("192.168.16.1", 57770, "martin.hubata", "martin.hubata", 167);

            // Fetch companies using a temporary empty-namespace MLRest (legacy API requires concrete MLRest)
            var emptyRest = new MLRest(connParams.Server, connParams.RestPort, "", "")
            {
                LoginName = connParams.UserName,
                FullName  = connParams.FullName,
                Mlp       = connParams.Mlp.ToString()
            };
            string errState = "";
            var companies = GetCompanyList(ref errState, emptyRest, false);
            if (!string.IsNullOrEmpty(errState))
                throw new InvalidOperationException(errState);

            // Find default company to create the real MLRest connection
            var defaultCompany = companies[companies.Select((c, i) => (c, i)).First(x => x.c.Id == "CB_DP").i];
            var mlRest = new MLRestAdapter(new MLRest(connParams.Server, connParams.RestPort, defaultCompany.Database, defaultCompany.Id)
            {
                LoginName = connParams.UserName,
                FullName  = connParams.FullName,
                Mlp       = connParams.Mlp.ToString()
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
