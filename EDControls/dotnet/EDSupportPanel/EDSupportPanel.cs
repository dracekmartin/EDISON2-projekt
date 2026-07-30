using System.Windows;
using EDControls.EDViewModel;

namespace EDControls.EDSupportPanel;

/// <summary>
/// Připojuje panel Podpora k oknu. Volá se z <c>EDViewModel.AttachToWindow</c>, takže ho
/// dostane každé okno s ViewModelem bez zásahu do svého XAML (obdoba legacy
/// <c>BaseForm.AttachSideMenu</c>). Opt-out: <c>EDViewModel.ShowSupportPanel = false</c>.
/// </summary>
internal static class EDSupportPanel
{
    // Attach běží před Show() okna; druhé volání (re-attach) nesmí panel zdvojit.
    private static readonly DependencyProperty AttachedProperty =
        DependencyProperty.RegisterAttached("Attached", typeof(bool), typeof(EDSupportPanel));

    /// <summary>Je k oknu panel už připojený? (Pro testy napojení a proti dvojímu attachi.)</summary>
    internal static bool IsAttached(Window owner) => (bool)(owner.GetValue(AttachedProperty) ?? false);

    internal static void Attach(Window owner, EDServices services, IPopupDisplayer popups)
    {
        if (IsAttached(owner)) return;
        owner.SetValue(AttachedProperty, true);

        if (owner.IsVisible)
        {
            Create(owner, services, popups);
            return;
        }

        // Setter Owner na nezobrazené okno hází → odlož vytvoření na první zobrazení.
        void OnVisibleChanged(object s, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue) return;
            owner.IsVisibleChanged -= OnVisibleChanged;
            Create(owner, services, popups);
        }

        owner.IsVisibleChanged += OnVisibleChanged;
    }

    // Best-effort i tady, ne jen v EDViewModel.AttachToWindow: odložená větev běží až
    // z IsVisibleChanged, tedy mimo try/catch volajícího — výjimka by šla do
    // DispatcherUnhandledException a vzala s sebou zobrazení okna.
    private static void Create(Window owner, EDServices services, IPopupDisplayer popups)
    {
        try { new SupportPanelWindow(owner, services, popups).TryShowPanel(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("EDSupportPanel: " + ex.Message); }
    }
}
