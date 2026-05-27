using System.Windows;

namespace EDControls.EDViewModel;

public class PopupDisplayer(Window? defaultOwner = null) : IPopupDisplayer
{
    /// <summary>
    /// Defaultní owner pro popupy které nemají per-call override. Settable proto, aby šel
    /// nastavit z App.xaml.cs až po vytvoření shell okna (instance se vytváří dříve než shell).
    /// Null = popup se zobrazí jako top-level bez rodiče (WPF to zvládne).
    /// </summary>
    public Window? DefaultOwner { get; set; } = defaultOwner;

    public bool Confirm(string message, Window? owner = null) =>
        EDPopup.Show(owner ?? DefaultOwner, message, EDPopupButtons.AnoNe);

    public void Info(string message, Window? owner = null) =>
        EDPopup.Show(owner ?? DefaultOwner, message, EDPopupButtons.Ok);

    public bool ConfirmClose(Window? owner = null) =>
        Confirm("Opravdu chcete zavřít okno bez uložení?", owner);

    public bool? ConfirmYesNoCancel(string message, string? title = null, Window? owner = null) =>
        EDPopup.ShowYesNoCancel(owner ?? DefaultOwner, message, title);

    public void Error(Exception ex, Window? owner = null) =>
        EDErrorPopup.Show(owner ?? DefaultOwner, ex);
}
