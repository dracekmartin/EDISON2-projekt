using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AvalonDock.Themes;
using EDControls.EDTheme;
using EDControls.UserFonts;
using EDISONMAIN2.Desktop;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.EdisonMainWindow.SupportStrip;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro WinForms color picker).
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DataObject = System.Windows.DataObject;
using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;
using TreeView = System.Windows.Controls.TreeView;
using Size = System.Windows.Size;

namespace EDISONMAIN2
{
    /// <summary>
    /// Interaction logic for EdisonMainWindow.xaml
    /// </summary>
    public partial class EdisonMainWindow : Window
    {
        public EdisonMainWindow()
        {
            InitializeComponent();
            // Na notebooku s FHD a 150 % má pracovní plocha 1280 × 688 DIP: maximalizované okno by obsah
            // rozložilo na MinHeight a stavový řádek by skončil pod okrajem obrazovky.
            var min = MinSizeWithinWorkArea(new Size(MinWidth, MinHeight), SystemParameters.WorkArea.Size);
            MinWidth = min.Width;
            MinHeight = min.Height;
            PreviewKeyDown += OnPreviewKeyDown;
            // Výběr uzlu i Enter řešíme jednou na kartě, ne per strom - obojí bublá, takže
            // přidání záložky nevyžaduje další wiring. SelectedItem TreeView nelze bindovat.
            TreeCard.AddHandler(TreeView.SelectedItemChangedEvent,
                                new RoutedPropertyChangedEventHandler<object>(TreeSelectedItemChanged));
            TreeCard.AddHandler(KeyDownEvent, new System.Windows.Input.KeyEventHandler(TreeCard_KeyDown));
            Loaded += WireMiniAppHost;
            ContentRendered += OnContentRendered;
            // Drží plochu miniaplikací nad minimem při zúžení i snížení (splittery i okno) - viz handler.
            DockManager.SizeChanged += MiniAppArea_SizeChanged;
            DockTabHeader.Register();
            ApplyDockTheme();
            ThemeService.Applied += ApplyDockTheme;
            CompanyCombo.Loaded += (_, _) => FitCompanyCombo();
            FontService.Applied += FitCompanyCombo;
            Closing += OnShellClosing;
            Closed += OnShellClosed;
            Closed += (_, _) => ThemeService.Applied -= ApplyDockTheme;
            Closed += (_, _) => FontService.Applied -= FitCompanyCombo;
            // DataContext nastavuje BuildShellWindow hned po ctoru, pruh se tedy naplní ještě před zobrazením.
            DataContextChanged += (_, e) =>
            {
                if (e.OldValue is EdisonMainViewModel old) old.PropertyChanged -= OnShellPropertyChanged;
                if (e.NewValue is EdisonMainViewModel vm)
                {
                    vm.PropertyChanged += OnShellPropertyChanged;
                    FillSupportStrip(vm.SupportWidgetUrl);
                }
            };
            Closed += (_, _) =>
            {
                if (DataContext is EdisonMainViewModel vm) vm.PropertyChanged -= OnShellPropertyChanged;
                _supportWidget?.Dispose();
            };
        }

        private IDisposable? _supportWidget;
        private string? _supportWidgetUrl;

