using System;
using System.Collections.Generic;
using EDControls;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.ChangelogWindow;
using EDISONMAIN2.Windows.ConnectionWindow;
using EDISONMAIN2.Windows.LoginWindow;
using ChangelogWnd = EDISONMAIN2.Windows.ChangelogWindow.ChangelogWindow;
using ConnectionWnd = EDISONMAIN2.Windows.ConnectionWindow.ConnectionWindow;
using MiniAppPickerWnd = EDISONMAIN2.Windows.MiniAppPickerWindow.MiniAppPickerWindow;
using UserProfileWnd = EDISONMAIN2.Windows.UserProfileWindow.UserProfileWindow;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    /// <summary>
    /// Modální dialogy shellu s výsledkem místo okna. <see cref="IWindowNavigator.ShowDialog"/> bere instanci okna,
    /// které potřebuje STA vlákno a resources aplikace. Přes tohle rozhraní jde kód po dialogu testovat ve VM.
    /// </summary>
    internal interface IShellDialogs
    {
        /// <summary>Výběr miniaplikace a jejího umístění, null = zrušeno.</summary>
        (MiniAppDescriptor Descriptor, MiniAppPlacement? Placement)? PickMiniApp(
            MiniAppCatalog catalog, IReadOnlyList<MiniAppTarget> targets);

        /// <summary>Novinky modálně, vrací stav zaškrtávátka „Nezobrazovat do příští změny" při zavření.</summary>
        bool ShowChangelog(string title, ChangelogContent content, bool snoozed);

        /// <summary>Vlastnosti uživatele. Změnu hesla okno volá přes <paramref name="changePassword"/>.</summary>
        void ShowUserProfile(string defaultWindowDisplay, Func<bool> changePassword);

        /// <summary>Výběr připojení k databázi, null = zrušeno.</summary>
        ConnectionChoice? PickConnection(ILocalPrefs prefs, ConnectionChoice defaultConnection);

        /// <summary>Dialog změny hesla, true = server heslo změnil.</summary>
        bool ChangePassword(IMLRest rest, string userName);
    }

    /// <summary>Produkční dialogy přes navigátor shellu.</summary>
    internal sealed class ShellDialogs(Func<IWindowNavigator> navigator) : IShellDialogs
    {
        public (MiniAppDescriptor Descriptor, MiniAppPlacement? Placement)? PickMiniApp(
            MiniAppCatalog catalog, IReadOnlyList<MiniAppTarget> targets)
        {
            var picker = new MiniAppPickerWnd(catalog, targets);
            return navigator().ShowDialog(picker) == true && picker.Picked is { } descriptor
                ? (descriptor, picker.Placed)
                : null;
        }

        public bool ShowChangelog(string title, ChangelogContent content, bool snoozed)
        {
            var wnd = new ChangelogWnd(title, content, snoozed);
            navigator().ShowDialog(wnd);
            return wnd.SnoozeChecked;
        }

        public void ShowUserProfile(string defaultWindowDisplay, Func<bool> changePassword) =>
            navigator().ShowDialog(new UserProfileWnd(defaultWindowDisplay, changePassword));

        public ConnectionChoice? PickConnection(ILocalPrefs prefs, ConnectionChoice defaultConnection)
        {
            var window = new ConnectionWnd(prefs, defaultConnection);
            return navigator().ShowDialog(window) == true ? window.Picked : null;
        }

        public bool ChangePassword(IMLRest rest, string userName) =>
            navigator().ShowDialog(new ChangePasswordWindow(rest, userName)) == true;
    }
}
