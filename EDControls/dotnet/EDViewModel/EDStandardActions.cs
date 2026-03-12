using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EDControls.EDViewModel;

/// <summary>
/// Factory methods for the standard Add / Edit / Delete actions.
/// Each method pre-fills the label (EDStrings), icon (EDIcons), and keyboard shortcut
/// so that only the command needs to be provided at the call site.
/// </summary>
public static class EDStandardActions
{
    public static EDAction Add(EDViewModel vm) => Add(vm.AddCommand);
    public static EDAction Edit(EDViewModel vm) => Edit(vm.EditCommand);
    public static EDAction Delete(EDViewModel vm) => Delete(vm.DeleteCommand);

    public static EDAction Add(ICommand command) =>
        new((string)Application.Current.FindResource("EDStrings.Add"),
            command,
            (ImageSource)Application.Current.FindResource("EDIcons.Add"),
            Key.N, ModifierKeys.Control);

    public static EDAction Edit(ICommand command) =>
        new((string)Application.Current.FindResource("EDStrings.Edit"),
            command,
            (ImageSource)Application.Current.FindResource("EDIcons.Edit"),
            Key.E, ModifierKeys.Control);

    public static EDAction Delete(ICommand command) =>
        new((string)Application.Current.FindResource("EDStrings.Delete"),
            command,
            (ImageSource)Application.Current.FindResource("EDIcons.Delete"),
            Key.D, ModifierKeys.Control);
}
