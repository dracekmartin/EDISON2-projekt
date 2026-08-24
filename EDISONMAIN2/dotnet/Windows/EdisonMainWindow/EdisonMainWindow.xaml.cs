using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AvalonDock.Themes;
using EDControls.EDTheme;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro WinForms color picker).
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DataObject = System.Windows.DataObject;
using Point = System.Windows.Point;
using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;
using TreeView = System.Windows.Controls.TreeView;

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
            PreviewKeyDown += OnPreviewKeyDown;
            // Výběr uzlu i Enter řešíme jednou na kartě, ne per strom - obojí bublá, takže
            // přidání záložky nevyžaduje další wiring. SelectedItem TreeView nelze bindovat.
            TreeCard.AddHandler(TreeView.SelectedItemChangedEvent,
                                new RoutedPropertyChangedEventHandler<object>(TreeSelectedItemChanged));
            TreeCard.AddHandler(KeyDownEvent, new System.Windows.Input.KeyEventHandler(TreeCard_KeyDown));
            Loaded += WireMiniAppHost;
            ContentRendered += OnContentRendered;
            // Drží plochu miniaplikací nad minimem při zúžení (splitter i okno) - viz handler.
            DockManager.SizeChanged += MiniAppArea_SizeChanged;
            ApplyDockTheme();
            ThemeService.Applied += ApplyDockTheme;
            Closing += OnShellClosing;
            Closed += (_, _) => ThemeService.Applied -= ApplyDockTheme;
        }

        // Zavření shellu ukončí aplikaci (ShutdownMode=OnMainWindowClose) i všechny agendy,
        // proto se ptáme stejně jako u přepnutí firmy. Programové Shutdown si prompt potlačí ve VM.
        private void OnShellClosing(object? sender, CancelEventArgs e)
        {
            if (DataContext is EdisonMainViewModel vm && !vm.ConfirmShellClose())
                e.Cancel = true;
        }

        // Vestavěný generic theme AvalonDocku má chrome (taby, bordery, pozadí dokumentů) natvrdo
        // světlý a EDTheme tokeny nečte - dark mode řeší výměna celého Theme; VS2013 dark paleta
        // odpovídá EDTheme dark (#1E1E1E/#252526). Guard: Applied chodí i bez změny theme.
        private void ApplyDockTheme()
        {
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
            // od teď smí SizeChanged clamp zasahovat (dřív by přepsal restorovanou šířku).
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
                // Před AttachMiniAppHost - ten spouští první restore, který už šířku stromu aplikuje.
                vm.AttachTreeLayout(
                    () => TreeColumn.ActualWidth,
                    w => TreeColumn.Width = new GridLength(w));
                vm.AttachMiniAppHost(new AvalonDockHost(DockManager));
            }
        }

        // Minimální šířka plochy miniaplikací (aby ji strom nezakryl).
        private const double MinMiniAppAreaWidth = 540;
        private const double MinTreeWidth = 150;
        private bool _layoutReady;

        // Post-layout korekce: když cokoli (drag splitteru, zúžení okna) zmenší plochu docku pod
        // minimum, o schodek zúžíme strom (až po jeho vlastní min). Nezávisí na chování GridSplitteru
        // (ten aplikuje posun až PO DragDelta, takže přímý clamp v handleru přepíše). Guard _layoutReady
        // brání zásahu během úvodního layoutu, kdy ještě neproběhl restore šířky stromu.
        private void MiniAppArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_layoutReady) return;
            double deficit = MinMiniAppAreaWidth - DockManager.ActualWidth;
            if (deficit <= 0) return;
            double treeTarget = Math.Max(MinTreeWidth, TreeColumn.ActualWidth - deficit);
            if (treeTarget < TreeColumn.ActualWidth)
                TreeColumn.Width = new GridLength(treeTarget);
        }

        // Single-click ve stromu: složka toggle expand, modul spustí. Event bubluje přes
        // vnořené TreeViewItemy - Handled=true zastaví rodiče, aby klik na modul
        // zároveň nesbalil jeho složku. Chevron neprojde (ToggleButton si MouseUp zpracuje sám).
        private void TreeItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = (TreeViewItem)sender;
            e.Handled = true;
            // Právě proběhl drag modulu → nespouštět ho jako klik.
            if (_treeDragActive) { _treeDragActive = false; return; }
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

        private Point _treeDragStart;
        private ExecutableModule? _treeDragModule;
        private bool _treeDragActive;

        // Zapamatuje modul + bod stisku (tunneling: rodič-family nastaví null, list-modul přepíše).
        private void TreeItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _treeDragStart = e.GetPosition(null);
            _treeDragModule = ((TreeViewItem)sender).DataContext as ExecutableModule;
        }

        // Po překročení prahu spustí drag modulu (do miniaplikace Oblíbené). Práh odliší od kliku.
        private void TreeItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _treeDragModule is null) return;
            var p = e.GetPosition(null);
            if (Math.Abs(p.X - _treeDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(p.Y - _treeDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            var module = _treeDragModule;
            _treeDragModule = null;
            _treeDragActive = true;
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(ModuleDrag.FromTree, module), DragDropEffects.Copy);
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
        // Přes index, ne ContainerFromItem: moduly jsou record s hodnotovou rovností, takže
        // dva shodně vyplněné uzly by si kontejnery prohodily.
        private static TreeViewItem? ContainerFor(ItemsControl parent, IModule target)
        {
            parent.UpdateLayout();
            for (int i = 0; i < parent.Items.Count; i++)
            {
                if (parent.ItemContainerGenerator.ContainerFromIndex(i) is not TreeViewItem container) continue;
                if (ReferenceEquals(parent.Items[i], target)) return container;
                if (parent.Items[i] is ModuleFamily && ContainerFor(container, target) is { } hit) return hit;
            }
            return null;
        }

        private void UsedModuleItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (((ListBoxItem)sender).DataContext is ExecutableModule module)
                OpenModule(module);
        }

        // CanExecute guard - AsyncRelayCommand během běhu vrací false, brání dvojímu spuštění.
        private void OpenModule(ExecutableModule module)
        {
            if (DataContext is EdisonMainViewModel vm && vm.OpenModuleCommand.CanExecute(module))
                vm.OpenModuleCommand.Execute(module);
        }

        // Ctrl+W+W chord detection. PreviewKeyDown (ne KeyDown) - chytá klávesy předtím,
        // než je child controls (menu, TextBox) zpracují. Bez Preview by Ctrl+W v Menu
        // zavřel popup místo aby spustil chord.
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
                // Ne-Ctrl+W klávesa → reset chord (uživatel pokračuje jinde).
                // Pouze pokud chord právě čeká, jinak žádná akce.
                vm.ResetChord();

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