        private void OnShellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EdisonMainViewModel.SupportWidgetUrl) && sender is EdisonMainViewModel vm)
                FillSupportStrip(vm.SupportWidgetUrl);
            if (e.PropertyName == nameof(EdisonMainViewModel.Companies))
                FitCompanyCombo();
        }

        internal static Size MinSizeWithinWorkArea(Size min, Size workArea) =>
            new(Math.Min(min.Width, workArea.Width), Math.Min(min.Height, workArea.Height));

        // Šířka přepínače firem je pevná podle nejdelší firmy: rostla by s psaným filtrem i se
        // šipkami a posouvala menu za sebou. Strop drží menu na jednom řádku v minimální šířce
        // okna při výchozím písmu; zvětšené písmo ho zalomit může.
        internal const double CompanyComboMaxWidth = 360;
        // Caret a vnitřní okraj TextBoxu, které rozdíl šířky comba a editačního pole nezahrne.
        private const double CompanyComboTextSlack = 6;

        private void FitCompanyCombo()
        {
            if (DataContext is not EdisonMainViewModel vm
                || CompanyCombo.Template?.FindName("PART_EditableTextBox", CompanyCombo) is not System.Windows.Controls.TextBox edit
                || edit.ActualWidth <= 0)
                return;
            var typeface = new System.Windows.Media.Typeface(CompanyCombo.FontFamily, CompanyCombo.FontStyle,
                CompanyCombo.FontWeight, CompanyCombo.FontStretch);
            double pixelsPerDip = System.Windows.Media.VisualTreeHelper.GetDpi(CompanyCombo).PixelsPerDip;
            double chrome = CompanyCombo.ActualWidth - edit.ActualWidth + CompanyComboTextSlack;
            CompanyCombo.Width = CompanyComboWidth(vm.Companies.Select(c => c.DisplayName),
                text => new System.Windows.Media.FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight, typeface, CompanyCombo.FontSize,
                    System.Windows.Media.Brushes.Black, pixelsPerDip).WidthIncludingTrailingWhitespace,
                chrome);
        }

        internal static double CompanyComboWidth(IEnumerable<string> texts, Func<string, double> measure, double chrome) =>
            Math.Min(texts.Select(measure).DefaultIfEmpty(0).Max() + chrome, CompanyComboMaxWidth);

        // Widget v pruhu podpory. Drží statické Font/Theme eventy, takže se musí uklidit se
        // zavřením okna - jinak přežije shell. Nenačtení widgetu shell neblokuje (dekorace).
        // Stejná URL (notifikace bez změny firmy či uživatele) widget nepřestavuje.
        private void FillSupportStrip(string url)
        {
            if (url == _supportWidgetUrl) return;
            _supportWidgetUrl = url;
            _supportWidget?.Dispose();
            _supportWidget = SupportWidgetHost.Fill(SupportStrip, url,
                "Informační panel podpory - novinky a upozornění IT pro uživatele");
        }

        // Zavření shellu ukončí aplikaci (ShutdownMode=OnMainWindowClose) i všechny agendy,
        // proto se ptáme stejně jako u přepnutí firmy. Programové Shutdown si prompt potlačí ve VM.
        private void OnShellClosing(object? sender, CancelEventArgs e)
        {
            if (DataContext is EdisonMainViewModel vm && !vm.ConfirmShellClose())
                e.Cancel = true;
        }

        private void OnShellClosed(object? sender, EventArgs e)
        {
            if (DataContext is EdisonMainViewModel vm) vm.OnShellClosed();
        }

        // Vestavěný generic theme AvalonDocku má chrome (taby, bordery, pozadí dokumentů) natvrdo
        // světlý a EDTheme tokeny nečte - dark mode řeší výměna celého Theme; VS2013 dark paleta
        // odpovídá EDTheme dark (#1E1E1E/#252526). Guard: Applied chodí i bez změny theme, dotažení
        // tab stripu na paletu ale musí proběhnout vždy.
        private void ApplyDockTheme()
        {
            DockThemeBrushes.Apply(DockManager);
            bool dark = ThemeService.Current == AppTheme.Dark;
            if (dark ? DockManager.Theme is Vs2013DarkTheme : DockManager.Theme is Vs2013LightTheme) return;
            DockManager.Theme = dark ? new Vs2013DarkTheme() : new Vs2013LightTheme();
        }

        // Po prvním vykreslení (WPF analog legacy frmMain_Shown) načti session state a otevři
        // uživatelův výchozí okno. ContentRendered fíruje jednou po prvním paintu; okno se tedy
        // ukáže hned a stromy dorazí pod overlayem. Pořadí je invariant - výchozí okno se hledá
        // ve stromu, takže init musí dobíhnout první.
        private async void OnContentRendered(object? sender, System.EventArgs e)
        {
            ContentRendered -= OnContentRendered;
            // Až teď je okno v cílové (maximalizované) velikosti a restore šířky stromu proběhl -
            // od teď smí šířkový clamp zasahovat (dřív by přepsal restorovanou šířku).
            _layoutReady = true;
            if (DataContext is not EdisonMainViewModel vm) return;
            try
            {
                await vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                // async void z eventu - bez catch by výjimka letěla na DispatcherUnhandledException.
                vm.PopupDisplayer.Error(ex);
                return;
            }
            // Novinky shellu před výchozím oknem - modální, otevírání pokračuje až po zavření.
            await vm.ShowShellChangelogIfAnyAsync();
            vm.OpenStartupWindowIfAny();
        }

        // MiniAppManager (ve VM) potřebuje DockingManager z view → host injektneme po načtení okna,
        // kdy už je DataContext nastaven (BuildShellWindow ho nastaví po konstruktoru). Jen jednou.
        private void WireMiniAppHost(object sender, RoutedEventArgs e)
        {
            Loaded -= WireMiniAppHost;
            if (DataContext is EdisonMainViewModel vm)
            {
                // Před AttachMiniAppHost - ten spouští první restore, který už rozměry panelů aplikuje.
                vm.AttachTreeLayout(
                    () => TreeColumn.ActualWidth,
                    w => TreeColumn.Width = new GridLength(w));
                vm.AttachSupportLayout(
                    () => SupportRow.ActualHeight,
                    h => SupportRow.Height = new GridLength(h));
                vm.AttachMiniAppHost(new AvalonDockHost(DockManager));
            }
        }

        // Minimální rozměry plochy miniaplikací (aby ji strom ani pruh podpory nezakryly).
        // Výška = dvě řady dlaždic Plochy (TileGridPanel.CellHeight 84) + pruh panelu a tab strip docku;
        // na star řádek MinHeight nesmí (přetékalo by z okna), proto je to konstanta tady, ne v XAML.
        private const double MinMiniAppAreaWidth = 540;
        private const double MinMiniAppAreaHeight = 220;
        private bool _layoutReady;

        // Limity dělení sloupce - podrobnosti u ShellSplitLimits. Strop výšky je nedestruktivní,
        // takže smí běžet i během úvodního layoutu; šířkový clamp by v něm zašlapal restore.
        private void MiniAppArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ShellSplitLimits.ApplySupportCeiling(SupportRow, SupportSplitterRow,
                                                 ContentRow.ActualHeight, MinMiniAppAreaHeight);
            if (!_layoutReady) return;
            ShellSplitLimits.ClampTreeWidth(TreeColumn, DockManager.ActualWidth, MinMiniAppAreaWidth);
        }

        // Single-click ve stromu: složka toggle expand, modul spustí. Event bubluje přes
        // vnořené TreeViewItemy - Handled=true zastaví rodiče, aby klik na modul
        // zároveň nesbalil jeho složku. Chevron neprojde (ToggleButton si MouseUp zpracuje sám).
        private void TreeItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = (TreeViewItem)sender;
            e.Handled = true;
            if (!Gesture.TakeClick(item.DataContext)) return;
            switch (item.DataContext)
            {
                case ModuleFamily:
                    item.IsExpanded = !item.IsExpanded;
                    break;
                case ExecutableModule module:
                    OpenModule(module);
                    break;
            }
        }

        // Tažení modulu ze stromu na plochu (přidání zástupce). Gesto sdílí strom, Používané i plocha.
        private static DesktopDragGesture Gesture => DesktopDragGesture.Shared;

        // Tunneling: okno dostane stisk dřív než položka, stisk mimo položky tak zahodí ten předchozí.
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Gesture.Reset();
            base.OnPreviewMouseLeftButtonDown(e);
        }

        private void TreeItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = (TreeViewItem)sender;
            Gesture.Press(e.GetPosition(null), item.DataContext as ExecutableModule, item.DataContext);
        }

        private void TreeItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (Gesture.Move<ExecutableModule>(e.GetPosition(null), e.LeftButton == MouseButtonState.Pressed) is { } module)
                DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(DesktopDragFormats.FromTree, module), DragDropEffects.Copy);
        }

        // Pravý klik defaultně selekci nemění → kontextové menu by se vizuálně nevztahovalo
        // ke kliknuté položce. Bublání: vnitřní item se označí první, Handled zastaví rodiče.
        // ContextMenu se otevírá až na MouseUp, takže Handled ho neblokuje.
        private void TreeItem_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ((TreeViewItem)sender).IsSelected = true;
            e.Handled = true;
        }

        // Výběr uzlu v kterémkoli stromě → VM.SelectedModule.
        private void TreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is EdisonMainViewModel vm)
                vm.SelectedModule = e.NewValue as IModule;
        }

        // Enter na fokusovaném uzlu = totéž co klik (modul spustí, složku přepne). Bere se
        // DataContext fokusovaného kontejneru, takže jeden handler obslouží stromy i Používané.
        private void TreeCard_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            switch ((e.OriginalSource as FrameworkElement)?.DataContext)
            {
                case ModuleFamily family:
                    family.IsExpanded = !family.IsExpanded;
                    e.Handled = true;
                    break;
                case ExecutableModule module:
                    OpenModule(module);
                    e.Handled = true;
                    break;
            }
        }

        // Enter v poli hledání označí první nález a dá mu fokus ve stromu; spustí ho až druhý Enter
        // (TreeCard_KeyDown výš) - jistota proti nechtěnému startu agendy při obecném hledaném textu.
        private void TreeSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || DataContext is not EdisonMainViewModel vm) return;
            if (vm.FirstSearchResult() is not { } hit || ActiveTreeView() is not { } tree) return;
            if (ContainerFor(tree, hit) is not { } item) return;

            item.IsSelected = true;
            item.BringIntoView();
            item.Focus();
            e.Handled = true;
        }

        // Klíče záložek drží VM.TreeTabs; stromy jsou pojmenovaní sourozenci v kartě (Používané
        // je ListBox mimo filtr hledání, proto null).
        private TreeView? ActiveTreeView() =>
            (DataContext as EdisonMainViewModel)?.SelectedTab.Key switch
            {
                "main"      => MainMenuTreeView,
                "system"    => SystemAdminTreeView,
                "partners"  => PartnerSolutionsTreeView,
                "favorites" => FavoritesTreeView,
                _           => null,
            };

        // Kontejner uzlu se hledá shora dolů - potomci vzniknou až když je rodič rozbalený
        // a proběhl jeho layout (hledání rodiče na cestě rozbalí, generátor je ale líný).
        private static TreeViewItem? ContainerFor(ItemsControl parent, IModule target)
        {
            parent.UpdateLayout();
            if (parent.ItemContainerGenerator.ContainerFromItem(target) is TreeViewItem direct) return direct;
            foreach (var family in parent.Items.OfType<ModuleFamily>())
                if (parent.ItemContainerGenerator.ContainerFromItem(family) is TreeViewItem container
                    && ContainerFor(container, target) is { } hit) return hit;
            return null;
        }

        private void UsedModuleItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
            Gesture.Press(e.GetPosition(null), null, ((ListBoxItem)sender).DataContext);

        private void UsedModuleItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (((ListBoxItem)sender).DataContext is ExecutableModule module && Gesture.TakeClick(module))
                OpenModule(module);
        }

        // CanExecute guard - AsyncRelayCommand během běhu vrací false, brání dvojímu spuštění.
        private void OpenModule(ExecutableModule module)
        {
            if (DataContext is EdisonMainViewModel vm && vm.OpenModuleCommand.CanExecute(module))
                vm.OpenModuleCommand.Execute(module);
        }

        // Ctrl+W+W (dvojhmat) - detekce. PreviewKeyDown (ne KeyDown) - chytá klávesy předtím,
        // než je child controls (menu, TextBox) zpracují. Bez Preview by Ctrl+W v Menu
        // zavřel popup místo aby spustil dvojhmat.
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not EdisonMainViewModel vm) return;

            bool isCtrlW = e.Key == Key.W && (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (isCtrlW)
            {
                vm.HandleCtrlW();
                e.Handled = true;
            }
            else
            {
                // Ne-Ctrl+W klávesa → zrušit čekání (uživatel pokračuje jinde).
                // Pouze pokud se právě čeká na druhý stisk, jinak žádná akce.
                vm.CancelSecondKeyWait();

                if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    TreeSearchBox.Focus();
                    TreeSearchBox.SelectAll();
                    e.Handled = true;
                }
            }
        }
    }
}
