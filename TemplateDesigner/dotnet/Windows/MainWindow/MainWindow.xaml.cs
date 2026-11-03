using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SkiaSharp;
using EDControls.EDInputForm;
using EDControls.EDTheme;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Config;
using TemplateDesigner.Serialization;
using ALWA.SYS;

namespace TemplateDesigner;

/// <summary>
/// Hlavní okno návrháře. Tady je stav okna, konstruktor a rozložení panelů; zbytek je v částech
/// <c>MainWindow.&lt;Oblast&gt;.cs</c>: Render (plátno a náhled), Mouse, Keyboard, InlineTextEdit,
/// TableCells (psaní do pevné tabulky), Palette, Sections, Menus, AgendaParams, Properties, StartPanel.
/// </summary>
public partial class MainWindow : Window
{
    // ── ViewModel ─────────────────────────────────────────────────────────────
    private TemplateDesignerViewModel? _vm;

    // ── Canvas / preview ──────────────────────────────────────────────────────
    private DesignerCanvas?         _canvas;
    private PreviewCanvas?          _previewCanvas;
    private DateTime                _nahledPosunulProgram;
    private ElementPropertiesWindowViewModel? _propsVm;

    // Zoom max omezen na 500% - při vyšším zoomu má SKElement >20 MP, což způsobuje
    // pomalé buffer uploads v SkiaSharp.Views.WPF (full surface kopie každý frame).
    // Pro skutečně vysoké zoomy bude potřeba virtualizace rendering (viz roadmap).
    private static readonly float[] ZoomLevels = [0.5f, 0.75f, 1.0f, 1.5f, 2.0f, 3.0f, 4.0f, 5.0f];

    // ─────────────────────────────────────────────────────────────────────────
    // Constructors
    // ─────────────────────────────────────────────────────────────────────────

    // ── Per-user preferences (per-user × IDFa state from Caché) ──
    private Services.TdPreferencesService? _preferences;

    /// <summary>WPF designer constructor - do not use at runtime.</summary>
    public MainWindow() { InitializeComponent(); }

