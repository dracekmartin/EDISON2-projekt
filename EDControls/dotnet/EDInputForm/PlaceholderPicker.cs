using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using EDCombo = EDControls.EDFilterComboBox.EDFilterComboBox;

namespace EDControls.EDInputForm;

/// <summary>
/// Výběr datového pole pro vložení do textu (`{POLE}`). Vyhledávání drží sdílený
/// <see cref="EDCombo"/> - žádná další vyhledávací logika, jen se rozbalí u tlačítka a vrátí
/// vybraný token.
/// <para>
/// Proč výběr a ne psaní: název pole napsaný rukou se překlepne a chyba se pozná **až v tisku**.
/// Proč vkládání a ne náhrada hodnoty: pole bývá věta s tokeny uvnitř, ne jen samotný token.
/// </para>
/// </summary>
internal static class PlaceholderPicker
{
    /// <summary>Rozbalí nabídku u prvku a vrátí token ve tvaru <c>{POLE}</c>; null = zrušeno.</summary>
    internal static string? Pick(UIElement anchor, IReadOnlyList<ListItem> fields)
    {
        string? picked = null;

        var combo = new EDCombo
        {
            StrictSelection = true,
            Suggestions = fields,
            MinWidth = 260,
            MaxDropDownHeight = 320,
        };

        var popup = new Popup
        {
            PlacementTarget = anchor,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            Child = new Border
            {
                Background = SystemColors.WindowBrush,
                BorderBrush = SystemColors.ActiveBorderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4),
                Child = combo,
            },
        };

        // Výběr fixuje EDFilterComboBox ve svém DropDownClosed; náš handler je registrovaný
        // později, takže SelectedKey už nese novou hodnotu.
        combo.DropDownClosed += (_, _) =>
        {
            picked = combo.SelectedKey;
            popup.IsOpen = false;
        };
        popup.Opened += (_, _) =>
        {
            combo.Focus();
            combo.IsDropDownOpen = true;
        };

        popup.IsOpen = true;

        // Modální čekání bez vlastního okna - vnořený dispatcher drží běh, dokud popup nezmizí.
        var frame = new System.Windows.Threading.DispatcherFrame();
        popup.Closed += (_, _) => frame.Continue = false;
        System.Windows.Threading.Dispatcher.PushFrame(frame);

        return string.IsNullOrEmpty(picked) ? null : "{" + picked + "}";
    }
}
