// Alias proti ambient System.Windows.Forms.UserControl (UseWindowsForms je zapnuté pro FontDialog).
using UserControl = System.Windows.Controls.UserControl;

namespace EDISONMAIN2.MiniApps;

public partial class DevCounterMiniAppView : UserControl
{
    public DevCounterMiniAppView() => InitializeComponent();
}