    /// <summary>Runtime constructor. mlRest = null for standalone mode; preferences = null disables persist.</summary>
    public MainWindow(MLRest? mlRest,
                      Services.TdPreferencesService? preferences = null,
                      IReadOnlyList<Serialization.AgendaRegistryEntry>? agendas = null,
                      Config.LaunchInput? launchInput = null)
    {
        _preferences = preferences;
        _vm         = new TemplateDesignerViewModel(mlRest, preferences, agendas, launchInput?.AgendaCode,
                                                    startupJenModul: launchInput is { Values.Count: 0 });
        DataContext = _vm;
        InitializeComponent();
        BuildElementPalette();

        // Ručně, protože TD okna neotvírá přes WindowNavigator - bez tohohle volání by se
        // pozice a velikost hlavního okna nepersistovaly (musí být před Show()).
        _vm.AttachToWindow(this);

        // AI generátor šablon zatím jen pro vývojáře (lokální dev klíč) - před produkcí skrytý.
        MenuAiAssistant.Visibility = MenuAiSep.Visibility =
            string.IsNullOrEmpty(AppSettings.Current?.AiAssistant.AnthropicApiKey)
                ? Visibility.Collapsed : Visibility.Visible;

        // DEV nástroje (přepínač firem, import z PDF) - každý vlastním klíčem v lokálním configu,
        // který se do publish nekopíruje; v produkci zůstávají skryté.
        BuildDevFirmMenu();

        // Aplikuj UI-side prefs (splittery, tab, preview pane viditelnost) až po InitializeComponent.
        ApplyUiPreferences();

        // V konstruktoru ještě není layout, takže se uložená šířka náhledu nemá proti čemu ořezat -
        // první SizeChanged je ta chvíle. Drží to i přetažení okna na menší monitor za běhu.
        MainGrid.SizeChanged += (_, _) => OrizniSirkuNahledu();
        // Levý dělič bere šířku plátnu, takže po něm zbude na náhled míň - bez přepočtu by mu
        // zůstalo povolené staré maximum a panel by se zase dostal ven.
        SectionsSplitter.DragDelta += (_, _) => OrizniSirkuNahledu();

        _canvas        = new DesignerCanvas(_vm.State);
        _previewCanvas = new PreviewCanvas(_vm.State) { PamatovatSazbu = true };   // zapomíná v InvalidatePreview
        _previewCanvas.OverflowChanged = h => _vm?.NastavHlaseni(TemplateDesignerViewModel.ZdrojHlaseni.Preteceni, h);

        _previewCanvas.PocetStranSazbyChanged = pocet =>
        {
            if (_vm is not null) _vm.PocetStranSazby = pocet;
        };

        _previewCanvas.OriznuteBunkyChanged = h => _vm?.NastavHlaseni(TemplateDesignerViewModel.ZdrojHlaseni.Orez, h);
        _previewCanvas.UzkeSloupceChanged = h => _vm?.NastavHlaseni(TemplateDesignerViewModel.ZdrojHlaseni.Sloupce, h);
        _previewCanvas.SazbaChanged = h => _vm?.NastavHlaseni(TemplateDesignerViewModel.ZdrojHlaseni.Sazba, h);
        _vm.RequestSelectElement = VyberPrvekZHlaseni;

        // Wire SkiaSharp (must be code-behind - not in XAML for net9+)
        skCanvas.PaintSurface        += OnPaintSurface;
        skPreview.PaintSurface       += OnPreviewPaintSurface;
        // Změna velikosti plochy (zoom, okno) data ani šablonu nemění - jen překreslit.
        skPreview.SizeChanged        += (_, _) => PrekresliNahled();
        skCanvas.MouseLeftButtonDown  += OnCanvasMouseDown;
        skCanvas.MouseMove            += OnCanvasMouseMove;
        skCanvas.MouseLeftButtonUp    += OnCanvasMouseUp;
        skCanvas.MouseRightButtonUp   += OnCanvasRightClick;
        CanvasScrollViewer.PreviewMouseWheel += OnCanvasScrollWheel;
        PreviewScrollViewer.PreviewMouseWheel += OnPreviewScrollWheel;
        // Repaint canvas po scroll - viewport culling potřebuje aktuální viditelnou oblast
        CanvasScrollViewer.ScrollChanged += (_, _) => skCanvas.InvalidateVisual();

        // Předlohy stránek (`B184`): přepnutí v návrhu posune náhled na první stránku, která
        // z předlohy vznikla; posun náhledu přepne předlohu v návrhu. Posun, který udělal program,
        // se chvíli ignoruje - jinak by se oba směry přetahovaly.
        _vm.PredlohaZmenena = (predloha, zNahledu) =>
        {
            CanvasScrollViewer.ScrollToVerticalOffset(0);
            if (!_vm.NahledVsechStranek)
            {
                // Náhled ukazuje jen stránky otevřené předlohy - po přepnutí jiné, jinak vysoký.
                UpdatePreviewSize();
                InvalidatePreview();
                PreviewScrollViewer.ScrollToVerticalOffset(0);
                return;
            }
            UpdatePreviewSize();
            InvalidatePreview();
            if (zNahledu || _previewCanvas?.TopPredlohy(predloha) is not float y) return;
            _nahledPosunulProgram = DateTime.UtcNow;
            PreviewScrollViewer.ScrollToVerticalOffset(Math.Max(0, y - 20));
        };
        PreviewScrollViewer.ScrollChanged += (_, e) =>
        {
            if (_vm is null || !_vm.MaPredlohy || !_vm.NahledVsechStranek || e.VerticalChange == 0) return;
            if ((DateTime.UtcNow - _nahledPosunulProgram).TotalMilliseconds < 400) return;
            // Rozhoduje stránka v horní třetině okna náhledu - tu má uživatel před očima.
            float y = (float)(PreviewScrollViewer.VerticalOffset + PreviewScrollViewer.ViewportHeight / 3);
            if (_previewCanvas?.PredlohaNa(y) is int p && p != _vm.OtevrenaPredloha)
                _vm.OtevriPredlohu(p, zNahledu: true);
        };

        // Plocha kolem stránky: WPF (pozadí ScrollViewerů) i Skia (canvas.Clear) musí mít
        // shodnou barvu, jinak je na hranici plátna/náhledu vidět šev. XAML bere token
        // EDTheme.Bg.Canvas přímo, Skia stranu naplníme odsud (Designer/ nezná WPF).
        ApplyCanvasThemeColor();
        ThemeService.Applied += OnThemeApplied;

        // Wire VM callbacks
        // UpdateCanvasSize (ne jen InvalidateVisual) - výška plátna roste s obsahem, takže po každé
        // změně (přidání/smazání/resize sekce či elementu) je třeba přepočítat velikost plátna.
        // Nabídka stránek dat závisí i na obsahu šablony (pole ve závorkách) - přepočet s náhledem.
        _vm.RequestCanvasRefresh         = () => { UpdateCanvasSize(); InvalidatePreview(); _vm.ObnovStrankovaniDat(); };
        _vm.RequestPreviewRefresh        = () => { InvalidatePreview(); _vm.ObnovStrankovaniDat(); };
        // Titulek se mění mj. při změně agendy → stejný signál řídí i kontextovou paletu JŘ elementů
        _vm.RequestTitleUpdate           = () => { UpdateTitle(); UpdatePaletteForAgenda(); };
        _vm.RequestCanvasSizeUpdate      = () => { UpdateCanvasSize(); UpdateTitle(); UpdatePaletteForAgenda(); };
        _vm.RequestPropertiesPanelUpdate = UpdatePropertiesPanel;
        _vm.RequestGridSizeUiSync        = SyncGridSizeComboBox;
        // Pager dat: po výměně ItemsSource WPF umí spolknout SelectedIndex z bindingu, případně
        // nechat zobrazovací box prázdný i při formálně správném indexu → deferovaně a BEZ podmínky
        // znovu vybrat položku přes SelectedItem (samotné porovnání indexů prázdný box neodhalí).
        _vm.RequestPreviewPagerSync      = () => Dispatcher.BeginInvoke(new Action(() =>
        {
            int idx = _vm.RealPreviewIndex;
            if (idx >= 0 && idx < CmbPreviewPage.Items.Count)
                CmbPreviewPage.SelectedItem = CmbPreviewPage.Items[idx];
        }), System.Windows.Threading.DispatcherPriority.Background);
        _vm.RequestParamsFormReload      = ReloadParamsTabForm;
        _vm.RequestParamsFieldSync       = SyncParamsTabField;
        _vm.RequestParamsVisibilityRefresh = ApplyParamsTabVisibility;
        // Chyba čtení z Caché může přijít z background threadu (Task.Run) → marshal na UI thread.
        _vm.RequestReadErrorStatus       = msg => Dispatcher.Invoke(() => _vm.StatusText = msg);
        // Modální/souborové dialogy + schránka - WPF drží tato služba, ne VM (R4)
        _vm.Dialogs = new WpfDesignerDialogService(this);

        // Wire section events
        _vm.SectionsRebuilt += RestoreSectionSelection;
        SectionVM.ShowPropertiesGlobal = s => OpenPropertiesForSection(s);
        SectionVM.SaveTemplateGlobal   = s => SaveSectionAsTemplate(s);

        // Properties VM - sdílená instance, dialog se vytvoří pokaždé nový při otevření
        _propsVm = new ElementPropertiesWindowViewModel(
            _vm.State,
            () => { skCanvas.InvalidateVisual(); InvalidatePreview(); },
            getAvailableFields:    _vm.GetAvailableFields,
            getAvailableGroups:    _vm.GetAvailableGroups);

        // Callback: otevření DepartureGroupEditorDialog z inline panelu i modálního dialogu
        _propsVm.RequestOpenDepartureGridDialog = el => OpenDepartureGridDialog(el);
        _propsVm.RequestOpenStopColumnsDialog = el => OpenStopColumnsDialog(el);
        _propsVm.RequestOpenDynamicTableDialog = el => OpenDynamicTableDialog(el);
        _propsVm.RequestPickFile = (title, filter) =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = title, Filter = filter };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        };

        // EDInputForm tlačítka v inline panelu (např. „Zvýraznění řádků…")
        InlineElementForm.ButtonClicked += OnInlineElementButtonClicked;

        // Inline properties panel - SetContext s execute + invalidate callbacky
        InlinePropertiesPanel.SetContext(
            cmd => _vm.Execute(cmd),
            () => { skCanvas.InvalidateVisual(); InvalidatePreview(); });
        InlinePropertiesPanel.ShowMessage("Vyberte prvek na plátně");

        // Template tree panel - SetContext
        TreePanel.SetContext(
            _vm,
            OpenPropertiesForElement,
            OpenPropertiesForSection,
            RestoreSectionSelection);

        // Initial state
        _vm.RefreshFieldsList();
        UpdateTitle();
        UpdatePaletteForAgenda();
        UpdateCanvasSize();
        UpdatePropertiesPanel();

        if (AppSettings.Current?.AiAssistant.Enabled == false)
            MenuAiAssistant.Visibility = Visibility.Collapsed;

        this.PreviewKeyDown += OnWindowKeyDown;
        this.PreviewTextInput += OnWindowTextInput;   // psaní do vybrané buňky pevné tabulky

        // Field drag from schema tree - Window-level events for cross-panel drag
        SchemaTree.PreviewMouseLeftButtonDown += SchemaTree_PreviewMouseDown;
        SchemaTree.PreviewMouseMove           += SchemaTree_PreviewMouseMove;
        this.PreviewMouseMove                 += OnWindowPreviewMouseMove;
        this.PreviewMouseLeftButtonUp         += OnWindowPreviewMouseUp;

        // Section drag reorder in left panel
        SectionsList.PreviewMouseLeftButtonDown += SectionsList_PreviewMouseDown;
        SectionsList.PreviewMouseMove           += SectionsList_PreviewMouseMove;
        SectionsList.PreviewMouseLeftButtonUp   += SectionsList_PreviewMouseUp;
        SectionsList.MouseLeave                 += SectionsList_DragCancel;

        // Per-user prefs save on close + Recent Files menu populate on open
        this.Closing += OnWindowClosing;
        MenuRecentFiles.SubmenuOpened += (_, _) => RebuildRecentFilesMenu();
        RebuildRecentFilesMenu();

        // Sync nebindovaných UI elementů (CmbGridSize) s loaded GridSize. VM ctor
        // už nastavil State.GridSize, ale RequestGridSizeUiSync callback v tu chvíli
        // ještě nebyl napojený - explicit sync teď, kdy už je vše drátované.
        SyncGridSizeComboBox(_vm.State.GridSize);
        SyncSnapGapComboBox(_vm.State.SnapGapMm);

        // Pokud LoadAsync nestihl dokončit před Show() (cold-start Caché HTTP trvá 2-3s),
        // subscribe na Loaded event - handler dispatchne re-apply na UI thread.
        // Subscribe-after-loaded case (warm cache) řeší Loaded event accessor - invoke immediately.
        if (_preferences is not null)
            _preferences.Loaded += OnPreferencesLoaded;

        // Spuštění s vybranou linkou (porovnání linek v modulu JŘ) otevře šablonu až nad hotovým
        // oknem - čte z databáze a chybu musí mít komu ukázat.
        if (launchInput is not null && _vm.ShouldOpenFromLaunch(launchInput))
        {
            RoutedEventHandler? open = null;
            open = async (_, _) =>
            {
                Loaded -= open;
                await _vm.OpenFromLaunchAsync(launchInput);
            };
            Loaded += open;
        }
        else
        {
            // Bez linky by uživatel viděl jen prázdnou stránku - úvodní obrazovka mu řekne, kudy dál.
            RoutedEventHandler? start = null;
            start = async (_, _) =>
            {
                Loaded -= start;
                await _vm.ShowStartPanelAsync();
            };
            Loaded += start;
        }

        // Šablony od jiných firem (`B189`): pruh se doplní, až server odpoví, start na něj nečeká.
        RoutedEventHandler? received = null;
        received = async (_, _) =>
        {
            Loaded -= received;
            await _vm.CheckReceivedTemplatesAsync();
        };
        Loaded += received;
    }

    private void OnPreferencesLoaded()
    {
        // Loaded event fires from threadpool - dispatch to UI thread.
        Dispatcher.BeginInvoke(async () =>
        {
            if (_vm is null || _preferences is null) return;
            try
            {
                _vm.ApplyLoadedPreferences();
                ApplyUiPreferences();
                SyncGridSizeComboBox(_vm.State.GridSize);
                SyncSnapGapComboBox(_vm.State.SnapGapMm);
                RebuildRecentFilesMenu();
                // Pokud byla šablona otevřena před dokončením LoadAsync, PrintParams nebyly aplikovány -
                // dohnat to teď. Celou trojici (per-agenda → šablona → UI), ne jen načtení hodnot:
                // jinak jdou hodnoty do GetData, ale ovladače v UI zůstanou na defaultech.
                await _vm.ApplyPersistedPrintParamsAsync();
                skCanvas.InvalidateVisual();
                InvalidatePreview();
            }
            catch (Exception ex)
            {
                // Načtení preferencí/print-params je nekritické - nespadnout přes globální handler,
                // jen nenápadně dát vědět (typicky Caché nedostupná).
                System.Diagnostics.Debug.WriteLine($"[OnPreferencesLoaded] {ex}");
                _vm.StatusText = "⚠ Nepodařilo se načíst uložené parametry (Caché nedostupná?)";
            }
        });
    }

    /// <summary>
    /// Aplikuje UI-side prefs (splittery, levý tab, viditelnost preview pane) z TdPreferencesService.
    /// Volá se po InitializeComponent, kdy už ColumnDefinitions a TabControl existují.
    /// VM-side prefs (zoom, grid, snap) se aplikují v VM konstruktoru přímo do DesignerState.
    /// </summary>
    private void ApplyUiPreferences()
    {
        var pref = _preferences?.MainState;
        if (pref is null) return;

        if (pref.LeftPanelWidthPx is { } leftW && leftW > 0)
            SectionsColumn.Width = new GridLength(leftW, GridUnitType.Pixel);

        if (!pref.ShowPreviewPane)
        {
            // BtnPreview_Changed handler nastaví PreviewColumn na 0.
            BtnPreview.IsChecked = false;
        }
        else if (pref.PreviewPanelWidthPx is { } previewW && previewW > 0)
        {
            // Star sizes (1*) → pixel; canvas dostane zbytek. Zachová pozici splitteru.
            PreviewColumn.Width = new GridLength(previewW, GridUnitType.Pixel);
            OrizniSirkuNahledu();
        }

        if (pref.ActiveLeftTabIndex >= 0 && pref.ActiveLeftTabIndex < LeftTabControl.Items.Count)
            LeftTabControl.SelectedIndex = pref.ActiveLeftTabIndex;

        // Zoom combo musí odrážet načtený State.Zoom - jinak combo ukazuje 100 %, ale plátno
        // rendruje na uloženém zoomu (a "1:1" to neopraví, protože index se nezmění).
        SyncZoomComboBox(_vm?.State.Zoom ?? 1f);
    }

    /// <summary>
    /// Drží panel náhledu uvnitř okna. Dvě věci, každá proti jiné cestě, jak se dostal ven
    /// (obojí nahlásil Jan 14. 9.):
    /// <list type="number">
    /// <item><b>MaxWidth sloupce</b> zastaví tažení splitteru. Plátno se nezmenší pod svou
    /// <c>MinWidth</c>, takže od té chvíle Grid celkovou šířku **překročí** místo aby tažení
    /// zastavil - panel ujede doprava a roste dál, dokud lišta stránkování nezmizí za okrajem.
    /// Tažení velikostí okna nehne, takže na to samotný ořez níž nestačí.</item>
    /// <item><b>Ořez uložené šířky.</b> Persistuje se v pixelech, takže po roztažení na velkém
    /// monitoru a otevření na menším přesáhne okno a Grid pevný sloupec nezmenší.</item>
    /// </list>
    /// Volá se při obnově preferencí a při každé změně velikosti okna, aby to přežilo
    /// i přetažení na menší monitor za běhu. Jen zmenšuje - roztažení zpátky patří uživateli.
    /// </summary>
    private void OrizniSirkuNahledu()
    {
        double sirkaOkna = MainGrid.ActualWidth;
        if (sirkaOkna <= 0) return;                    // před prvním layoutem není proti čemu měřit

        double zabrano = SectionsColumn.ActualWidth + SectionsSplitterColumn.ActualWidth
                       + CanvasColumn.MinWidth + PraveSplitterPx;
        double max = sirkaOkna - zabrano;
        if (max <= 0) return;

        PreviewColumn.MaxWidth = max;

        if (PreviewColumn.Width.IsAbsolute && PreviewColumn.Width.Value > max)
            PreviewColumn.Width = new GridLength(max, GridUnitType.Pixel);
    }

    /// <summary>Šířka pravého GridSplitteru (MainWindow.xaml, sloupec 3) - nemá jméno, aby na něj šlo sáhnout.</summary>
    private const double PraveSplitterPx = 4;

    /// <summary>Srovná zoom combo na nejbližší úroveň k danému zoomu (a tím i State.Zoom přes SelectionChanged).</summary>
    private void SyncZoomComboBox(float zoom)
    {
        if (CmbZoom is null) return;
        int best = 2; // 100 %
        float bestDiff = float.MaxValue;
        for (int i = 0; i < ZoomLevels.Length; i++)
        {
            float d = Math.Abs(ZoomLevels[i] - zoom);
            if (d < bestDiff) { bestDiff = d; best = i; }
        }
        if (CmbZoom.SelectedIndex != best)
            CmbZoom.SelectedIndex = best;   // SelectionChanged srovná State.Zoom + překreslí
    }

    // ── Plocha kolem stránky z theme tokenu ────────────────────────────────

    /// <summary>Přenese <c>EDTheme.Bg.Canvas</c> do Skia strany (plocha, kterou kreslí canvasy).</summary>
    private void ApplyCanvasThemeColor()
    {
        if (TryFindResource("EDTheme.Bg.Canvas.Color") is not System.Windows.Media.Color c) return;
        CanvasTheme.Background = new SKColor(c.R, c.G, c.B, c.A);
    }

    private void OnThemeApplied()
    {
        ApplyCanvasThemeColor();
        skCanvas.InvalidateVisual();
        InvalidatePreview();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // Statická událost by jinak držela zavřené okno naživu (leak při reopen).
        ThemeService.Applied -= OnThemeApplied;

        if (_vm is null || _preferences is null) return;

        // Snapshot UI-side state pro persist. ActualWidth=0 → null (nepřepsat na neplatnou hodnotu).
        double? leftPx    = SectionsColumn.ActualWidth > 0 ? SectionsColumn.ActualWidth : null;
        double? previewPx = PreviewColumn.ActualWidth   > 0 ? PreviewColumn.ActualWidth   : null;
        int tabIndex      = LeftTabControl.SelectedIndex;
        bool showPreview  = BtnPreview.IsChecked == true;

        // Fire-and-forget - closing nesmí čekat na Caché latenci. Pokud proces zhasne dřív
        // než save dokončí, ztratí se poslední session změny (akceptovatelný trade-off).
        _ = _vm.SaveMainStateAsync(leftPx, previewPx, tabIndex, showPreview);
        _ = _vm.SavePrintParamsAsync();
    }

    private void RebuildRecentFilesMenu()
    {
        if (_preferences is null || MenuRecentFiles is null) return;
        MenuRecentFiles.Items.Clear();
        var files = _preferences.RecentFiles;
        if (files.Count == 0)
        {
            MenuRecentFiles.Items.Add(new MenuItem { Header = "(žádné)", IsEnabled = false });
            return;
        }
        for (int i = 0; i < files.Count; i++)
        {
            var path = files[i];
            var item = new MenuItem
            {
                Header = $"_{i + 1}  {System.IO.Path.GetFileName(path)}",
                ToolTip = path,
                Tag = path
            };
            item.Click += async (_, _) =>
            {
                if (_vm is not null && item.Tag is string p)
                {
                    // Stejně jako Ctrl+O / import: neztratit rozdělanou práci bez potvrzení.
                    if (!await _vm.ConfirmUnsavedChangesAsync()) return;
                    await _vm.OpenTemplateFromFileAsync(p);
                    RebuildRecentFilesMenu();
                }
            };
            MenuRecentFiles.Items.Add(item);
        }
    }

    // ── Layout: sbalení levého panelu (uvolní místo na malém monitoru) ─────────

    private double _savedLeftWidth = 200;

    /// <summary>Sbalí/rozbalí levý panel (sekce / pole agendy) - uvolní místo na malém monitoru.</summary>
    private void BtnToggleLeftPanel_Changed(object sender, RoutedEventArgs e)
    {
        if (SectionsColumn is null) return;
        if (BtnToggleLeftPanel.IsChecked == true)
        {
            SectionsColumn.MinWidth = 120;
            SectionsColumn.Width = new GridLength(_savedLeftWidth);
            SectionsSplitterColumn.Width = new GridLength(4);
        }
        else
        {
            if (SectionsColumn.ActualWidth > 0) _savedLeftWidth = SectionsColumn.ActualWidth;
            SectionsColumn.MinWidth = 0;                 // jinak by Width=0 byla zaclampovaná na 120
            SectionsColumn.Width = new GridLength(0);
            SectionsSplitterColumn.Width = new GridLength(0);
        }
        if (MenuSectionsPanel is not null) MenuSectionsPanel.IsChecked = BtnToggleLeftPanel.IsChecked == true;
    }

    // ── Panel visibility ──────────────────────────────────────────────────────

    /// <summary>Menu Zobrazit → Levý panel dělá totéž co ☰ a Ctrl+B. Do 30. 10. mělo vlastní kód, který
    /// panel neschoval (sloupec drží nejmenší šířku) a se ☰ se rozcházel.</summary>
    private void MenuPanelSection_Changed(object sender, RoutedEventArgs e)
    {
        if (BtnToggleLeftPanel is null) return;
        BtnToggleLeftPanel.IsChecked = MenuSectionsPanel.IsChecked;
    }

    private void BtnPreview_Changed(object sender, RoutedEventArgs e)
    {
        if (PreviewColumn is null) return; // Event fires during InitializeComponent before controls are ready
        bool show = BtnPreview.IsChecked == true;
        if (show)
        {
            // Reset both columns to 1* so they split 50:50
            // (GridSplitter converts star sizes to pixels internally)
            CanvasColumn.Width = new GridLength(1, GridUnitType.Star);
            PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            PreviewColumn.Width = new GridLength(0);
        }
        MenuPreview.IsChecked = show;
        if (show) InvalidatePreview();
    }

    private void MenuPreview_Changed(object sender, RoutedEventArgs e)
    {
        if (BtnPreview is null) return; // Event fires during InitializeComponent
        BtnPreview.IsChecked = MenuPreview.IsChecked;
        BtnPreview_Changed(sender, e);
    }
}
