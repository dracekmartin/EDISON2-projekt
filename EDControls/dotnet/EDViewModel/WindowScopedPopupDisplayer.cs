using System.Windows;

namespace EDControls.EDViewModel;

/// <summary>
/// Wrapper kolem globálního <see cref="IPopupDisplayer"/>, který do per-call owneru injektuje
/// VM-vlastní okno. Vytváří se v <see cref="EDViewModel.AttachToWindow(Window)"/> a vrací se
/// jako <see cref="EDViewModel.PopupDisplayer"/>.
/// Resolve order pro každý popup: explicit per-call owner → scoped owner (VM window) → DefaultOwner globálního displayeru.
/// </summary>
internal sealed class WindowScopedPopupDisplayer(IPopupDisplayer inner, Window scopedOwner) : IPopupDisplayer
{
    public bool Confirm(string message, Window? owner = null) =>
        inner.Confirm(message, owner ?? scopedOwner);

    public void Info(string message, Window? owner = null) =>
        inner.Info(message, owner ?? scopedOwner);

    public bool ConfirmClose(Window? owner = null) =>
        inner.ConfirmClose(owner ?? scopedOwner);

    public bool ConfirmExit(string message, Window? owner = null) =>
        inner.ConfirmExit(message, owner ?? scopedOwner);

    public bool? ConfirmYesNoCancel(string message, string? title = null, Window? owner = null) =>
        inner.ConfirmYesNoCancel(message, title, owner ?? scopedOwner);

    public void Error(Exception ex, Window? owner = null) =>
        inner.Error(ex, owner ?? scopedOwner);
}
