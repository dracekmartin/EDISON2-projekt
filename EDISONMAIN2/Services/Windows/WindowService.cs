using System.Windows;
using ALWA.SYS;
using EDISONMAIN2.Services;

namespace EDISONMAIN2.Services.Windows
{
    class WindowService : IWindowService
    {
        public void ShowInputFormTest(ConnectionParams connParams, string ns, string idfa)
        {
            var mlRest = new MLRest(connParams.Server, connParams.RestPort, ns, idfa)
            {
                LoginName = connParams.UserName,
                FullName  = connParams.FullName,
                Mlp       = connParams.Mlp.ToString()
            };
            new EDInputFormTestWindow(mlRest) { Owner = Application.Current.MainWindow }.Show();
        }

        public void ShowEDListTest(ConnectionParams connParams, string ns, string idfa)
        {
            var mlRest = new MLRest(connParams.Server, connParams.RestPort, ns, idfa)
            {
                LoginName = connParams.UserName,
                FullName  = connParams.FullName,
                Mlp       = connParams.Mlp.ToString()
            };
            new EDListTestWindow(mlRest) { Owner = Application.Current.MainWindow }.Show();
        }
    }
}
