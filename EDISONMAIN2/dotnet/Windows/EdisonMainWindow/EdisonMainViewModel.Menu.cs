using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro WinForms color picker).
using Application = System.Windows.Application;
using ALWA.SYS;
using EDControls;
using EDControls.EDSupportPanel;
using EDControls.EDViewModel;
using EDControls.UserFonts;
using EDISONMAIN2.Windows.ConnectionWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using ConnectionWnd = EDISONMAIN2.Windows.ConnectionWindow.ConnectionWindow;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    // Obsluha hlavního menu shellu (replikuje legacy frmNoMDI.mnuMain), stavový řádek s datem a dvojhmat Ctrl+W.
    partial class EdisonMainViewModel
    {
        // Test funkcí: overlay s vybranou variantou na 10 s - dost na posouzení animace,
        // netřeba nic rušit ručně.
        private async Task TestActionOverlayAsync(string? spriteKey)
        {
            using (ActionInProgress(spriteKey))
                await Task.Delay(TimeSpan.FromSeconds(10));
        }

        // Legacy launche z menu se stejným overlayem jako spouštění ze stromu.
        private async Task LaunchModelessAsync(string exe, string formFqn)
        {
            using (ActionInProgress())
                await WindowNavigator.ShowLegacyAsync(exe, formFqn);
        }

        private async Task LaunchDialogAsync(string exe, string formFqn)
        {
            using (ActionInProgress())
                await WindowNavigator.ShowLegacyAsDialogAsync(exe, formFqn);
        }

        private void ResetForms()
        {
            if (!PopupDisplayer.Confirm("Obnovit výchozí pozice a velikosti všech oken?"))
                return;
            // Reset persistovaných pozic/velikostí oken (per MLP × IDFa) - legacy endpoint.
            Rest.QueryOrThrow("ALVA.LIBRARY.Form", "ResetSettings", new CVar());
            PopupDisplayer.Info("Nastavení oken bylo obnoveno. Změna se projeví po jejich příštím otevření.");
        }

        private static void RunEdisonCopy()
        {
            var exe = CurrentExecutable();
            if (exe.Length == 0) return;
            Process.Start(EdisonCopyStartInfo(exe));
        }

        // Kopie startuje přímo jako legacy (frmNoMDI); bez značky by ji App poslal přes EDLOADER a jeho brány.
        internal static ProcessStartInfo EdisonCopyStartInfo(string exe)
        {
            var info = new ProcessStartInfo(exe) { UseShellExecute = true };
            info.ArgumentList.Add(App.LoaderMarker);
            return info;
        }

        // Parita s legacy frmConnection: výchozí připojení řídí centrálně EDISONini.xml (jen pro čtení),
        // další si uživatel ukládá lokálně. Jiný server = nová instance shellu - session v procesu
        // je na jeden server (IMLRest.Server/Port jsou konstanty).
        private void ConnectToDatabase()
        {
            var window = new ConnectionWnd(_prefs, DefaultConnection());
            if (WindowNavigator.ShowDialog(window) != true || window.Picked is not { } picked) return;
            var exe = CurrentExecutable();
            if (exe.Length == 0) return;
            Process.Start(ConnectionLaunchInfo(exe, picked));
        }

        // Bez EDISONini.xml (instance spuštěná s argumenty připojení) je výchozí aktuální session.
        private ConnectionChoice DefaultConnection()
        {
            var ini = SettingsLoader.Load().Server;
            return ini.Host.Length > 0
                ? new ConnectionChoice(ini.Name.Length > 0 ? ini.Name : "EDISONini.xml", ini.Host, ini.Port, IsDefault: true)
                : new ConnectionChoice("Aktuální", Rest.Server, Rest.Port, IsDefault: true);
        }

        /// <summary>Nová instance shellu proti vybranému serveru; argumenty čte <see cref="ConnectionOverride.Parse"/>.</summary>
        internal static ProcessStartInfo ConnectionLaunchInfo(string exe, ConnectionChoice connection)
        {
            var info = new ProcessStartInfo(exe) { UseShellExecute = true };
            foreach (var arg in new ConnectionOverride(connection.Server, connection.RestPort).ToArguments())
                info.ArgumentList.Add(arg);
            return info;
        }

        private static string CurrentExecutable() => Process.GetCurrentProcess().MainModule?.FileName ?? "";

        private const string LegacyShellExe = "EDISONMAIN.exe";
        // reason=2 je EdisonLaunchReasonEnum.RunEdison; bez něj frmNoMDI ohlásí "nelze spustit
        // samostatně" a zavře se (kontrola, že shell spustil loader, ne uživatel ručně).
        private const string LegacyShellArgs = "reason=2";

        // Starý shell zůstává dostupný i po přepnutí EDLOADERu na EDISONMAIN2. Záměrně plain
        // Process.Start, ne WindowNavigator: ten child váže na KillOnExit job (starý shell by
        // umřel s tímhle), ukazuje ho ve "Spuštěné moduly" a dedupuje modeless launche.
        private static void OpenLegacyShell() => Process.Start(LegacyShellStartInfo());

        internal static ProcessStartInfo LegacyShellStartInfo() =>
            new(EDControls.EDViewModel.WindowNavigator.ResolveExecutablePath(LegacyShellExe))
            {
                Arguments = LegacyShellArgs,
                UseShellExecute = true,
            };

        // Tmavý režim - sdílená preference (%APPDATA%\m-line\common\theme.json) přes ThemeService;
        // přepne živě + propaguje do ostatních m-line procesů (TemplateDesigner) přes watcher.
        public bool IsDarkMode
        {
            get => EDControls.EDTheme.ThemeService.IsDark;
            set
            {
                if (EDControls.EDTheme.ThemeService.IsDark == value) return;
                EDControls.EDTheme.ThemeService.SetDarkMode(value);
                OnPropertyChanged();
            }
        }

        // Nastavení → Nastavení fontu - dialog v EDControls (uložení i aplikaci řeší sám).
        // Storage je shared font.json, takže ostatní m-line tooly (TemplateDesigner) přes
        // FileSystemWatcher okamžitě reagují na změnu.
        private static void ShowFontSettings()
            => FontSettingsWindow.ShowDialogFor(Application.Current?.MainWindow);

        private void OpenWebUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        // Týž text jako tlačítko „Informace o verzi" v panelu Podpora - jeden zdroj pravdy.
        private void ShowAppInfo() =>
            PopupDisplayer.Info(SupportActions.BuildVersionInfo(
                MLRest, _services.AppDisplayName, _services.AppVersion));

        // Stálý text bez timer refreshe - shell se typicky spouští denně, půlnoční rollover neřešíme.
        public string InformationText { get; } = BuildInformationText(DateOnly.FromDateTime(DateTime.Today));

        internal static string BuildInformationText(DateOnly today)
        {
            var text = $"Dnes je {today:dd.MM.yyyy}";
            var nameday = CzechNamedays.GetByDate(today);
            if (!string.IsNullOrEmpty(nameday)) text += $", svátek má {nameday}";
            return text;
        }

        // Initial = placeholder: WPF neotevře prázdný submenu, takže MouseEnter refresh by se nevyvolal.
        private IReadOnlyList<RunningModuleInfo> _runningModules = [EmptyPlaceholder];
        public IReadOnlyList<RunningModuleInfo> RunningModules
        {
            get => _runningModules;
            private set => Set(ref _runningModules, value);
        }

        private static readonly RunningModuleInfo EmptyPlaceholder =
            new("Žádné moduly nejsou spuštěné", () => { }, IsEnabled: false);

        private void RefreshRunningModules()
        {
            var modules = _getActiveModules();
            if (modules.Count == 0) { RunningModules = [EmptyPlaceholder]; return; }

            var labels = _trees.ModuleLabels;
            RunningModules = [.. modules.Select(m => m with { DisplayName = ResolveRunningLabel(m, labels) })];
        }

        /// <summary>
        /// Label do menu „Spuštěné moduly": jednoznačný popisek modulu ze stromu (týž jako v Používaných),
        /// jinak (modul mimo strom) vlastní titulek okna bez koncového <c>[IDFa]</c>. Titulek nestačí -
        /// dvě agendy mají běžně shodný (Zaměstnanci z MZDY2 vs. z Požadavků na volno). Firma je
        /// v položce zbytečná - při přepnutí firmy se otevřená okna zavírají.
        /// </summary>
        internal static string ResolveRunningLabel(RunningModuleInfo info, IReadOnlyDictionary<string, string> labels) =>
            labels.TryGetValue(ModuleTreeState.ModuleKey(info.ExeName, info.FormFqn, info.InputParam), out var label)
                ? label
                : StripIdfaSuffix(info.DisplayName);

        /// <summary>Odřízne jeden koncový <c>[...]</c> (suffix s firmou). Hranatá závorka uvnitř názvu zůstane.
        /// Suffix přidávají legacy formy samy (<c>clsForm.ShowIDFA</c>), okna EDISON2 ho nemají.</summary>
        internal static string StripIdfaSuffix(string title)
        {
            var trimmed = title.TrimEnd();
            if (!trimmed.EndsWith(']')) return title;
            var open = trimmed.LastIndexOf('[');
            return open <= 0 ? title : trimmed[..open].TrimEnd();
        }

        // Ctrl+W+W (dvojhmat) - skrytá zkratka pro M-line utility.
        // Stav perzistuje v UI threadu, není potřeba timeout - jakákoliv jiná klávesa čekání zruší.
        private bool _isWaitingForSecondKey;
        public bool IsWaitingForSecondKey
        {
            get => _isWaitingForSecondKey;
            private set
            {
                if (Set(ref _isWaitingForSecondKey, value))
                    OnPropertyChanged(nameof(SecondKeyHintText));
            }
        }
        public string SecondKeyHintText => IsWaitingForSecondKey ? "Stisknuto Ctrl+W - čekám na druhý stisk…" : "";

        public void HandleCtrlW()
        {
            if (IsWaitingForSecondKey)
            {
                IsWaitingForSecondKey = false;
                // UTILITIES.exe ukáže vlastní FrmPassword (legacy date-dependent vzorec) -
                // shell jen detekuje dvojhmat a spustí. Single source of truth pro formuli.
                WindowNavigator.ShowLegacy("UTILITIES.exe", "ALVA.UTILITIES.formUTILITIESMain");
            }
            else
            {
                IsWaitingForSecondKey = true;
            }
        }

        public void CancelSecondKeyWait()
        {
            if (IsWaitingForSecondKey) IsWaitingForSecondKey = false;
        }

        private void ChangePassword()
        {
            bool? changed = WindowNavigator.ShowDialog(
                new EDISONMAIN2.Windows.LoginWindow.ChangePasswordWindow(Rest, UserName));
            // Změna hesla za běhu zneplatní zapamatované (uložené při loginu se starým heslem).
            if (changed == true && _prefs.RememberedPasswordFor(Rest.Server, Rest.Port) != null)
            {
                _prefs.SetRememberedPassword(Rest.Server, Rest.Port, null);
                _prefs.Save();
            }
        }
    }
}
