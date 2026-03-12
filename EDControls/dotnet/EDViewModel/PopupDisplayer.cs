using System.Linq;
using System.Windows;

namespace EDControls.EDViewModel;

public class PopupDisplayer : IPopupDisplayer
{
    private static Window? ActiveWindow =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

    public bool Confirm(string message) =>
        EDPopup.Show(ActiveWindow, message, EDPopupButtons.AnoNe);

    public void Info(string message) =>
        EDPopup.Show(ActiveWindow, message, EDPopupButtons.Ok);

    public bool ConfirmClose() =>
        Confirm("Opravdu chcete zavřít okno bez uložení?");

    public void Error(Exception ex) =>
        EDErrorPopup.Show(ActiveWindow, ex);
}
