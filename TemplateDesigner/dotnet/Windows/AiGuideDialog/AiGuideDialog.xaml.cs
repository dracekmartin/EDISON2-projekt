using System.Windows;
using System.Windows.Controls;

namespace TemplateDesigner.Windows.AiGuideDialog;

public partial class AiGuidePanel : UserControl
{
    public AiGuidePanel()
    {
        InitializeComponent();

        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is AiGuidePanelViewModel vm)
                vm.RequestScrollToEnd = () => Dispatcher.BeginInvoke(() => ChatScroll.ScrollToEnd());
        };
    }
}
