using System.Windows;
// Alias proti ambient System.Windows.Forms.UserControl (UseWindowsForms je zapnuté pro FontDialog).
using UserControl = System.Windows.Controls.UserControl;

namespace EDISONMAIN2.MiniApps;

public partial class SupportCarouselMiniAppView : UserControl
{
    public SupportCarouselMiniAppView() => InitializeComponent();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        (DataContext as SupportCarouselMiniAppViewModel)?.UpdateViewportWidth(e.NewSize.Width);
}
