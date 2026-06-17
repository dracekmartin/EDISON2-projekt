using System.Windows;
using System.Windows.Controls;

namespace EDControls.MiniApps;

/// <summary>
/// Sdílená view pro <see cref="SimpleMiniAppViewModel"/> — hostuje obsah z továrny VM.
/// Továrnu volá jednou; panel se pak může dock/float re-parentovat, element zůstává tentýž.
/// </summary>
public partial class SimpleMiniAppView : UserControl
{
    public SimpleMiniAppView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (Host.Content is null && e.NewValue is SimpleMiniAppViewModel vm)
        {
            var content = vm.ContentFactory();
            Host.Content = content;
            vm.OnContentCreated(content);
        }
    }
}
