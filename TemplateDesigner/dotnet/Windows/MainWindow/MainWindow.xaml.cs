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

    // ── Mouse state machine ───────────────────────────────────────────────────
    private enum MouseMode { Idle, Dragging, Resizing, RubberBand, AddingElement, SectionResizing }
    private MouseMode _mouseMode = MouseMode.Idle;

    private TemplateElement? _dragElement;
    private float _dragMouseStartCanvasX, _dragMouseStartCanvasY;
    private readonly Dictionary<TemplateElement, (float X, float Y)> _dragInitPositions = new();
    // Per-axis snap state z posledního mouse move (pro soft-snap v EndDrag)
    private bool _lastSnapActiveX, _lastSnapActiveY;
    // True jakmile myš překročí click-vs-drag práh (4px). Před tím se klik chová jako selekce
    // bez posunu - jinak by jemný cuk myší při kliku vyvolal soft-snap a rozbil pozici.
    private bool _dragHasMoved;
    private const float DragThresholdPxSquared = 16f; // 4px

    private readonly SnapEngine _snapEngine = new();

    private SKPoint _rubberBandStart;
    private SKRect? _rubberBandRect;

    private TemplateElement? _resizeElement;
    private ResizeHandle     _resizeHandle;
    private float  _resizeInitX, _resizeInitY, _resizeInitW;
    private float? _resizeInitH;
    private float  _resizeInitHComputed;
    private float  _resizeMouseStartX, _resizeMouseStartY;
    private float  _resizeOriginX, _resizeOriginY;
    private float  _resizeSectionW, _resizeSectionH; // hranice sekce pro clamp

    // ── Tažení spodní hrany sekce (mění její výšku, sekce pod se posunou) ─────
    private Section? _sectionResizeTarget;
    private float    _sectionResizeStartY;      // px, kde tažení začalo
    private float    _sectionResizeStartH;      // mm, výška před tažením
    private bool     _sectionResizeUpwards;     // patička je kotvená dole, takže roste nahoru

    // ── Adding element (drag from palette / field drag) ────────────────────────
    private string? _addingElementType;
    private string? _addingFieldName;       // e.g. "CISLO_LINKY" - mutually exclusive with _addingElementType
    private SKPoint _addingGhostPos;
    private Section? _addingTargetSection;
    private TemplateElement? _addingTargetElement; // TextElement under cursor (field drag append)

    // ── Field drag initiation from TreeView ─────────────────────────────────────
    private System.Windows.Point _fieldDragStartPoint;
    private SchemaFieldVM? _fieldDragCandidate;

    // ── Duplicate pending flag (Ctrl+D chord) ─────────────────────────────────
    private bool _duplicatePending;

    // ── Section list selection helper ─────────────────────────────────────────
    private bool _suppressSectionSelection;

    // ── Section drag reorder ────────────────────────────────────────────────────
    private SectionVM? _sectionDragCandidate;
    private System.Windows.Point _sectionDragStartPoint;
    private bool _sectionDragActive;
    private int _sectionDropIndex = -1;
    private int _sectionDropIndicatorAt = -1; // index itemu s upraveným Margin

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
                      string? startupAgendaCode = null)
    {
        _preferences = preferences;
        _vm         = new TemplateDesignerViewModel(mlRest, preferences, agendas, startupAgendaCode);
        DataContext = _vm;
        InitializeComponent();

        // Ručně, protože TD okna neotvírá přes WindowNavigator - bez tohohle volání by se
        // pozice a velikost hlavního okna nepersistovaly (musí být před Show()).
        _vm.AttachToWindow(this);

        // AI generátor šablon zatím jen pro vývojáře (lokální dev klíč) - před produkcí skrytý;
        // Import PDF zůstává pro všechny.
        MenuAiAssistant.Visibility =
            string.IsNullOrEmpty(AppSettings.Current?.AiAssistant.AnthropicApiKey)
                ? Visibility.Collapsed : Visibility.Visible;

        // DEV přepínač firem - stejný gate jako AI generátor (lokální dev klíč). Naplní se
        // z pevného seznamu ve VM; v produkci zůstává skrytý.
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
        _previewCanvas = new PreviewCanvas(_vm.State);
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
        skPreview.SizeChanged        += (_, _) => InvalidatePreview();
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
            _vm.RebuildSections,
            UpdateCanvasSize,
            () => _vm.RefreshFieldsList(),
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
        InlinePropertiesPanel.ShowMessage("Vyberte element na canvasu");

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

    // ─────────────────────────────────────────────────────────────────────────
    // Render
    // ─────────────────────────────────────────────────────────────────────────

    private void OnPaintSurface(object? sender, object e)
    {
        if (_canvas is null) return;
        var args    = (dynamic)e;
        SKSurface surface = args.Surface;
        // Předat viditelnou oblast pro viewport culling (grid pak nekreslí mimo viewport).
        _canvas.VisibleViewport = new SKRect(
            (float)CanvasScrollViewer.HorizontalOffset,
            (float)CanvasScrollViewer.VerticalOffset,
            (float)(CanvasScrollViewer.HorizontalOffset + CanvasScrollViewer.ViewportWidth),
            (float)(CanvasScrollViewer.VerticalOffset + CanvasScrollViewer.ViewportHeight));
        _canvas.Render(surface.Canvas, (float)skCanvas.ActualWidth, (float)skCanvas.ActualHeight);

        DrawTableCellOverlay(surface.Canvas, (float)skCanvas.ActualWidth);

        if (_mouseMode == MouseMode.RubberBand && _rubberBandRect.HasValue)
        {
            var r = _rubberBandRect.Value;
            using var fill   = new SKPaint { Color = new SKColor(0x222563EB) };
            using var border = new SKPaint { Color = new SKColor(0xFF2563EB), IsStroke = true, StrokeWidth = 1f };
            surface.Canvas.DrawRect(r, fill);
            surface.Canvas.DrawRect(r, border);
        }

        if (_mouseMode == MouseMode.AddingElement
            && (_addingElementType is not null || _addingFieldName is not null)
            && _vm is not null
            && _addingGhostPos != default)
        {
            float cw = (float)skCanvas.ActualWidth;

            // Field drag na TextElement → zvýraznit cílový element
            if (_addingFieldName is not null && _addingTargetElement is not null)
            {
                var elRect = _canvas.GetElementCanvasRect(_addingTargetElement, cw);
                if (elRect is not null)
                {
                    using var highlight = new SKPaint { Color = new SKColor(0x302563EB) };
                    using var border = new SKPaint
                    {
                        Color = new SKColor(0xFF2563EB), IsStroke = true,
                        StrokeWidth = 2f
                    };
                    surface.Canvas.DrawRect(elRect.Value, highlight);
                    surface.Canvas.DrawRect(elRect.Value, border);
                }
            }
            // Zvýrazni cílovou sekci + ghost element
            else if (_addingTargetSection is not null)
            {
                var origin = _canvas.GetSectionOrigins(cw)
                    .FirstOrDefault(s => s.Section == _addingTargetSection);
                if (origin.Section is not null)
                {
                    float sW = _vm.State.CurrentTemplate.PageSize.PrintableWidth * _canvas.MmToPx;
                    float sH = _addingTargetSection.EffectiveDesignerHeight * _canvas.MmToPx;
                    var sRect = new SKRect(origin.OriginX, origin.OriginY, origin.OriginX + sW, origin.OriginY + sH);
                    using var sectionHighlight = new SKPaint { Color = new SKColor(0x182563EB) };
                    surface.Canvas.DrawRect(sRect, sectionHighlight);

                    // Ghost element - čárkovaný obdélník
                    var (defW, defH) = TemplateDesignerViewModel.GetDefaultElementSize(_addingElementType ?? ElementTypes.Text);
                    float gx = _addingGhostPos.X - (defW * _canvas.MmToPx / 2f);
                    float gy = _addingGhostPos.Y - (defH * _canvas.MmToPx / 2f);
                    var ghostRect = new SKRect(gx, gy, gx + defW * _canvas.MmToPx, gy + defH * _canvas.MmToPx);
                    using var ghostFill = new SKPaint { Color = new SKColor(0x152563EB) };
                    using var ghostBorder = new SKPaint
                    {
                        Color = new SKColor(0xFF2563EB),
                        IsStroke = true,
                        StrokeWidth = 1.5f,
                        PathEffect = SKPathEffect.CreateDash([6f, 4f], 0)
                    };
                    surface.Canvas.DrawRect(ghostRect, ghostFill);
                    surface.Canvas.DrawRect(ghostRect, ghostBorder);
                }
            }
        }
    }

    private void UpdateCanvasSize()
    {
        if (_canvas is null || _vm is null) return;
        const float padding   = 80f;  // 40 top + 40 bottom standardní okraj kolem stránky
        const float topBuffer = 26f;  // = DesignerCanvas.TopBufferPx (nad plátnem je 34px pruh stránek jako nad náhledem)
        float pageW   = _vm.State.CurrentTemplate.PageSize.Width * _canvas.MmToPx;
        // Výška plátna = listy stránek pod sebou (i s případným přesahem obsahu).
        float paperH  = _canvas.TotalPaperHeightPx();
        var dpi = VisualTreeHelper.GetDpi(skCanvas);
        skCanvas.Width  = NaCelePixely(pageW + padding + _canvas.ExtraLeftPx, dpi.DpiScaleX);
        skCanvas.Height = NaCelePixely(paperH + padding + topBuffer, dpi.DpiScaleY);
        skCanvas.InvalidateVisual();
        UpdatePreviewSize();
    }

    private void InvalidateCanvas()
    {
        skCanvas.InvalidateVisual();
        InvalidatePreview();
    }

    private void OnCanvasScrollWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        e.Handled = true;
        int newIdx = Math.Clamp(CmbZoom.SelectedIndex + (e.Delta > 0 ? 1 : -1), 0, ZoomLevels.Length - 1);
        if (newIdx == CmbZoom.SelectedIndex) return;

        // Zoom ke kurzoru: bod obsahu pod myší má po zoomu zůstat pod myší (standard editorů).
        // Offsety se nastaví až po layout passu (BeginInvoke Loaded) - ScrollViewer jinak clampne
        // na starou velikost plátna.
        var mouse = e.GetPosition(CanvasScrollViewer);
        double contentX = CanvasScrollViewer.HorizontalOffset + mouse.X;
        double contentY = CanvasScrollViewer.VerticalOffset + mouse.Y;
        float oldZoom = ZoomLevels[Math.Clamp(CmbZoom.SelectedIndex, 0, ZoomLevels.Length - 1)];
        float scale = ZoomLevels[newIdx] / oldZoom;

        CmbZoom.SelectedIndex = newIdx;   // SelectionChanged přenastaví State.Zoom + UpdateCanvasSize

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            CanvasScrollViewer.ScrollToHorizontalOffset(contentX * scale - mouse.X);
            CanvasScrollViewer.ScrollToVerticalOffset(contentY * scale - mouse.Y);
        });
    }

    private void FitToPage()
    {
        if (_vm is null || _canvas is null) return;
        float viewportW = (float)CanvasScrollViewer.ActualWidth - 20f; // margin pro scrollbar
        float pageW     = _vm.State.CurrentTemplate.PageSize.Width * (96f / 25.4f); // mm → px at zoom=1
        if (pageW <= 0 || viewportW <= 0) return;

        float idealZoom = viewportW / pageW;
        // Najdi nejbližší úroveň zoomu (zaokrouhleno dolů)
        int bestIdx = 0;
        for (int i = ZoomLevels.Length - 1; i >= 0; i--)
        {
            if (ZoomLevels[i] <= idealZoom + 0.01f) { bestIdx = i; break; }
        }
        CmbZoom.SelectedIndex = bestIdx;
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
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    private float PreviewZoom =>
        CmbPreviewZoom is { SelectedIndex: var idx } && idx >= 0 && idx < ZoomLevels.Length
            ? ZoomLevels[idx]
            : 1.0f;

    private void UpdatePreviewSize()
    {
        if (BtnPreview?.IsChecked != true || _previewCanvas is null || _vm is null) return;
        float zoom = PreviewZoom;
        var dpi = VisualTreeHelper.GetDpi(skPreview);
        skPreview.Width  = NaCelePixely(_previewCanvas.ComputePageWidthPx(zoom), dpi.DpiScaleX);
        skPreview.Height = NaCelePixely(
            _previewCanvas.CalculatePageHeightPx(zoom, _vm.BuildPreviewMockData()) + 40f, dpi.DpiScaleY);
    }

    /// <summary>Rozměr plochy Skia zaokrouhlený nahoru na celý pixel obrazovky.</summary>
    /// <remarks>⚠️ <c>SKElement</c> uřízne bitmapu na celé pixely a WPF ji pak roztáhne zpátky na
    /// necelý rozměr - obraz se převzorkuje a čára, která tím padne mezi pixely, se rozmaže do dvou
    /// řad. Při zvětšení pak jedna čára tabulky vypadala silnější, v PDF ne.</remarks>
    private static double NaCelePixely(double dip, double dpiScale) =>
        Math.Ceiling((dip * dpiScale) - 0.001) / dpiScale;

    private void OnPreviewScrollWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        e.Handled = true;
        int newIdx = Math.Clamp(CmbPreviewZoom.SelectedIndex + (e.Delta > 0 ? 1 : -1), 0, ZoomLevels.Length - 1);
        if (newIdx == CmbPreviewZoom.SelectedIndex) return;

        // Zoom ke kurzoru (stejně jako plátno): bod obsahu pod myší má po zoomu zůstat pod myší.
        // Offsety se nastaví až po layout passu (BeginInvoke Loaded) - skPreview se přepočítá
        // synchronně v CmbPreviewZoom_SelectionChanged → UpdatePreviewSize, ScrollViewer jinak
        // clampne na starou velikost.
        var mouse = e.GetPosition(PreviewScrollViewer);
        double contentX = PreviewScrollViewer.HorizontalOffset + mouse.X;
        double contentY = PreviewScrollViewer.VerticalOffset + mouse.Y;
        float oldZoom = ZoomLevels[Math.Clamp(CmbPreviewZoom.SelectedIndex, 0, ZoomLevels.Length - 1)];
        float scale = ZoomLevels[newIdx] / oldZoom;

        CmbPreviewZoom.SelectedIndex = newIdx;   // SelectionChanged → InvalidatePreview → UpdatePreviewSize

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            PreviewScrollViewer.ScrollToHorizontalOffset(contentX * scale - mouse.X);
            PreviewScrollViewer.ScrollToVerticalOffset(contentY * scale - mouse.Y);
        });
    }

    private void CmbPreviewZoom_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null || _previewCanvas is null) return;
        InvalidatePreview();
    }

    /// <summary>„1:1" - skutečná velikost 100 % (designer i náhled). Tlačítko i Ctrl+1.</summary>
    private void BtnResetView_Click(object sender, RoutedEventArgs e) => ResetZoomTo100();

    /// <summary>„Fit" - přizpůsobit designer stránce. Tlačítko i Ctrl+0.</summary>
    private void BtnFitPage_Click(object sender, RoutedEventArgs e) => FitToPage();

    private void ResetZoomTo100()
    {
        const int defaultIdx = 2; // 100 %
        if (CmbZoom is not null)        CmbZoom.SelectedIndex        = defaultIdx;
        if (CmbPreviewZoom is not null) CmbPreviewZoom.SelectedIndex = defaultIdx;
    }

    private void InvalidatePreview()
    {
        if (BtnPreview?.IsChecked != true) return;
        UpdatePreviewSize();
        skPreview.InvalidateVisual();
    }

    private void OnPreviewPaintSurface(object? sender, object e)
    {
        if (_previewCanvas is null || _vm is null) return;
        var args    = (dynamic)e;
        SKSurface surface = args.Surface;
        float w = (float)args.Info.Width;
        float h = (float)args.Info.Height;
        var previewData = _vm.BuildPreviewMockData();
        _vm.State.CurrentPreviewData = previewData;
        _previewCanvas.Render(surface.Canvas, w, h, previewData);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Section list - selection helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void RestoreSectionSelection()
    {
        _suppressSectionSelection = true;
        try { SectionsList.SelectedItem = _vm?.Sections.FirstOrDefault(s => s.Section == _vm?.State.ActiveSection); }
        finally { _suppressSectionSelection = false; }
    }

    private void SectionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSectionSelection || _vm is null) return;
        if (SectionsList.SelectedItem is SectionVM svm)
        {
            _suppressSectionSelection = true;
            try { _vm.ActivateSection(svm.Section); }
            finally { _suppressSectionSelection = false; }
        }
    }

    private void SectionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SectionsList.SelectedItem is SectionVM svm)
            OpenPropertiesForSection(svm.Section);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Section drag reorder
    // ─────────────────────────────────────────────────────────────────────────

    private void SectionsList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _sectionDragCandidate = null;
        _sectionDragActive = false;
        if (GetSectionVMFromEvent(e) is { } svm)
        {
            _sectionDragStartPoint = e.GetPosition(SectionsList);
            _sectionDragCandidate = svm;
        }
    }

    private void SectionsList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            if (_sectionDragActive) SectionsList_DragFinish();
            return;
        }

        if (_sectionDragCandidate is not null && !_sectionDragActive)
        {
            var pos = e.GetPosition(SectionsList);
            if (Math.Abs(pos.X - _sectionDragStartPoint.X) < 5 && Math.Abs(pos.Y - _sectionDragStartPoint.Y) < 5)
                return;

            _sectionDragActive = true;
            SectionsList.CaptureMouse();
            SectionsList.Cursor = Cursors.Hand;
        }

        if (_sectionDragActive)
        {
            var pos = e.GetPosition(SectionsList);
            _sectionDropIndex = GetSectionDropIndex(pos);
            UpdateSectionDropIndicator();
        }
    }

    private void SectionsList_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_sectionDragActive)
        {
            SectionsList_DragFinish();
            e.Handled = true;
        }
        else
        {
            _sectionDragCandidate = null;
        }
    }

    private void SectionsList_DragCancel(object sender, MouseEventArgs e)
    {
        if (!_sectionDragActive) { _sectionDragCandidate = null; return; }
        _sectionDragCandidate = null;
        _sectionDragActive = false;
        _sectionDropIndex = -1;
        SectionsList.ReleaseMouseCapture();
        SectionsList.ClearValue(CursorProperty);
        ClearSectionDropIndicator();
    }

    private void SectionsList_DragFinish()
    {
        if (_sectionDragCandidate is not null && _sectionDropIndex >= 0 && _vm is not null)
        {
            int fromIndex = _vm.State.CurrentTemplate.Sections.IndexOf(_sectionDragCandidate.Section);
            int toIndex = _sectionDropIndex;
            // Adjust: pokud drop je za dragged item, odečti 1 (protože se item nejdřív odebere)
            if (toIndex > fromIndex) toIndex--;
            if (fromIndex >= 0 && toIndex >= 0 && fromIndex != toIndex)
            {
                _vm.ReorderSection(_sectionDragCandidate.Section, fromIndex, toIndex);
                // ActivateSection nevoláme - ReorderSection už volá RebuildSections
                _vm.State.ActiveSection = _sectionDragCandidate.Section;
            }
        }

        _sectionDragCandidate = null;
        _sectionDragActive = false;
        _sectionDropIndex = -1;
        SectionsList.ReleaseMouseCapture();
        SectionsList.ClearValue(CursorProperty);
        ClearSectionDropIndicator();
    }

    /// <summary>Vrátí index kam se má sekce vložit (0 = před první, Count = za poslední).</summary>
    private int GetSectionDropIndex(System.Windows.Point posInListBox)
    {
        if (_vm is null) return -1;
        int count = _vm.Sections.Count;

        for (int i = 0; i < count; i++)
        {
            if (SectionsList.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container)
                continue;
            var itemPos = container.TranslatePoint(new System.Windows.Point(0, 0), SectionsList);
            float midY = (float)(itemPos.Y + container.ActualHeight / 2);
            if (posInListBox.Y < midY)
                return i;
        }
        return count;
    }

    private void UpdateSectionDropIndicator()
    {
        ClearSectionDropIndicator();
        if (_sectionDropIndex < 0 || _vm is null) return;

        int count = _vm.Sections.Count;
        int indicatorAt = _sectionDropIndex < count ? _sectionDropIndex : count - 1;
        bool atBottom = _sectionDropIndex >= count;

        var border = GetSectionItemBorder(indicatorAt);
        if (border is null) return;

        // Zvětšený Margin na vrchu/spodu funguje jako vizuální indikátor pozice dropu
        border.Margin = atBottom
            ? new Thickness(0, 2, 0, 4)
            : new Thickness(0, 6, 0, 0);
        _sectionDropIndicatorAt = indicatorAt;
    }

    private void ClearSectionDropIndicator()
    {
        if (_sectionDropIndicatorAt >= 0)
        {
            var border = GetSectionItemBorder(_sectionDropIndicatorAt);
            if (border is not null)
                border.Margin = new Thickness(0, 2, 0, 0); // obnovit výchozí
            _sectionDropIndicatorAt = -1;
        }
    }

    /// <summary>Najde vnitřní Border element ze SectionItemTemplate pro daný index.</summary>
    private Border? GetSectionItemBorder(int index)
    {
        if (SectionsList.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem container)
            return null;
        // ListBoxItem template = ContentPresenter → Border (z DataTemplate)
        container.ApplyTemplate();
        if (VisualTreeHelper.GetChildrenCount(container) == 0) return null;
        var presenter = VisualTreeHelper.GetChild(container, 0);
        if (VisualTreeHelper.GetChildrenCount(presenter) == 0) return null;
        return VisualTreeHelper.GetChild(presenter, 0) as Border;
    }

    private static SectionVM? GetSectionVMFromEvent(MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            var current = source;
            while (current is not null)
            {
                if (current is FrameworkElement { DataContext: SectionVM svm })
                    return svm;
                current = VisualTreeHelper.GetParent(current);
            }
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Toolbar handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void CmbZoom_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        int idx = CmbZoom.SelectedIndex;
        if (idx >= 0 && idx < ZoomLevels.Length)
        {
            _vm.State.Zoom = ZoomLevels[idx];
            UpdateCanvasSize();
        }
    }

    private void CmbGridSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null || CmbGridSize.SelectedItem is not ComboBoxItem item) return;
        if (float.TryParse(item.Tag?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float size))
            _vm.GridSize = size;
    }

    /// <summary>Najde ComboBoxItem s odpovídajícím Tagem a aktivuje ho. Volá se po načtení šablony.</summary>
    private void SyncGridSizeComboBox(float gridSizeMm)
    {
        foreach (var item in CmbGridSize.Items.OfType<ComboBoxItem>())
        {
            if (float.TryParse(item.Tag?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)
                && Math.Abs(v - gridSizeMm) < 0.001f)
            {
                CmbGridSize.SelectedItem = item;
                return;
            }
        }
    }

    /// <summary>Sync CmbSnapGap na uloženou hodnotu mezery - nebindovaný combobox (jako CmbGridSize).
    /// Bez toho combo po startu/loadu ukazoval default, i když State.SnapGapMm držel uloženou hodnotu.</summary>
    private void SyncSnapGapComboBox(float gapMm)
    {
        foreach (var item in CmbSnapGap.Items.OfType<ComboBoxItem>())
        {
            if (float.TryParse(item.Tag?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)
                && Math.Abs(v - gapMm) < 0.001f)
            {
                CmbSnapGap.SelectedItem = item;
                return;
            }
        }
    }

    private void CmbSnapGap_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null || CmbSnapGap.SelectedItem is not ComboBoxItem item) return;
        if (float.TryParse(item.Tag?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float gap))
            _vm.SnapGapMm = gap;
    }

    private void MenuScale_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null || sender is not MenuItem item || item.Tag is not string tagStr) return;
        if (float.TryParse(tagStr,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float scale))
        {
            _vm.ScaleTemplate(scale);
        }
    }

    // ── Panel visibility ──────────────────────────────────────────────────────

    private void MenuPanelSection_Changed(object sender, RoutedEventArgs e)
    {
        if (SectionsColumn is null) return;
        SectionsColumn.Width = MenuSectionsPanel.IsChecked
            ? new GridLength(200, GridUnitType.Pixel)
            : new GridLength(0);
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

    // ─────────────────────────────────────────────────────────────────────────
    // Menu / button click handlers (thin - delegate to ViewModel)
    // ─────────────────────────────────────────────────────────────────────────

    private void MenuNew_Click(object sender, RoutedEventArgs e)       => _vm!.NewCommand.Execute(null);
    private void MenuOpen_Click(object sender, RoutedEventArgs e)      => _vm!.OpenCommand.Execute(null);
    private void MenuSave_Click(object sender, RoutedEventArgs e)      => _vm!.SaveCommand.Execute(null);
    private void MenuSaveAs_Click(object sender, RoutedEventArgs e)    => _vm!.SaveAsCommand.Execute(null);

    // Re-entrance guard - modální ShowDialog otevírá vnořený dispatcher, který může
    // doručit dříve queueované Ctrl+P. Bez guardu by druhé pressnutí během už-otevřeného
    // dialogu vyvolalo nový PrintDialog skrz Owner-set-on-closed crash.
    private bool _printDialogOpening;

    private void MenuPrint_Click(object sender, RoutedEventArgs e)
    {
        if (_printDialogOpening || _vm is null) return;
        _printDialogOpening = true;
        try { OpenPrintDialog(); }
        finally { _printDialogOpening = false; }
    }

    /// <summary>
    /// Načtení dat všech vybraných linek (tiskové okno, kontrola z návrháře). Null = bez databáze
    /// nebo agendy. Stránky dat nesou popis linky (<c>KontrolaVystupu.LinkaKlic</c>).
    /// </summary>
    private Func<Dictionary<string, object?>, IProgress<(int Hotovo, int Celkem, string Linka)>?, List<DataContext>?>? NacitaniVsechLinek()
    {
        Func<Dictionary<string, object?>, IProgress<(int Hotovo, int Celkem, string Linka)>?, List<DataContext>?>? buildRealData = null;
        if (_vm!.CacheProvider is not null && !string.IsNullOrEmpty(_vm.State.CurrentTemplate.AgendaCode))
        {
            var provider = _vm.CacheProvider;
            var agendaCode = _vm.State.CurrentTemplate.AgendaCode!;
            // Snapshot na UI threadu - callback běží na threadpoolu a nesmí sahat na živou šablonu
            var columnsDefJson = _vm.CurrentColumnsDefinitionJson();
            var turnusy = _vm.CurrentTurnusyKeSpojum();
            // Kultura šablony (formát dat, čísel) - bez ní by tisk formátoval podle nastavení
            // Windows, zatímco náhled podle šablony, a slovenská šablona by vyšla jinak.
            var kultura = _vm.GetTemplateCulture();
            buildRealData = (paramValues, prubeh) =>
            {
                List<DataContext>? pages;

                // Pokud má uživatel aktivní instance (vybrané přes Instance Picker), iteruj je
                // a vygeneruj batch napříč všemi - ignoruje LINKA_ID z parametrů dialogu.
                if (_vm.ActiveInstances.Count > 0)
                {
                    var paramName = _vm.CurrentInstanceParamName;
                    var all = new List<DataContext>(_vm.ActiveInstances.Count * 4);
                    int hotovo = 0;
                    foreach (var inst in _vm.ActiveInstances)
                    {
                        prubeh?.Report((hotovo++, _vm.ActiveInstances.Count, inst.Display));
                        var instPages = provider.GetData(agendaCode, TemplateDesignerViewModel.ComposeGetDataParams(
                            paramValues, paramName, inst.Value, _vm.ActiveSkupinaId, columnsDefJson,
                            turnusy, _vm.JazykSablony));
                        if (instPages is null) continue;
                        // Linka ke každé stránce - kontrola v tiskovém okně z ní řekne, které lince
                        // hláška patří (`B178`).
                        var linka = new Dictionary<string, object?> { [TemplateDesigner.Models.KontrolaVystupu.LinkaKlic] = inst.Display };
                        all.AddRange(instPages.Select(p => p.WithCulture(kultura).WithExtraValues(linka)));
                    }
                    pages = all.Count > 0 ? all : null;
                }
                else
                {
                    // Bez vybrané instance nelze tisknout reálná data - linku určuje výhradně
                    // Instance Picker (parametry sestavy už LINKA_ID neobsahují). Dialog na null
                    // ukáže hlášku s navedením na výběr linky.
                    pages = null;
                }

                if (pages is null) return null;
                // Merge parametrů do každého DataContextu - C# rendering je potřebuje
                // (HODINY_OD, HODINY_DO, CASOVA_OSA, TISK_PROJETYCH_ZAST atd.)
                pages = TemplateDesignerViewModel.MergeParamsIntoPages(pages, paramValues).ToList();

                return TemplateDesigner.Models.NoteVisibilityFilter.Apply(
                    pages,
                    TemplateDesigner.Models.NoteVisibilityFilter.ParseMode(
                        paramValues.GetValueOrDefault(TemplateDesigner.Models.NoteVisibilityFilter.ParamName)),
                    false,
                    TemplateDesigner.Models.NoteVisibilityFilter.ShouldHideNaZnameniLegend(_vm.State.CurrentTemplate),
                    hidePrestupLegend: TemplateDesigner.Models.NoteVisibilityFilter.ShouldHidePrestupLegend(_vm.State.CurrentTemplate),
                    hiddenContent: TemplateDesigner.Models.NoteVisibilityFilter.HiddenContentCategories(_vm.State.CurrentTemplate));
            };
        }

        return buildRealData;
    }

    /// <summary>
    /// Kontrola všech vybraných linek z návrháře (`B178`). Okno s výsledkem se otevře hned a ukazuje
    /// průběh; klik na hlášku přepne na její linku a vybere prvek - to z tiskového okna nejde.
    /// </summary>
    private async void BtnKontrola_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var nacitani = NacitaniVsechLinek();
        if (nacitani is null || _vm.ActiveInstances.Count == 0)
        {
            _vm.StatusText = "Kontrola potřebuje vybrané linky - vyberte je na štítku vedle agendy.";
            return;
        }

        var vysledek = new KontrolaVysledkuDialogViewModel("Načítám data…", []) { Probiha = true };
        vysledek.RequestVybrat = PrejdiNaHlaseni;
        new KontrolaVysledkuDialog(vysledek) { Owner = this }.Show();
        try
        {
            var parametry = new Dictionary<string, object?>(_vm.PreviewParamValues);
            var prubeh = new Progress<(int Hotovo, int Celkem, string Linka)>(p =>
            {
                vysledek.Nadpis = $"Načítám data {p.Hotovo + 1} z {p.Celkem} · {p.Linka}";
                vysledek.Prubeh = p.Celkem > 0 ? 0.5 * p.Hotovo / p.Celkem : 0;
            });
            var data = await Task.Run(() => nacitani(parametry, prubeh));
            if (data is null or { Count: 0 })
            {
                vysledek.Nadpis = "Databáze nevrátila žádná data.";
                return;
            }
            var template = _vm.State.CurrentTemplate;
            var hlaseni = await TemplateDesigner.Models.KontrolaVystupu.ZkontrolujAsync(template, data,
                d => TemplateDesigner.Models.KontrolaVystupu.ZLinky(template, d, _previewCanvas!.HlaseniSazby),
                (hotovo, celkem, linka) =>
                {
                    vysledek.Nadpis = hotovo < celkem ? $"Kontroluji {hotovo + 1} z {celkem} · {linka}" : "";
                    vysledek.Prubeh = celkem > 0 ? 0.5 + 0.5 * hotovo / celkem : 1;
                });
            int linek = TemplateDesigner.Models.KontrolaVystupu.PoLinkach(data).Count;
            vysledek.Hlaseni = hlaseni;
            vysledek.Nadpis = hlaseni.Count > 0
                ? $"Zkontrolováno {TemplateDesigner.Models.KontrolaVystupu.Linek(linek)}: {TemplateDesigner.Models.KontrolaVystupu.SouhrnPoctu(hlaseni)}"
                : $"Zkontrolováno {TemplateDesigner.Models.KontrolaVystupu.Linek(linek)} - bez hlášek";
        }
        catch (Exception ex)
        {
            vysledek.Nadpis = $"Kontrola selhala: {ex.Message}";
        }
        finally
        {
            vysledek.Probiha = false;
        }
    }

    /// <summary>Klik na hlášku v okně kontroly: přepnout na její linku a vybrat prvek.</summary>
    private void PrejdiNaHlaseni(Hlaseni h)
    {
        if (_vm is null) return;
        if (h.Linka is { } linka)
        {
            int i = _vm.ActiveInstances.ToList().FindIndex(x => x.Display == linka);
            if (i >= 0 && i != _vm.ActiveInstanceIndex) _vm.ActiveInstanceIndex = i;
        }
        if (h.PrvekId is { } id) VyberPrvekZHlaseni(id);
        Activate();
    }

    private void OpenPrintDialog()
    {
        // PrintDialog operuje nad sdíleným dict s preview lištou (_vm.PreviewParamValues).
        // Saved PrintParams se aplikují při openu šablony (LoadPrintParamsAndApplyAsync),
        // takže _previewParams už obsahuje persistované hodnoty - žádný extra I/O zde.

        var buildRealData = NacitaniVsechLinek();

        var tplAgendaCode = _vm!.State.CurrentTemplate.AgendaCode;
        var agendaClass = string.IsNullOrEmpty(tplAgendaCode) ? null : _vm.CacheProvider?.GetCacheClass(tplAgendaCode);

        var vm = new PrintDialogViewModel(
            _vm.State.CurrentTemplate,
            _vm.AgendaParameters,
            _vm.PreviewParamValues,
            paramValues => _vm.BuildPreviewMockData(paramValues),
            buildRealData,
            _preferences,
            onParamsChanged: _vm.OnExternalParamsChanged,
            agendaClass: agendaClass,
            maVybraneLinky: () => _vm.ActiveInstances.Count > 0);

        vm.RequestRenderPng = (filePath, dataPages) =>
            _previewCanvas!.RenderMultiDataToPng(filePath, dataPages,
                template: Serialization.TemplateSerializer.DeepClone(_vm!.State.CurrentTemplate));

        vm.RequestRenderPdf = (filePath, data, sheets, meritko) =>
            _previewCanvas!.RenderToPdf(filePath, data, sheets, meritko);

        // Render nad snapshotem šablony - VM volá callback z threadpoolu (async tisk),
        // živá šablona by závodila s preview renderem UI threadu (element cache).
        vm.RequestRenderMultiPdf = (filePath, dataPages, sheets, meritko) =>
            _previewCanvas!.RenderMultiDataToPdf(filePath, dataPages,
                template: Serialization.TemplateSerializer.DeepClone(_vm!.State.CurrentTemplate),
                sheetLayout: sheets, meritko: meritko);

        vm.RequestOverflowCheck = dataPages =>
            _previewCanvas!.CountOverflowingPages(dataPages);
        vm.RequestKontrolaLinky = data => TemplateDesigner.Models.KontrolaVystupu.ZLinky(
            _vm.State.CurrentTemplate, data, _previewCanvas!.HlaseniSazby);

        var dlg = new PrintDialog(vm)
            { Owner = this, PlaceholderFields = _vm!.PlaceholderFieldItems() };
        dlg.ShowDialog();

        // Tisk sdílí _previewParams s tabem parametrů - po zavření přenačti tab formulář,
        // aby odrážel hodnoty případně změněné v dialogu.
        ReloadParamsTabForm();
    }

    // ── EDInputForm parametrů v levém tabu (AgendaParams, includeExport=0) ───────
    // Vzor stejný jako PrintDialog: CacheClass + InitParams → Init → SchemaLoaded → napojení polí.

    private bool _paramsTabInitHooked;

    /// <summary>Přenačte formulář parametrů v levém tabu z aktuální agendy + hodnot (sdílený dict).
    /// Volá se po změně agendy/šablony (VM callback) i po zavření tiskového dialogu (sync hodnot).</summary>
    private void ReloadParamsTabForm()
    {
        if (_vm is null) return;

        // Agenda bez parametrů (GENERAL / bez GetParameters) → vyčistit CacheClass, ať se při
        // příštím Loaded neprovede Init se stale InitParams předchozí agendy.
        if (!_vm.HasParameters || _vm.CurrentAgendaCacheClass is null)
        {
            ParamsTabForm.CacheClass = "";
            return;
        }

        ParamsTabForm.InitParams = _vm.BuildAgendaParamsInitParams();
        if (!_paramsTabInitHooked)
        {
            ParamsTabForm.SchemaLoaded += OnParamsTabInitialized;
            _paramsTabInitHooked = true;
        }
        ParamsTabForm.CacheClass = "ALVA.TemplateDesigner.EDInputForm.AgendaParams";

        // Init() = synchronní Caché round-trip. Voláme ho jen když je control už Loaded (přepnutí
        // agendy za běhu). Při prvním zobrazení tabu (a v konstruktoru okna) ho spustí EDInputForm
        // Loaded auto-init s aktuálními InitParams - jinak by transientní chyba Caché propadla ven
        // z konstruktoru okna a aplikace by se neotevřela (R1/R2). Chyba jde do status baru, ne do pádu.
        if (!ParamsTabForm.IsLoaded) return;
        try { ParamsTabForm.Init(); }
        catch (System.Exception ex) { _vm.StatusText = $"⚠ Parametry sestavy se nepodařilo načíst: {ex.Message}"; }
    }

    private void OnParamsTabInitialized()
    {
        foreach (var field in (ParamsTabForm.Items ?? []).OfType<Field>())
            field.PropertyChanged += OnParamsTabFieldChanged;
        ApplyParamsTabVisibility();
    }

    private void OnParamsTabFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null || sender is not Field field || e.PropertyName != nameof(Field.Value)) return;
        _vm.SetAgendaParamValue(field.Key, field.Value ?? "");
        RefreshParamsTabInitParams();  // jinak se hodnota po přepnutí tabu vrátí na starou
        ApplyParamsTabVisibility();   // visibleWhen (např. Hodiny od/do jen ve „Vlastní rozsah")
    }

    /// <summary>Přepíše hodnotu jednoho pole v tabu parametrů (bez `Init()`, který je synchronní
    /// round-trip na Caché). Zápis vyvolá FieldChanged, ale ViewModel stejnou hodnotu zahodí.</summary>
    private void SyncParamsTabField(string key, string value)
    {
        RefreshParamsTabInitParams();
        var field = (ParamsTabForm.Items ?? []).OfType<Field>().FirstOrDefault(f => f.Key == key);
        if (field is not null && field.Value != value) field.Value = value;
    }

    /// <summary>
    /// Přenese aktuální hodnoty parametrů do <c>InitParams</c>. **Nutné po každé změně hodnoty**:
    /// formulář se sám inicializuje na `Loaded`, tedy i při návratu na tab, a bere je odtud - se
    /// starým snímkem by se zobrazená hodnota vrátila na tu, se kterou se tab stavěl naposledy,
    /// zatímco náhled i tisk už jedou podle nové.
    /// </summary>
    private void RefreshParamsTabInitParams()
    {
        if (_vm is null || string.IsNullOrEmpty(ParamsTabForm.CacheClass)) return;
        ParamsTabForm.InitParams = _vm.BuildAgendaParamsInitParams();
    }

    private void ApplyParamsTabVisibility()
    {
        if (_vm is null) return;
        foreach (var field in (ParamsTabForm.Items ?? []).OfType<Field>())
        {
            var target = _vm.IsAgendaParamVisible(field.Key) ? ItemVisibility.Visible : ItemVisibility.Collapsed;
            if (field.Visibility != target) field.Visibility = target;
        }
    }

    private bool _closeConfirmed;

    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_closeConfirmed || _vm is null) return;
        if (!_vm.IsDirty) return;   // čisté okno → nech zavřít normálně (žádný cancel, žádný async)

        // Dirty: uložení do DB je async → zrušit toto zavření, počkat na potvrzení, pak zavřít
        // programově. Close() PŘES Dispatcher - až po doběhnutí Closing eventu; jinak WPF hodí
        // „nelze volat Close při zavírání" i když confirm skončí synchronně (volba „Ne").
        e.Cancel = true;
        try
        {
            if (await _vm.ConfirmUnsavedChangesAsync())
            {
                _closeConfirmed = true;
                Dispatcher.BeginInvoke(new Action(Close));
            }
        }
        catch (System.Exception ex)
        {
            // R2: uložení do DB mohlo selhat (výpadek sítě) → hláška, okno zůstane otevřené.
            _vm.PopupDisplayer.Error(ex);
        }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MenuPageProps_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _ = OpenPropertiesForPage(_vm.State.CurrentTemplate);
    }

    private void MenuUndo_Click(object sender, RoutedEventArgs e)  => _vm!.UndoCommand.Execute(null);
    private void MenuRedo_Click(object sender, RoutedEventArgs e)  => _vm!.RedoCommand.Execute(null);
    private void MenuDelete_Click(object sender, RoutedEventArgs e) => _vm!.DeleteCommand.Execute(null);
    private void MenuHelp_Click(object sender, RoutedEventArgs e)  => ShowHelp();

    /// <summary>Naplní DEV submenu firem z pevného seznamu ve VM. Viditelné jen se zapnutým
    /// <c>dev.firmSwitch</c> v configu a jen když je připojení k databázi.</summary>
    private void BuildDevFirmMenu()
    {
        bool devMode = AppSettings.Current?.Dev.FirmSwitch == true;
        bool show = devMode && _vm?.CurrentFirmIdfa is not null;
        MenuDevSep.Visibility = MenuDevSwitchFirm.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show) return;

        MenuDevSwitchFirm.Items.Clear();
        foreach (var (code, label) in TemplateDesignerViewModel.DevFirms)
        {
            var item = new MenuItem { Header = label, Tag = code, IsCheckable = true };
            item.Click += MenuDevSwitchFirm_Click;
            MenuDevSwitchFirm.Items.Add(item);
        }
        RefreshDevFirmMenuChecks();
    }

    private void MenuDevSwitchFirm_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null || sender is not MenuItem item || item.Tag is not string code) return;
        _vm.DevSwitchFirm(code);
        RefreshDevFirmMenuChecks();
    }

    /// <summary>Označí zaškrtnutím aktuální firmu; ostatní odškrtne.</summary>
    private void RefreshDevFirmMenuChecks()
    {
        var current = _vm?.CurrentFirmIdfa;
        foreach (var mi in MenuDevSwitchFirm.Items.OfType<MenuItem>())
            mi.IsChecked = mi.Tag is string c && string.Equals(c, current, StringComparison.Ordinal);
    }

    private async void MenuAiAssistant_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        try
        {
            var (apiKey, aiReason) = await TryGetAiApiKeyAsync();
            var aiVm   = new AiTemplateAssistantDialogViewModel(apiKey, _vm.SchemaProvider, aiReason);
            var dialog = new AiTemplateAssistantDialog(aiVm) { Owner = this };

            if (dialog.ShowDialog() == true && aiVm.GeneratedTemplate is not null)
            {
                if (!await _vm.ConfirmUnsavedChangesAsync()) return;
                _vm.LoadGeneratedTemplate(aiVm.GeneratedTemplate);
            }
        }
        catch (System.Exception ex)
        {
            _vm.PopupDisplayer.Error(ex);   // R2: async void → chyba přes popup, ne pád
        }
    }

    private bool _aiKeyFetchRunning;

    /// <summary>
    /// Vyzvedne AI klíč (lokální / mlweb přes EdisonAiService). Klíč null = AI nedostupné,
    /// Reason nese uživatelský důvod (limit, chybí konfigurace…) - dialogy ho zobrazí,
    /// ale samy fungují dál (generátor, PDF import mají AI jako podmíněný krok).
    /// Cold-start = Caché config + HTTP resolve (sekundy, timeout 60 s) → wait-cursor + status,
    /// guard proti opakovanému kliku do menu během volání.
    /// </summary>
    private async Task<(string? Key, string? Reason)> TryGetAiApiKeyAsync()
    {
        if (_aiKeyFetchRunning) return (null, "Probíhá předchozí požadavek - chvíli strpení.");
        _aiKeyFetchRunning = true;
        Mouse.OverrideCursor = Cursors.Wait;
        if (_vm is not null) _vm.StatusText = "Ověřuji dostupnost AI služby…";
        try
        {
            return (await Services.EdisonAiService.GetApiKeyAsync(), null);
        }
        catch (Services.EdisonAiException ex)
        {
            return (null, ex.Message);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            if (_vm is not null) _vm.StatusText = "";
            _aiKeyFetchRunning = false;
        }
    }

    private async void MenuImportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        try
        {
            var (apiKey, aiReason) = await TryGetAiApiKeyAsync();
            var vm = new PdfImportDialogViewModel(_vm.SchemaProvider, apiKey, _vm.State.CurrentTemplate?.AgendaCode, aiReason);
            var dialog = new PdfImportDialog(vm) { Owner = this };

            if (dialog.ShowDialog() == true && vm.ImportedTemplate is not null)
            {
                if (!await _vm.ConfirmUnsavedChangesAsync()) return;
                await _vm.LoadGeneratedTemplateAsync(vm.ImportedTemplate);
            }
        }
        catch (System.Exception ex)
        {
            _vm.PopupDisplayer.Error(ex);   // R2: async void → chyba přes popup, ne pád
        }
    }

    /// <summary>Aktualizuje strom pokud je viditelný (po změně výběru na canvasu).</summary>
    private void RefreshTreeIfVisible()
    {
        if (TabTree?.IsSelected == true)
            TreePanel.Rebuild();
    }

    private void BtnZForward_Click(object sender, RoutedEventArgs e)  => _vm!.ChangeZIndex(+1, false);
    private void BtnZBackward_Click(object sender, RoutedEventArgs e) => _vm!.ChangeZIndex(-1, false);
    private void BtnZFront_Click(object sender, RoutedEventArgs e)    => _vm!.ChangeZIndex(+1, true);
    private void BtnZBack_Click(object sender, RoutedEventArgs e)     => _vm!.ChangeZIndex(-1, true);

    private void BtnAlignLeft_Click(object sender, RoutedEventArgs e)    => _vm!.AlignElements(AlignMode.Left);
    private void BtnAlignRight_Click(object sender, RoutedEventArgs e)   => _vm!.AlignElements(AlignMode.Right);
    private void BtnAlignCenterH_Click(object sender, RoutedEventArgs e) => _vm!.AlignElements(AlignMode.CenterH);
    private void BtnAlignTop_Click(object sender, RoutedEventArgs e)     => _vm!.AlignElements(AlignMode.Top);
    private void BtnAlignBottom_Click(object sender, RoutedEventArgs e)  => _vm!.AlignElements(AlignMode.Bottom);
    private void BtnAlignMiddleV_Click(object sender, RoutedEventArgs e) => _vm!.AlignElements(AlignMode.MiddleV);
    private void BtnDistributeH_Click(object sender, RoutedEventArgs e)  => _vm!.AlignElements(AlignMode.DistributeH);
    private void BtnDistributeV_Click(object sender, RoutedEventArgs e)  => _vm!.AlignElements(AlignMode.DistributeV);
    private void MenuNormalizePositions_Click(object sender, RoutedEventArgs e) => OpenNormalizePositionsDialog();
    private void MenuCopyStyle_Click(object sender, RoutedEventArgs e)          => _vm?.CopyStyle();
    private void MenuPasteStyle_Click(object sender, RoutedEventArgs e)         => _vm?.PasteStyle();
    private void MenuPrevzitVychoziFont_Click(object sender, RoutedEventArgs e) => _vm?.PrevzitVychoziFont();

    // ─────────────────────────────────────────────────────────────────────────
    // Add section / element menus
    // ─────────────────────────────────────────────────────────────────────────

    private void BtnAddPage_Click(object sender, RoutedEventArgs e) => _vm?.AddPage();

    private void BtnAddSection_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var menu = new ContextMenu();

        void Add(string label, Func<Section> factory)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => _vm.AddSection(factory());
            menu.Items.Add(item);
        }

        void Sep() => menu.Items.Add(new Separator());

        Add("Hlavička stránky", () => new Section
        {
            Name = "Hlavička stránky", PrintBehavior = SectionPrintBehavior.OnEveryPage,
            Role = SectionRole.Header,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 25f
        });
        Add("Patička stránky", () => new Section
        {
            Name = "Patička stránky", PrintBehavior = SectionPrintBehavior.OnEveryPage,
            Role = SectionRole.Footer,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 15f
        });
        Add("Oddělovač", () => new Section
        {
            Name = "Oddělovač", PrintBehavior = SectionPrintBehavior.Once, DesignerHeight = 5f
        });
        Sep();
        Add("Hlavička sestavy (jen 1. str.)", () => new Section
        {
            Name = "Hlavička sestavy", PrintBehavior = SectionPrintBehavior.OnFirstPageOnly,
            Role = SectionRole.Header,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 30f
        });
        Add("Patička sestavy (jen posl. str.)", () => new Section
        {
            Name = "Patička sestavy", PrintBehavior = SectionPrintBehavior.OnLastPageOnly,
            Role = SectionRole.Footer,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 20f
        });
        Add("Hlavička kromě 1. str.", () => new Section
        {
            Name = "Hlavička kromě 1. str.", PrintBehavior = SectionPrintBehavior.OnEveryPageExceptFirst,
            Role = SectionRole.Header,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 25f
        });
        Sep();

        var customTemplates = _vm.GetCustomSectionTemplateNames();
        if (customTemplates.Count > 0)
        {
            foreach (var templateName in customTemplates)
            {
                var name = templateName;
                Add($"💾 {templateName}", () =>
                {
                    var t = _vm.LoadCustomSectionTemplate(name);
                    if (t is null)
                    {
                        _vm.PopupDisplayer.Info($"Šablonu „{name}“ se nepodařilo načíst.");
                        return new Section { Name = "Chyba" };
                    }
                    return t;
                });
            }
            Sep();
        }

        Add("⚙ Prázdná sekce", () => new Section
        {
            Name = $"Sekce {_vm.State.CurrentTemplate.Sections.Count + 1}",
            PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 30f
        });

        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    private void BtnAddElement_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (_vm.State.ActiveSection is null)
        {
            _vm.PopupDisplayer.Info("Nejprve vyberte sekci kliknutím v levém panelu nebo na canvasu.");
            return;
        }

        var menu = new ContextMenu();
        void Add(string label, Func<TemplateElement?> factory)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) =>
            {
                // Null = uživatel vkládání zrušil (rozměr nové tabulky).
                if (factory() is not { } element) return;
                _vm.Execute(new AddElementCommand(_vm.State.ActiveSection!, element));
                _vm.State.SelectedElements.Clear();
                _vm.State.SelectedElements.Add(element);
                UpdatePropertiesPanel();
                InvalidateCanvas();
            };
            menu.Items.Add(item);
        }

        Add("Text",        () => TemplateDesignerViewModel.CreateElementByType(ElementTypes.Text, 5, 5));
        Add("Obdélník",   () => TemplateDesignerViewModel.CreateElementByType(ElementTypes.Rectangle, 5, 5));
        Add("Čára",       () => TemplateDesignerViewModel.CreateElementByType(ElementTypes.Line, 0, 10));
        Add("Obrázek",    () => TemplateDesignerViewModel.CreateElementByType(ElementTypes.Image, 5, 5));
        Add("Kód (QR, čárový)", () => TemplateDesignerViewModel.CreateElementByType(ElementTypes.Barcode, 5, 5));
        Add("Tabulka (z dat)", () => TemplateDesignerViewModel.CreateElementByType(ElementTypes.DynamicTable, 0, 0));
        Add("Tabulka (pevná)", () => _vm.CreateElementInteractive(ElementTypes.Table, 0, 0));

        // JŘ elementy dle capability agendy (druh z registru → typy; stejný kontext jako paleta).
        var kind = _vm.CurrentAgendaElementKind;
        menu.Items.Add(new Separator());
        if (AgendaCapabilities.Supports(kind, ElementTypes.StopList))
            Add("Seznam zastávek",    () => TemplateDesignerViewModel.CreateElementByType(ElementTypes.StopList, 0, 0));
        if (AgendaCapabilities.Supports(kind, ElementTypes.DepartureGrid))
            Add("Minuty odjezdů",     () => TemplateDesignerViewModel.CreateElementByType(ElementTypes.DepartureGrid, 0, 0));
        // Hlavní tabulka sestavy jde před poznámkami - poznámky jsou doplněk. U zastávkových to
        // pořadí drží samo (seznam zastávek, odjezdy, poznámky), u linkových by poznámky vyšly
        // první, protože jejich tabulka je v tomhle výčtu za nimi.
        if (AgendaCapabilities.Supports(kind, ElementTypes.LineTable))
            Add("Tabulka spojů (zastávky × spoje)", () => TemplateDesignerViewModel.CreateElementByType(ElementTypes.LineTable, 0, 0));
        if (AgendaCapabilities.Supports(kind, ElementTypes.NotesLegend))
            Add("Poznámky",           () => _vm.CreateElementInteractive(ElementTypes.NotesLegend, 0, 0));

        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Element palette drag
    // ─────────────────────────────────────────────────────────────────────────

    private void ElementPalette_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string type)
        {
            _addingElementType = type;
            _addingTargetSection = null;
            _addingGhostPos = default;
            _mouseMode = MouseMode.AddingElement;
            skCanvas.CaptureMouse();
            skCanvas.Cursor = Cursors.Cross;
            e.Handled = true;
        }
    }

    private void SchemaTree_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _fieldDragCandidate = null;
        if (e.OriginalSource is DependencyObject source)
        {
            // FindVisualParent místo ContainerFromElement - ten vrací rodičovskou skupinu u vnořených TreeView položek
            var item = FindVisualParent<TreeViewItem>(source);
            if (item?.DataContext is SchemaFieldVM field)
            {
                _fieldDragStartPoint = e.GetPosition(SchemaTree);
                _fieldDragCandidate = field;
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var current = System.Windows.Media.VisualTreeHelper.GetParent(child);
        while (current is not null)
        {
            if (current is T result) return result;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void SchemaTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_fieldDragCandidate is null || e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(SchemaTree);
        if (Math.Abs(pos.X - _fieldDragStartPoint.X) < 5 && Math.Abs(pos.Y - _fieldDragStartPoint.Y) < 5)
            return;

        // Threshold překročen - zahájit drag
        _addingFieldName = _fieldDragCandidate.FieldName;
        _addingElementType = null;
        _addingTargetSection = null;
        _addingTargetElement = null;
        _addingGhostPos = default;
        _fieldDragCandidate = null;
        _mouseMode = MouseMode.AddingElement;
        skCanvas.Cursor = Cursors.Cross;
        e.Handled = true;
    }

    /// <summary>
    /// Window-level PreviewMouseMove - field drag přes celé okno.
    /// TreeView přebíjí CaptureMouse na skCanvas, proto nelze spoléhat na canvas-level events.
    /// </summary>
    private void OnWindowPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_mouseMode != MouseMode.AddingElement || _addingFieldName is null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { EndAddingElement(); return; }

        var pos = e.GetPosition(skCanvas);
        float cx = (float)pos.X, cy = (float)pos.Y, cw = (float)skCanvas.ActualWidth;

        _addingGhostPos = new SKPoint(cx, cy);
        _addingTargetSection = _canvas!.HitTestSection(cx, cy, cw);

        _addingTargetElement = null;
        var hit = _canvas.HitTestElement(cx, cy, cw);
        if (hit?.Element is TextElement)
            _addingTargetElement = hit.Value.Element;

        InvalidateCanvas();
    }

    /// <summary>
    /// Window-level PreviewMouseLeftButtonUp - dokončí field drag.
    /// </summary>
    private void OnWindowPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_mouseMode != MouseMode.AddingElement || _addingFieldName is null) return;
        EndAddingElement();
    }

    private void ResetAddingElementState()
    {
        _mouseMode = MouseMode.Idle;
        _addingElementType = null;
        _addingFieldName = null;
        _addingTargetSection = null;
        _addingTargetElement = null;
        _fieldDragCandidate = null;
        skCanvas.ReleaseMouseCapture();
        skCanvas.Cursor = Cursors.Arrow;
    }

    private void CancelAddingElement()
    {
        ResetAddingElementState();
        InvalidateCanvas();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Schema tree double-click
    // ─────────────────────────────────────────────────────────────────────────

    private void LeftTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TabFields?.IsSelected == true)
            _vm?.RefreshFieldsList();
        if (TabTree?.IsSelected == true)
            TreePanel.Rebuild();
    }

    private void SchemaTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null) return;
        if (SchemaTree.SelectedItem is not SchemaFieldVM field) return;

        _vm.InsertOrCopyPlaceholder(field.Placeholder);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Save section as template (UI dialog - stays in code-behind)
    // ─────────────────────────────────────────────────────────────────────────

    private void SaveSectionAsTemplate(Section section)
    {
        if (_vm is null) return;
        var dlgVm = new SaveSectionTemplateDialogViewModel(section.Name);
        var dlg   = new SaveSectionTemplateDialog(dlgVm) { Owner = this };
        if (dlg.ShowDialog() == true)
            _vm.SaveSectionTemplate(section, dlgVm.Name);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    // ── Properties dialog helpers ────────────────────────────────────────────

    /// <summary>Filtr hledání, který si pamatují okna vlastností prvku otevřená z plátna.</summary>
    /// <remarks>Uživatel ladí jednu vlastnost: vyhledá ji, změní, zavře okno kvůli náhledu a otevře
    /// znovu - bez paměti by hledal pokaždé znovu (Jan 23. 9.). Jen tady: panel vlastností, sekce,
    /// stránka ani buňka tabulky ho nesdílí.</remarks>
    private const string ElementDialogSearchKey = "TemplateDesigner.ElementDialog";

    private void OpenPropertiesForElement(TemplateElement element)
    {
        if (element is DepartureGridElement depEl)
        {
            OpenDepartureGridDialog(depEl);
            return;
        }
        if (element is StopListElement stopEl)
        {
            OpenStopColumnsDialog(stopEl);
            return;
        }
        if (element is DynamicTableElement dynTable)
        {
            OpenDynamicTableDialog(dynTable);
            return;
        }
        if (element is LineTableElement lineEl)
        {
            OpenLineTableColumnsDialog(lineEl);
            return;
        }
        // Generický EDInputForm dialog pro elementy s Caché schema (key = property path).
        // Mapping element type → (cacheClass, title). Ostatní typy fallback na legacy
        // ElementPropertiesWindow (PropertyDescriptor panel).
        if (EDInputFormSchema(element) is var (cacheClass, title) && cacheClass is not null)
        {
            OpenGenericElementEditDialog(element, cacheClass, title!);
            return;
        }

        _propsVm!.PrepareForElement(element, isNewSession: true);
        new ElementPropertiesWindow(_propsVm) { Owner = this }.ShowDialog();
        InvalidateCanvas();
        InvalidatePreview();
    }

    private void OpenDepartureGridDialog(DepartureGridElement element)
    {
        // Dostupné skupiny z preview dat
        var previewData = _vm!.State.CurrentPreviewData;
        var availableGroups = element.GetAvailableSkupinyDnu(previewData);

        // Vzory sloupců staví server z konfigurace linky - klient je nesestavuje sám, aby
        // paritní test hlídal to, co editor reálně pošle (bez dat se vzory nenabízejí).
        var presetsJson = previewData?.GetValue("PRESETY_SLOUPCU")?.ToString();

        var dlg = new DepartureGroupEditorDialog(
            element, availableGroups, element.GroupColumns, element.HourColumnStyle,
            element.CustomColumns, presetsJson)
            { Owner = this, SearchMemoryKey = ElementDialogSearchKey };

        if (dlg.ShowDialog() != true) return;   // Cancel - nic se neaplikovalo (Obecné batchuje na OK)

        _vm.ApplyDepartureGridEdit(element, dlg.GeneralCommand, dlg.StyleCommands,
            dlg.ResultGroupColumns, availableGroups.Count > 0, dlg.ResultHourColumnStyle,
            dlg.ResultCustomColumns);

        InvalidateCanvas();
        InvalidatePreview();
        UpdatePropertiesPanel();
    }

    /// <summary>Mapping element type → (Caché schema class, titulek) pro generický
    /// EDInputForm editor. Vrací (null, null) pro typy bez schema (fallback na legacy).</summary>
    private static (string? cacheClass, string? title) EDInputFormSchema(TemplateElement element) => element switch
    {
        Models.Elements.TextElement      => ("ALVA.TemplateDesigner.EDInputForm.TextElementEdit", "Vlastnosti textu"),
        Models.Elements.LineElement      => ("ALVA.TemplateDesigner.EDInputForm.LineElementEdit", "Vlastnosti čáry"),
        Models.Elements.RectangleElement   => ("ALVA.TemplateDesigner.EDInputForm.RectangleElementEdit", "Vlastnosti obdélníku"),
        Models.Elements.BarcodeElement     => ("ALVA.TemplateDesigner.EDInputForm.BarcodeElementEdit", "Vlastnosti kódu"),
        Models.Elements.NotesLegendElement => ("ALVA.TemplateDesigner.EDInputForm.NotesLegendElementEdit", "Vlastnosti poznámek"),
        Models.Elements.ImageElement       => ("ALVA.TemplateDesigner.EDInputForm.ImageElementEdit", "Vlastnosti obrázku"),
        // Pevná tabulka má jediný tab (bez sloupců a řádků) - strukturu i obsah dělá plátno.
        Models.Elements.TableElement       => ("ALVA.TemplateDesigner.EDInputForm.TableElementEdit", "Vlastnosti tabulky"),
        // LineTable NENÍ zde - má vlastní 2-tab dialog (Obecné + Sloupce), viz OpenLineTableColumnsDialog.
        _ => (null, null)
    };

    /// <summary>Caché *Edit.cls schéma pro inline panel - pokrývá i komplexní elementy
    /// (StopList/DepartureGrid/Dynamic), jejichž „Obecné" tab modálu jede na tomtéž schématu.
    /// Sloupce komplexních zůstávají jen v dvojkliku (plný 2-tab dialog).</summary>
    private static string? PanelCacheClass(TemplateElement element)
    {
        var (cc, _) = EDInputFormSchema(element);
        if (cc is not null) return cc;
        return element switch
        {
            Models.Elements.StopListElement      => "ALVA.TemplateDesigner.EDInputForm.StopListElementEdit",
            Models.Elements.DepartureGridElement => "ALVA.TemplateDesigner.EDInputForm.DepartureGridElementEdit",
            Models.Elements.DynamicTableElement  => "ALVA.TemplateDesigner.EDInputForm.DynamicTableElementEdit",
            Models.Elements.LineTableElement     => "ALVA.TemplateDesigner.EDInputForm.LineTableElementEdit",
            _ => null
        };
    }

    private void OpenGenericElementEditDialog(TemplateElement element, string cacheClass, string title)
    {
        var dlg = new ElementEditDialog(element, cacheClass, title)
            { Owner = this, PlaceholderFields = _vm!.PlaceholderFieldItems(), SearchMemoryKey = ElementDialogSearchKey };
        if (element is NotesLegendElement poznamky)
        {
            bool linkovy = _vm.JeLinkovyJr;
            dlg.ViditelnostPole = (klic, _) => NotesLegendElement.PoleFormulareViditelne(klic, linkovy);
            dlg.UpravaPoli = pole => NastavVolbyRazeni(pole, linkovy);
            // Okno vlastností zapisuje až s Uložit - rozpracované druhy si proto drží samo,
            // aby druhé otevření ukázalo první úpravu.
            var rozpracovane = poznamky.EfektivniSkupiny();
            dlg.AkceTlacitka = klic =>
            {
                if (klic != NotesLegendElement.SkupinyPoznamekKlic
                    || VyberSkupinyPoznamek(poznamky, rozpracovane, dlg) is not { } skupiny) return null;
                rozpracovane = skupiny;
                return TemplateDesignerViewModel.SkupinyPoznamekCommand(poznamky, skupiny);
            };
        }
        if (dlg.ShowDialog() == true && dlg.ResultCommand is not null)
        {
            _vm!.Execute(dlg.ResultCommand);
        }
        InvalidateCanvas();
        InvalidatePreview();
        UpdatePropertiesPanel();
    }

    private void OpenStopColumnsDialog(StopListElement element)
    {
        var dlg = new StopColumnsEditorDialog(element) { Owner = this, SearchMemoryKey = ElementDialogSearchKey };

        if (dlg.ShowDialog() == true)
        {
            _vm!.ApplyStopColumnsEdit(element, dlg.GeneralCommand, dlg.ResultColumns);
            if (dlg.HighlightEdited) _vm!.ApplyHighlightRulesEdit(element, dlg.ResultHighlightRules ?? []);
        }

        InvalidateCanvas();
        InvalidatePreview();
        UpdatePropertiesPanel();
    }

    /// <summary>EDInputForm button v inline panelu - otevře editor pravidel zvýraznění.</summary>
    private void OnInlineElementButtonClicked(string key)
    {
        // Z formuláře buňky zpátky na nastavení celé tabulky (= odchod z buňky, jako Esc).
        if (key == "__tableSettings") { ExitToTableSettings(); return; }

        if (key == NotesLegendElement.SkupinyPoznamekKlic)
        {
            if (_vm!.State.SelectedElements is [NotesLegendElement poznamky]
                && VyberSkupinyPoznamek(poznamky, poznamky.EfektivniSkupiny()) is { } skupiny)
            {
                _vm.Execute(TemplateDesignerViewModel.SkupinyPoznamekCommand(poznamky, skupiny));
                InvalidateCanvas();
                InvalidatePreview();
            }
            return;
        }

        if (key != "__highlightRules") return;
        if (_vm!.State.SelectedElements is [IRowHighlightable h])
            OpenHighlightRulesDialog(h);
    }

    /// <summary>Okno „Druhy poznámek"; null = Storno.</summary>
    private List<NastaveniSkupinyPoznamek>? VyberSkupinyPoznamek(NotesLegendElement poznamky,
        IReadOnlyList<NastaveniSkupinyPoznamek> vychozi, Window? owner = null)
    {
        var dlg = new SkupinyPoznamekDialog(vychozi, poznamky.Columns) { Owner = owner ?? this };
        return dlg.ShowDialog() == true ? dlg.Vysledek : null;
    }

    private void OpenHighlightRulesDialog(IRowHighlightable element)
    {
        var dlg = new HighlightRulesEditorDialog(element.HighlightRules) { Owner = this };

        if (dlg.ShowDialog() == true)
            _vm!.ApplyHighlightRulesEdit(element, dlg.ResultRules);

        InvalidateCanvas();
        InvalidatePreview();
        UpdatePropertiesPanel();
    }

    private void OpenLineTableColumnsDialog(LineTableElement element)
    {
        var dlg = new LineTableColumnsEditorDialog(element) { Owner = this, SearchMemoryKey = ElementDialogSearchKey };

        if (dlg.ShowDialog() == true)
        {
            _vm!.ApplyLineTableColumnsEdit(element, dlg.GeneralCommand, dlg.ResultColumns);
            if (dlg.HighlightEdited) _vm!.ApplyHighlightRulesEdit(element, dlg.ResultHighlightRules ?? []);
        }

        InvalidateCanvas();
        InvalidatePreview();
        UpdatePropertiesPanel();
    }

    private void OpenDynamicTableDialog(DynamicTableElement element)
    {
        var collections = _vm!.GetAvailableGroups();
        var dlg = new DynamicTableEditorDialog(element, collections) { Owner = this, SearchMemoryKey = ElementDialogSearchKey };

        if (dlg.ShowDialog() == true)
        {
            _vm!.ApplyDynamicTableEdit(element, dlg.GeneralCommand, dlg.ResultColumns);
            if (dlg.HighlightEdited) _vm!.ApplyHighlightRulesEdit(element, dlg.ResultHighlightRules ?? []);
        }

        InvalidateCanvas();
        InvalidatePreview();
        UpdatePropertiesPanel();
    }

    /// <summary>
    /// Pole formuláře sekce, která v dané kombinaci nic nedělají, se schovají (rozhodnutí Jan 24. 9.):
    /// barva a tloušťka bez rámečku, Stránky jinde než u hlavičky a patičky (obsah se tiskne jednou,
    /// pod obsahem vždy na všech stránkách), Přetečení a nový list u opakované sekce (ta se nedělí
    /// a opakuje se na každém listu). Do `B18` pravidla drží návrhář, formulář z Caché viditelnost neumí.
    /// </summary>
    internal static bool ViditelnostPoleSekce(string klic, IReadOnlyDictionary<string, string> hodnoty)
    {
        string typ = hodnoty.GetValueOrDefault(nameof(Section.Typ)) ?? "";
        bool obsah = typ is "" or nameof(TypSekce.Obsah);
        return klic switch
        {
            nameof(Section.BorderColor) or nameof(Section.BorderWidth)
                => hodnoty.GetValueOrDefault(nameof(Section.ShowBorder)) == "1",
            nameof(Section.Stranky) => typ is nameof(TypSekce.HlavickaStranky) or nameof(TypSekce.PatickaStranky),
            nameof(Section.OverflowBehavior) or nameof(Section.ZacitNaNoveStrance) => obsah,
            _ => true,
        };
    }

    /// <summary>Nabídka „Řazení poznámek" podle agendy šablony (<see cref="NotesLegendElement.VolbyRazeni"/>).
    /// Nastavuje se vždy celá, protože panel drží týž formulář i pro prvek z jiné šablony.</summary>
    internal static void NastavVolbyRazeni(IEnumerable<Item>? pole, bool linkovyJr)
    {
        if ((pole ?? []).OfType<Field>().FirstOrDefault(f => f.Key == nameof(NotesLegendElement.Razeni)) is not { } razeni)
            return;
        razeni.ListItems = [.. NotesLegendElement.VolbyRazeni(linkovyJr, razeni.Value ?? "")
            .Select(v => new EDControls.ListItem(v.Hodnota, v.Popis))];
    }

    /// <summary>Klik na hlášku vpravo dole vybere její prvek na plátně (`B178`).</summary>
    private void VyberPrvekZHlaseni(Guid id)
    {
        if (_vm is null) return;
        var sekce = _vm.State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Any(e => e.Id == id));
        var prvek = sekce?.Elements.First(e => e.Id == id);
        if (sekce is null || prvek is null) return;
        _vm.State.SelectedElements.Clear();
        _vm.State.SelectedElements.Add(prvek);
        _vm.ActivateSection(sekce);
        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    private void OpenPropertiesForSection(Section section)
    {
        var dlg = new ElementEditDialog(
            section,
            "ALVA.TemplateDesigner.EDInputForm.SectionEdit",
            "Vlastnosti sekce: " + section.Name)
            {
                Owner = this, PlaceholderFields = _vm!.PlaceholderFieldItems(),
                ViditelnostPole = ViditelnostPoleSekce,
            };
        if (dlg.ShowDialog() == true && dlg.ResultCommand is not null)
        {
            _vm!.Execute(dlg.ResultCommand);
            _vm!.SrazVyskuSekceDoMezi(section);
        }
        InvalidateCanvas();
        InvalidatePreview();
        _vm!.RebuildSections();
    }

    private void OpenPropertiesForMultipleElements(List<TemplateElement> elements)
    {
        _propsVm!.PrepareForMultipleElements(elements);
        new ElementPropertiesWindow(_propsVm) { Owner = this }.ShowDialog();
        InvalidateCanvas();
        InvalidatePreview();
    }

    // Property-path klíče ve FinalValues (= ElementFormBinder.Snapshot cesta) - MUSÍ odpovídat
    // property cestě na Template.PageSize. Sdílená konstanta, ať detekce změny rozměru neselže tiše
    // při přejmenování pole formuláře.
    private const string PagePathWidth  = "PageSize.Width";
    private const string PagePathHeight = "PageSize.Height";

    private async Task OpenPropertiesForPage(Template template)
    {
        try
        {
            float oldW = template.PageSize.Width, oldH = template.PageSize.Height;

            var dlg = new ElementEditDialog(
                template,
                "ALVA.TemplateDesigner.EDInputForm.PageEdit",
                "Vlastnosti stránky") { Owner = this };
            if (dlg.ShowDialog() != true) return;

            // Změna velikosti / orientace stránky přepočítá obsah na novou velikost (B104) - dřív
            // šablonu vymazala. Ostatní změny (okraje, kultura, font) jdou jako běžná vratná změna.
            var fv = dlg.FinalValues;

            // Pojistka: dialog vrátil hodnoty, ale klíče rozměrů chybí → detekce resetu by tiše selhala.
            if (fv is not null && (!fv.ContainsKey(PagePathWidth) || !fv.ContainsKey(PagePathHeight)))
                System.Diagnostics.Debug.WriteLine(
                    $"[OpenPropertiesForPage] FinalValues bez klíče rozměrů ({PagePathWidth}/{PagePathHeight}) - reset detekce vypnutá?");

            bool dimsChanged = fv is not null
                && (DiffMm(oldW, ParseMm(fv, PagePathWidth, oldW))
                    || DiffMm(oldH, ParseMm(fv, PagePathHeight, oldH)));

            if (dimsChanged)
            {
                if (!await _vm!.ChangePageFormatAsync(fv!)) return;
                UpdateCanvasSize();
                InvalidateCanvas();
                InvalidatePreview();
                return;
            }

            if (dlg.ResultCommand is not null) _vm!.Execute(dlg.ResultCommand);
            UpdateCanvasSize();
            InvalidateCanvas();
            InvalidatePreview();
        }
        catch (Exception ex)
        {
            _vm?.PopupDisplayer.Error(ex);   // R2: chyby přes IPopupDisplayer, ne pád async void
        }
    }

    private static float ParseMm(IReadOnlyDictionary<string, string> fields, string key, float fallback) =>
        fields.TryGetValue(key, out var s)
        && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v : fallback;

    private static bool DiffMm(float a, float b) => Math.Abs(a - b) > 0.1f;

    private void UpdatePropertiesPanel()
    {
        if (_vm is null) return;

        RefreshTreeIfVisible();
        _vm.UpdateStatusForSelection();
        RefreshInlineProperties();
    }

    private void RefreshInlineProperties()
    {
        if (_propsVm is null || InlinePropertiesPanel is null) return;

        Action<IDesignerCommand> execute  = cmd => _vm!.Execute(cmd);
        Action                   invalidate = () => { skCanvas.InvalidateVisual(); InvalidatePreview(); };
        InlinePropertiesPanel.SetContext(execute, invalidate);
        InlineElementForm.SetContext(execute, invalidate);
        FormatToolbar.SetContext(execute, invalidate);

        // Uvnitř tabulky patří panel i lišta **vybrané buňce**, ne prvku - nastavení celé tabulky
        // je za tlačítkem v panelu (a přes Esc). Jinak by proti sobě stály dvě sady týchž
        // vlastností s jiným dosahem.
        if (InTableCells && CellFormatting is not null) { SyncFormatToolbar(); return; }

        var selected = _vm!.State.SelectedElements;

        // Jeden element se schématem → EDInputForm panel. Descriptory se pořád počítají
        // pro FormatToolbar. Více / žádný / bez schématu → starý PropertyDescriptor panel.
        string? panelSchema = selected.Count == 1 ? PanelCacheClass(selected[0]) : null;

        if (selected.Count >= 1)
        {
            var descriptors = selected.Count == 1
                ? _propsVm.PrepareForElement(selected[0], isNewSession: false)
                : _propsVm.PrepareForMultipleElements(selected);
            FormatToolbar.ShowDescriptors(descriptors);

            if (panelSchema is not null)
            {
                // Nabídka „vložit pole" - seznam zná jen návrhář (ze schématu agendy), Caché ne.
                InlineElementForm.PlaceholderFields = _vm.PlaceholderFieldItems();
                bool linkovy = _vm.JeLinkovyJr;
                bool poznamky = selected[0] is NotesLegendElement;
                InlineElementForm.ViditelnostPole = poznamky
                    ? (klic, _) => NotesLegendElement.PoleFormulareViditelne(klic, linkovy)
                    : null;
                InlineElementForm.UpravaPoli = poznamky ? pole => NastavVolbyRazeni(pole, linkovy) : null;
                InlineElementForm.ShowElement(selected[0], panelSchema);
                ShowInlineFormPanel(true);
            }
            else
            {
                InlinePropertiesPanel.ShowDescriptors(descriptors);
                ShowInlineFormPanel(false);
            }

            // Přepnout na záložku Vlastnosti pokud jsme na Sekce nebo Strom.
            // Netaháme uživatele pryč z tabů, kde aktivně pracuje s něčím jiným
            // (Datová pole = drag polí, Průvodce = chat, Parametry = nastavení před tiskem).
            // ⚠️ **Parametry sem přibyly až 25. 8.** a chyběly tu od jejich zavedení: změna
            // parametru, která sáhne do šablony (rozvržení sloupců), vyvolá obnovu panelu
            // vlastností - a uživateli to pod rukama odskočilo ze záložky, ve které zrovna
            // přepínal volbu (nahlásil Jan 24. 8.).
            if (LeftTabControl.SelectedItem != TabFields
                && LeftTabControl.SelectedItem != TabAiGuide
                && LeftTabControl.SelectedItem != TabParams)
                LeftTabControl.SelectedItem = TabProperties;
        }
        else
        {
            InlinePropertiesPanel.ShowMessage("Vyberte element na canvasu");
            FormatToolbar.ShowDescriptors(null);
            ShowInlineFormPanel(false);
        }
    }

    /// <summary>Přepne viditelnost mezi EDInputForm panelem (jeden element) a starým PropertyDescriptor
    /// panelem (0 / více / bez schématu).</summary>
    private void ShowInlineFormPanel(bool useForm)
    {
        InlineElementForm.Visibility     = useForm ? Visibility.Visible : Visibility.Collapsed;
        InlinePropertiesPanel.Visibility = useForm ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateTitle()
    {
        if (_vm is null) return;
        string agenda = !string.IsNullOrEmpty(_vm.State.CurrentTemplate.AgendaCode)
            ? $"[{_vm.CurrentAgendaDisplayName}] " : "";
        // Indikátor původu: kam míří Uložit. [DB] = uložená v databázi (Ctrl+S aktualizuje),
        // [neuloženo] = nová/importovaná, ještě není v DB (Ctrl+S ji založí).
        string origin = _vm.IsCacheBacked ? "[DB] " : "[neuloženo] ";
        string name = _vm.State.CurrentTemplate.Name;
        string dirty = _vm.IsDirty ? " *" : "";
        Title = $"{agenda}{origin}{name}{dirty} - Návrhář sestav";
    }

    /// <summary>
    /// Kontextová paleta JŘ elementů dle agendy šablony: zastávkové agendy ukazují
    /// StopList/DepartureGrid/NotesLegend, knižní LineTable; agenda bez JŘ kontextu (GENERAL,
    /// fakturace) žádné - JŘ elementy patří jen ke své agendě.
    /// </summary>
    private void UpdatePaletteForAgenda()
    {
        if (_vm is null) return;
        var kind = _vm.CurrentAgendaElementKind;
        static Visibility Vis(bool b) => b ? Visibility.Visible : Visibility.Collapsed;
        BtnPalStopList.Visibility      = Vis(AgendaCapabilities.Supports(kind, ElementTypes.StopList));
        BtnPalDepartureGrid.Visibility = Vis(AgendaCapabilities.Supports(kind, ElementTypes.DepartureGrid));
        BtnPalNotesLegend.Visibility   = Vis(AgendaCapabilities.Supports(kind, ElementTypes.NotesLegend));
        BtnPalLineTable.Visibility     = Vis(AgendaCapabilities.Supports(kind, ElementTypes.LineTable));
    }

    private void ShowHelp()
    {
        var helpDialog = new HelpDialog { Owner = this };
        helpDialog.ShowDialog();
    }

    private void OpenNormalizePositionsDialog()
    {
        if (_vm is null) return;
        var elements = _vm.State.SelectedElements.Count > 0
            ? (IReadOnlyList<TemplateElement>)_vm.State.SelectedElements
            : _vm.State.CurrentTemplate.Sections.SelectMany(s => s.Elements).ToList();
        if (elements.Count == 0) return;

        var vm  = new NormalizePositionsDialogViewModel(elements);
        var dlg = new NormalizePositionsDialog(vm) { Owner = this };
        if (dlg.ShowDialog() == true)
            _vm.NormalizePositions(elements, vm.Step);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Keyboard handler
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Leží pole uvnitř panelu Pomocníka? Ten si klávesy obsluhuje sám.</summary>
    /// <remarks>Hledá se po vizuálním stromu, ne podle jména pole - jméno by se dalo přejmenovat,
    /// aniž by to někdo spojil s touhle výjimkou.</remarks>
    private static bool JePolePomocnika(DependencyObject? prvek)
    {
        while (prvek is not null)
        {
            if (prvek is Windows.AiGuideDialog.AiGuidePanel) return true;
            prvek = System.Windows.Media.VisualTreeHelper.GetParent(prvek);
        }
        return false;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm is null) return;
        // V inline edit modu nereagujeme na globální zkratky - TextBox si Enter/Esc obslouží sám.
        // Bez tohoto by PreviewKeyDown odchytil Enter dřív než TextBox a otevřel dialog místo commitu.
        if (_inlineEditElement is not null) return;

        // Uvnitř tabulky patří klávesy buňce, ne prvku (jinak by Delete smazal celou tabulku).
        // ⚠️ Jen když si je nenárokuje pole, kam se píše (`CellKeysActive`): režim buňky zůstává
        // zapnutý i po potvrzení textu, takže bez téhle podmínky by po kliknutí do pole v panelu
        // vlastností Delete mazal obsah buňky místo znaku v poli (nález revize 12. 8.).
        if (InTableCells && (_cellEditing || CellKeysActive))
        {
            if (HandleCellFormatKey(e) || HandleCellEditKey(e) || HandleTableCellsKey(e))
            { e.Handled = true; return; }
            if (_cellEditing) return;   // zbytek psaní obslouží textové pole samo
        }
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        // Psaní v textovém poli (inline panel EDInputForm, filtr stromu, …): Enter potvrdí pole
        // (push LostFocus binding → živý write-back), ne otevře dialog vlastností. Ostatní bare
        // klávesy (Delete/šipky/písmena/F2) patří poli, ne globálním zkratkám - jinak by Delete
        // v poli smazal vybraný element. Ctrl/Alt zkratky (Ctrl+Z/S/…) procházejí dál.
        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox focusedField)
        {
            // ⚠️ Pomocník si Enter obsluhuje sám (odeslat dotaz) a bez téhle výjimky by se k němu
            // nedostal: okno tuneluje `PreviewKeyDown` dřív než TextBox a Enter by tu skončil jako
            // potvrzení bindingu (nález Jan 16. 9. - v Pomocníkovi se muselo klikat na Odeslat).
            if (e.Key == Key.Enter && JePolePomocnika(focusedField)) return;
            if (e.Key == Key.Enter)
            {
                focusedField.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                e.Handled = true;
                return;
            }
            if (!ctrl && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return;
        }

        if (e.Key == Key.Z && ctrl)
        {
            _vm.UndoCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Y && ctrl)
        {
            _vm.RedoCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.S && ctrl)
        {
            _vm.SaveCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.N && ctrl && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.NewCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.O && ctrl)
        {
            MenuOpen_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.P && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _ = OpenPropertiesForPage(_vm.State.CurrentTemplate);
            e.Handled = true;
        }
        else if (e.Key == Key.P && ctrl)
        {
            MenuPrint_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.A && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.ChangeAgendaCommand.Execute(null);
            e.Handled = true;
        }
        // Výběr instancí z klávesnice - vedle změny agendy, protože je to táž dvojice voleb
        // „nad čím pracuju". Písmeno I jako instance: agenda si sama říká linka nebo faktura,
        // takže doménové písmeno by platilo jen pro jízdní řády.
        else if (e.Key == Key.I && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.OpenInstancePickerCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.PageUp && ctrl)
        {
            _vm.ChangeZIndex(+1, toExtreme: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
        else if (e.Key == Key.PageDown && ctrl)
        {
            _vm.ChangeZIndex(-1, toExtreme: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
        else if (e.Key == Key.A && ctrl && _vm.State.ActiveSection is not null)
        {
            _vm.State.SelectedElements.Clear();
            _vm.State.SelectedElements.AddRange(_vm.State.ActiveSection.Elements);
            UpdatePropertiesPanel();
            InvalidateCanvas();
            e.Handled = true;
        }
        else if (e.Key == Key.G && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.UngroupSelected();
            InvalidateCanvas();
            e.Handled = true;
        }
        else if (e.Key == Key.G && ctrl)
        {
            _vm.GroupSelected();
            InvalidateCanvas();
            e.Handled = true;
        }
        else if (e.Key == Key.H && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.SplitSelected(horizontal: true);
            e.Handled = true;
        }
        else if (e.Key == Key.J && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.SplitSelected(horizontal: false);
            e.Handled = true;
        }
        else if (e.Key == Key.C && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.CopyStyle();
            e.Handled = true;
        }
        else if (e.Key == Key.V && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.PasteStyle();
            e.Handled = true;
        }
        else if (e.Key == Key.C && ctrl)
        {
            _vm.CopySelected();
            e.Handled = true;
        }
        else if (e.Key == Key.X && ctrl)
        {
            _vm.CutSelected();
            InvalidateCanvas();
            e.Handled = true;
        }
        else if (e.Key == Key.V && ctrl)
        {
            _vm.PasteClipboard();
            e.Handled = true;
        }
        else if (e.Key == Key.D && ctrl)
        {
            _duplicatePending = true;
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            _vm.DeleteSelected();
            e.Handled = true;
        }
        else if ((e.Key == Key.Up || e.Key == Key.Down)
                 && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                 && _vm.State.ActiveSection is not null)
        {
            _vm.MoveSection(_vm.State.ActiveSection, e.Key == Key.Up ? -1 : +1);
            e.Handled = true;
        }
        else if (_duplicatePending && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            _duplicatePending = false;
            var dir = e.Key switch
            {
                Key.Up    => DuplicateDirection.Up,
                Key.Down  => DuplicateDirection.Down,
                Key.Left  => DuplicateDirection.Left,
                Key.Right => DuplicateDirection.Right,
                _ => throw new InvalidOperationException()
            };
            _vm.DuplicateSelected(dir);
            e.Handled = true;
        }
        // Shift = velikost, Ctrl = posun po mřížce, samotné šipky = posun o milimetr. Shift na
        // velikosti je to, co uživatel zná ze starého návrháře; velký krok posunu tím zanikl,
        // protože při výchozí mřížce 1 mm dělal totéž jako šipka samotná (rozhodnutí Jan 1. 9.).
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down
                 && _vm.State.SelectedElements.Count > 0)
        {
            switch (TemplateDesignerViewModel.ResolveArrowAction(ctrl, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
            {
                case ArrowKeyAction.Resize: _vm.ResizeByKey(e.Key, gridStep: false); break;
                case ArrowKeyAction.MoveByGrid: _vm.MoveByKey(e.Key, gridStep: true); break;
                default: _vm.MoveByKey(e.Key, gridStep: false); break;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _vm.State.SelectedElements.Count == 1)
        {
            OpenPropertiesForElement(_vm.State.SelectedElements[0]);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _vm.State.SelectedElements.Count > 1)
        {
            OpenPropertiesForMultipleElements(_vm.State.SelectedElements);
            e.Handled = true;
        }
        else if (e.Key == Key.F2 && _vm.State.SelectedElements.Count == 1
                 && _vm.State.SelectedElements[0] is TextElement te
                 && te.Rotation == 0f && !te.IsLocked)
        {
            EnterInlineEdit(te);
            e.Handled = true;
        }
        // F2 = „uprav obsah na místě" i u pevné tabulky - dvojklik zůstává na nastavení, aby se
        // tabulka nechovala jinak než ostatní prvky (námitka Jana 12. 8.).
        else if (e.Key == Key.F2 && _vm.State.SelectedElements.Count == 1
                 && TryEnterTableCells(_vm.State.SelectedElements[0]))
        {
            e.Handled = true;
        }
        else if (e.Key == Key.F1)
        {
            ShowHelp();
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            if (_vm.RefreshDataCommand.CanExecute(null)) _vm.RefreshDataCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.D0 && ctrl)
        {
            FitToPage();       // Ctrl+0 = Přizpůsobit stránce (== tlačítko „Fit")
            e.Handled = true;
        }
        else if (e.Key == Key.D1 && ctrl)
        {
            ResetZoomTo100();  // Ctrl+1 = Skutečná velikost 100 % (== tlačítko „1:1")
            e.Handled = true;
        }
        else if (e.Key == Key.B && ctrl)
        {
            BtnToggleLeftPanel.IsChecked = !(BtnToggleLeftPanel.IsChecked == true);
            e.Handled = true;
        }
        else if (e.Key == Key.N && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            OpenNormalizePositionsDialog();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // Escape ruší probíhající operaci: drag/resize vrátí pozice na začátek (commit: false),
            // rubber band se zahodí bez výběru. Bez operace = zrušit výběr.
            if (_mouseMode == MouseMode.AddingElement)   CancelAddingElement();
            else if (_mouseMode == MouseMode.Dragging)   EndDrag(commit: false);
            else if (_mouseMode == MouseMode.Resizing)   EndResize(commit: false);
            else if (_mouseMode == MouseMode.SectionResizing) EndSectionResize(commit: false);
            else if (_mouseMode == MouseMode.RubberBand)
            {
                _rubberBandRect = null;
                _mouseMode = MouseMode.Idle;
                skCanvas.ReleaseMouseCapture();
                InvalidateCanvas();
            }
            else
            {
                _vm.State.SelectedElements.Clear();
                UpdatePropertiesPanel();
                InvalidateCanvas();
            }
            e.Handled = true;
        }

        // Fallback: any key (except D itself) while duplicate is pending → clear flag + plain duplicate
        if (_duplicatePending && e.Key != Key.D)
        {
            _duplicatePending = false;
            if (!e.Handled) _vm.DuplicateSelected();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mouse state machine
    // ─────────────────────────────────────────────────────────────────────────

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || _canvas is null) return;

        if (_duplicatePending)
        {
            _duplicatePending = false;
            _vm.DuplicateSelected();
        }

        var pos = e.GetPosition(skCanvas);
        float cx = (float)pos.X, cy = (float)pos.Y, cw = (float)skCanvas.ActualWidth;

        if (e.ClickCount == 2)
        {
            var dblHit = _canvas.HitTestElementForClick(cx, cy, cw);
            if (dblHit is not null)
            {
                var (dblSection, dblElement) = dblHit.Value;
                if (!_vm.State.SelectedElements.Contains(dblElement))
                {
                    _vm.State.SelectedElements.Clear();
                    _vm.State.SelectedElements.Add(dblElement);
                    _vm.ActivateSection(dblSection);
                }
                OpenPropertiesForElement(dblElement);
                e.Handled = true;
                return;
            }
            var dblSection2 = _canvas.HitTestSectionHeader(cx, cy, cw);
            if (dblSection2 is not null)
            {
                OpenPropertiesForSection(dblSection2);
                e.Handled = true;
                return;
            }
            // U4: dvojklik do prázdna = zrušit výběr (ne otevřít vlastnosti stránky - ty jsou
            // na Ctrl+Shift+P / menu Zobrazení). Dialog do prázdna je pro uživatele překvapení.
            _vm.State.SelectedElements.Clear();
            UpdatePropertiesPanel();
            InvalidateCanvas();
            e.Handled = true;
            return;
        }

        // Spodní hrana sekce - tažením se mění její výška a sekce pod ní se posunou.
        // ⚠️ Až PO úchytu prvku: prvek, který se hrany dotýká, musí zůstat roztažitelný.
        if (_canvas.HitTestHandle(cx, cy, cw) is null &&
            _canvas.HitTestSectionEdge(cx, cy, cw) is { } edge &&
            edge.Section.DesignerHeight is { } edgeStartH)
        {
            _sectionResizeTarget  = edge.Section;
            _sectionResizeUpwards = edge.RosteNahoru;
            _sectionResizeStartY = cy;
            _sectionResizeStartH = edgeStartH;
            _vm.State.ActiveSection = edge.Section;

            _mouseMode = MouseMode.SectionResizing;
            skCanvas.CaptureMouse();
            skCanvas.Cursor = System.Windows.Input.Cursors.SizeNS;
            e.Handled = true;
            return;
        }

        var handleHit = _canvas.HitTestHandle(cx, cy, cw);
        if (handleHit.HasValue)
        {
            var (el, handle) = handleHit.Value;
            float? originalH = el.Height;
            if (el.Height is null) el.Height = el.EffectiveDesignerHeight;

            _resizeElement       = el;
            _resizeHandle        = handle;
            _resizeInitX         = el.X;
            _resizeInitY         = el.Y;
            _resizeInitW         = el.Width;
            _resizeInitH         = originalH;
            _resizeInitHComputed = el.Height!.Value;
            var origin = _canvas.GetSectionOrigins(cw)
                                .FirstOrDefault(t => t.Section.Elements.Contains(el));
            _resizeOriginX     = origin.OriginX;
            _resizeOriginY     = origin.OriginY;
            _resizeSectionW    = _vm.State.CurrentTemplate.PageSize.PrintableWidth;
            _resizeSectionH    = origin.Section.EffectiveDesignerHeight;
            _resizeMouseStartX = (cx - _resizeOriginX) / _canvas.MmToPx;
            _resizeMouseStartY = (cy - _resizeOriginY) / _canvas.MmToPx;

            _mouseMode         = MouseMode.Resizing;
            skCanvas.CaptureMouse();
            skCanvas.Cursor = GetResizeCursor(handle);
            e.Handled = true;
            return;
        }

        var hit = _canvas.HitTestElementForClick(cx, cy, cw);

        // Hrana sloupce má přednost před výběrem buňky i před posunem prvku - myš na ní ukazuje
        // kurzor pro změnu šířky, takže uživatel míří na ni.
        if (TryBeginDividerDrag(cx, cy, cw))
        {
            e.Handled = true;
            return;
        }

        // Uvnitř tabulky: klik přepne buňku (Excel), klik jinam nás z tabulky pustí ven.
        ExitTableCellsIfElsewhere(hit?.Element);
        if (InTableCells && TrySelectCellAt(cx, cy, cw))
        {
            e.Handled = true;
            return;
        }

        if (hit is not null)
        {
            var (section, element) = hit.Value;
            bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            bool alt  = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

            // Kam se kliklo - F2 pak otevře tuhle buňku, ne pořád levou horní.
            RememberElementClick(element, cx, cy, cw);

            // Alt+klik = rovnou do té buňky. Alt v aplikaci znamená „o úroveň hloub" (vytáhnout
            // prvek ze skupiny), takže obsah prvku je jeho přirozené pokračování - a je to jeden
            // klik bez F-klávesy, což na noteboocích s Fn řadou rozhoduje.
            if (alt && element is TableElement { Mode: TableMode.Fixed })
            {
                _vm.State.SelectedElements.Clear();
                _vm.State.SelectedElements.Add(element);
                _vm.ActivateSection(section);
                if (TryEnterTableCells(element))
                {
                    UpdatePropertiesPanel();
                    e.Handled = true;
                    return;
                }
            }

    
            _vm.HandleElementHit(section, element, ctrl, alt);

            _dragElement           = element;
            _dragMouseStartCanvasX = cx;
            _dragMouseStartCanvasY = cy;
            _dragHasMoved          = false;
            _dragInitPositions.Clear();
            foreach (var el in _vm.State.SelectedElements.Where(el2 => !el2.IsLocked))
                _dragInitPositions[el] = (el.X, el.Y);

            _mouseMode = MouseMode.Dragging;
            skCanvas.CaptureMouse();
            UpdatePropertiesPanel();
            InvalidateCanvas();
            e.Handled = true;
            return;
        }

        var section2 = _canvas.HitTestSection(cx, cy, cw);
        _vm.HandleEmptyAreaHit(section2, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));

        _rubberBandStart = new SKPoint(cx, cy);
        _rubberBandRect  = null;
        _mouseMode       = MouseMode.RubberBand;
        skCanvas.CaptureMouse();
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_vm is null || _canvas is null) return;
        var pos = e.GetPosition(skCanvas);
        float cx = (float)pos.X, cy = (float)pos.Y, cw = (float)skCanvas.ActualWidth;

        if (InDividerDrag)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndDividerDrag(true); return; }
            UpdateDividerDrag(cx, cy, cw);
            return;
        }

        // Tažení výběru buněk uvnitř tabulky - musí být před přesunem prvku, jinak by tah po
        // buňkách hýbal celou tabulkou.
        if (InCellRangeDrag)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndCellRangeDrag(); return; }
            UpdateCellRangeDrag(cx, cy, cw);
            return;
        }

        if (_mouseMode == MouseMode.Dragging)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndDrag(false); return; }
            if (_dragElement is null || _dragInitPositions.Count == 0) return;

            // Click vs drag - dokud myš nepřekročí threshold, nehýbáme s elementem.
            // Brání tomu, aby pouhý klik (např. pro otevření properties) zaregistroval soft-snap.
            if (!_dragHasMoved)
            {
                float dxPx = cx - _dragMouseStartCanvasX;
                float dyPx = cy - _dragMouseStartCanvasY;
                if (dxPx * dxPx + dyPx * dyPx < DragThresholdPxSquared) return;
                _dragHasMoved = true;
            }

            float deltaMmX = (cx - _dragMouseStartCanvasX) / _canvas.MmToPx;
            float deltaMmY = (cy - _dragMouseStartCanvasY) / _canvas.MmToPx;

            _vm.State.ActiveGapIndicators.Clear();
            _vm.State.SnapTargetElements.Clear();
            _vm.State.ActiveSectionCenterX = null;
            bool snappedX = false, snappedY = false;
            if (_dragInitPositions.TryGetValue(_dragElement!, out var refInit))
            {
                // Ctrl držený = dočasně vypnout snap (free positioning).
                bool ctrlHeld = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                if (_vm.State.SnapToElements && _vm.State.ActiveSection is { } section && !ctrlHeld)
                {
                    var others = section.Elements.Where(el2 => !_dragInitPositions.ContainsKey(el2));
                    float threshold = 8f / _canvas.MmToPx; // ~8px
                    _snapEngine.GapMm = _vm.State.SnapGapMm;

                    // Edge anchors: hrany sekce/printable area - edge-to-edge i gap snap (mezera od okraje).
                    float sectionW = _vm.State.CurrentTemplate.PageSize.PrintableWidth;
                    float sectionH = section.EffectiveDesignerHeight;
                    var edgeX = new List<float> { 0f, sectionW };
                    var edgeY = new List<float> { 0f, sectionH };

                    // Extra X anchors: X hodnoty elementů z ostatních sekcí
                    // (všechny sekce sdílejí levý margin → X je přímo porovnatelné). Bez gap snap.
                    var extraX = new List<float>();
                    foreach (var otherSection in _vm.State.CurrentTemplate.Sections)
                    {
                        if (otherSection == section) continue;
                        foreach (var el in otherSection.Elements)
                        {
                            extraX.Add(el.X);
                            extraX.Add(el.X + el.Width);
                        }
                    }

                    // Extra Y anchors: vertikální střed. Bez gap snap.
                    float refElH = _dragElement!.Height ?? _dragElement!.EffectiveDesignerHeight;
                    var extraY = new List<float> { (sectionH - refElH) / 2f };

                    // Section center X (center-to-anchor) - pro horizontální guide line.
                    float sectionCenterX = sectionW / 2f;

                    var snap = _snapEngine.Calculate(
                        _dragElement!, (refInit.X, refInit.Y),
                        deltaMmX, deltaMmY, others, threshold,
                        extraXAnchors: extraX, extraYAnchors: extraY,
                        edgeXAnchors: edgeX, edgeYAnchors: edgeY,
                        sectionCenterX: sectionCenterX);

                    if (snap.X.HasSnap) { deltaMmX = snap.X.AdjustedDelta; snappedX = true; }
                    if (snap.Y.HasSnap) { deltaMmY = snap.Y.AdjustedDelta; snappedY = true; }
                    if (snap.X.Gap is { } gx) _vm.State.ActiveGapIndicators.Add(gx);
                    if (snap.Y.Gap is { } gy) _vm.State.ActiveGapIndicators.Add(gy);

                    // Section center guide line - pokud snap právě sedí na sectionCenterX
                    _vm.State.ActiveSectionCenterX = snap.X.IsSectionCenter ? (float?)sectionCenterX : null;

                    // Zvýrazni cílové elementy
                    var targets = (snap.X.MatchedElements ?? []).Concat(snap.Y.MatchedElements ?? []).Distinct();
                    _vm.State.SnapTargetElements.AddRange(targets);
                }

                if (_vm.State.SnapToGrid && !ctrlHeld)
                {
                    float g = _vm.State.GridSize;
                    // Bounds elementu v sekci - aby grid snap nepřekročil okraje (jinak by clamp odsunul element mimo grid line)
                    float boundSectionW = _vm.State.CurrentTemplate.PageSize.PrintableWidth;
                    var gridSection = _vm.State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(_dragElement!));
                    float boundSectionH = gridSection?.EffectiveDesignerHeight ?? float.MaxValue;
                    float boundElW = _dragElement!.Width;
                    float boundElH = _dragElement!.Height ?? _dragElement!.EffectiveDesignerHeight;
                    float maxGridX = MathF.Max(0, MathF.Floor((boundSectionW - boundElW) / g) * g);
                    float maxGridY = MathF.Max(0, MathF.Floor((boundSectionH - boundElH) / g) * g);

                    // Grid snap má prioritu - pokud je zapnutý, zaokrouhlí i výsledek smart snapu,
                    // takže pozice je vždy grid-aligned (očekávání uživatele: "mám grid 1mm → pozice jsou v 1mm krocích").
                    // Bez tohoto smart snap k non-grid pozicím sousedů (typicky po PDF importu) obchází grid.
                    {
                        float sx = MathF.Round((refInit.X + deltaMmX) / g) * g;
                        sx = Math.Clamp(sx, 0f, maxGridX);
                        deltaMmX = sx - refInit.X;
                    }
                    {
                        float sy = MathF.Round((refInit.Y + deltaMmY) / g) * g;
                        sy = Math.Clamp(sy, 0f, maxGridY);
                        deltaMmY = sy - refInit.Y;
                    }
                }
            }
            _lastSnapActiveX = snappedX;
            _lastSnapActiveY = snappedY;

            var (clampedX, clampedY) = ClampDragDelta(deltaMmX, deltaMmY);
            foreach (var kvp in _dragInitPositions)
            { kvp.Key.X = kvp.Value.X + clampedX; kvp.Key.Y = kvp.Value.Y + clampedY; }

            // Měřící badge - aktuální pozice draggovaného elementu
            _vm.State.DragBadgeText = $"X: {_dragElement!.X:0.#} mm   Y: {_dragElement!.Y:0.#} mm";
            _vm.State.DragBadgeX = cx;
            _vm.State.DragBadgeY = cy;

            InvalidateCanvas();
            return;
        }

        if (_mouseMode == MouseMode.SectionResizing)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndSectionResize(commit: false); return; }
            if (_sectionResizeTarget is null) return;

            // Patička roste nahoru (je kotvená ke spodku papíru), ostatní sekce dolů.
            float posunMm = (cy - _sectionResizeStartY) / _canvas.MmToPx;
            if (_sectionResizeUpwards) posunMm = -posunMm;
            float novaMm  = _sectionResizeStartH + posunMm;

            // Ctrl vypíná mřížku - stejně jako u prvků.
            if (_vm.State.SnapToGrid && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                novaMm = MathF.Round(novaMm / _vm.State.GridSize) * _vm.State.GridSize;

            // Táž mez jako u „Vyplnit zbytek stránky": sekce nesmí vytlačit ostatní ze stránky.
            // Bez toho by tatáž věc měla dvě chování podle toho, kudy k ní uživatel přišel.
            float strop = Models.Layout.SectionLayoutMath.ZbyvajiciMistoMm(
                _vm.State.CurrentTemplate, _sectionResizeTarget);
            // Dno = obsah sekce. Bez toho by se prvek u dolního okraje v návrháři oříznul,
            // ale při tisku vytiskl celý (engine sekci nafoukne na obsah).
            float dno = Models.Layout.SectionLayoutMath.MinimalniVyskaProSekci(_sectionResizeTarget);
            novaMm = Math.Clamp(novaMm, dno, MathF.Max(dno, strop));

            _sectionResizeTarget.DesignerHeight = novaMm;
            _vm.StatusText = $"Výška sekce {_sectionResizeTarget.Name}: {novaMm:0.#} mm"
                + (novaMm >= strop - 0.05f ? " (na hranici stránky)" : "");

            UpdateCanvasSize();
            InvalidateCanvas();
            return;
        }

        if (_mouseMode == MouseMode.Resizing)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndResize(false); return; }
            if (_resizeElement is null) return;

            float mouseMmX = (cx - _resizeOriginX) / _canvas.MmToPx;
            float mouseMmY = (cy - _resizeOriginY) / _canvas.MmToPx;

            // Ctrl držený = dočasně vypnout snap (free positioning/resize)
            bool resizeCtrlHeld = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            if (_vm.State.SnapToGrid && !resizeCtrlHeld)
            {
                float g = _vm.State.GridSize;
                mouseMmX = MathF.Round(mouseMmX / g) * g;
                mouseMmY = MathF.Round(mouseMmY / g) * g;
            }

            var (newX, newY, newW, newH) = ComputeResize(mouseMmX, mouseMmY);

            bool movL = _resizeHandle is ResizeHandle.TopLeft or ResizeHandle.MiddleLeft or ResizeHandle.BottomLeft;
            bool movR = _resizeHandle is ResizeHandle.TopRight or ResizeHandle.MiddleRight or ResizeHandle.BottomRight;
            bool movT = _resizeHandle is ResizeHandle.TopLeft or ResizeHandle.TopCenter or ResizeHandle.TopRight;
            bool movB = _resizeHandle is ResizeHandle.BottomLeft or ResizeHandle.BottomCenter or ResizeHandle.BottomRight;

            // Smart snap při resize
            _vm.State.ActiveGapIndicators.Clear();
            _vm.State.SnapTargetElements.Clear();
            _vm.State.ActiveSectionCenterX = null;
            if (_vm.State.SnapToElements && _vm.State.ActiveSection is { } resizeSection && !resizeCtrlHeld)
            {
                var others = resizeSection.Elements.Where(e2 => e2 != _resizeElement);
                float threshold = 8f / _canvas.MmToPx;
                _snapEngine.GapMm = _vm.State.SnapGapMm;

                var snap = _snapEngine.CalculateResize(
                    newX, newY, newW, newH,
                    movL, movR, movT, movB,
                    others, _resizeElement, threshold);

                newX = snap.X; newY = snap.Y; newW = snap.W; newH = snap.H;
                foreach (var g in snap.Gaps) _vm.State.ActiveGapIndicators.Add(g);
                _vm.State.SnapTargetElements.AddRange(snap.Targets);
            }

            // Snap na čtverec - u rohového úchytu tvarového elementu dorovná W≈H na přesný
            // čtverec (uživatel nemusí ručně dopočítávat rovné strany). Práh v px → v mm dle zoomu;
            // Ctrl snap vypíná. Protilehlý roh (kotva) zůstává, dorovnaná strana snapuje na grid.
            bool squareSnapped = false;
            if (!resizeCtrlHeld && (movL || movR) && (movT || movB)
                && _resizeElement is RectangleElement or ImageElement or BarcodeElement
                && newH is { } hVal && newW > 0.1f && hVal > 0.1f
                && MathF.Abs(newW - hVal) <= 6f / _canvas.MmToPx)
            {
                float s = (newW + hVal) / 2f;
                if (_vm.State.SnapToGrid)
                {
                    float g = _vm.State.GridSize;
                    s = MathF.Round(s / g) * g;
                }
                s = MathF.Max(s, 0.1f);
                float candX = movL ? newX + newW - s : newX;
                float candY = movT ? newY + hVal - s : newY;
                if (candX >= 0 && candY >= 0
                    && candX + s <= _resizeSectionW && candY + s <= _resizeSectionH)
                {
                    newX = candX; newY = candY; newW = s; newH = s;
                    squareSnapped = true;
                }
            }

            _resizeElement.X = newX; _resizeElement.Y = newY;
            _resizeElement.Width = newW; _resizeElement.Height = newH;

            // Měřící badge - aktuální rozměr a pozice (stejný formát jako u dragu)
            _vm.State.DragBadgeText = squareSnapped
                ? $"{newW:0.#} × {newH:0.#} mm  ◻ čtverec   X: {newX:0.#}   Y: {newY:0.#} mm"
                : $"{newW:0.#} × {newH:0.#} mm   X: {newX:0.#}   Y: {newY:0.#} mm";
            _vm.State.DragBadgeX = cx;
            _vm.State.DragBadgeY = cy;

            InvalidateCanvas();
            return;
        }

        if (_mouseMode == MouseMode.RubberBand)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndRubberBand(); return; }
            _rubberBandRect = new SKRect(
                Math.Min(_rubberBandStart.X, cx), Math.Min(_rubberBandStart.Y, cy),
                Math.Max(_rubberBandStart.X, cx), Math.Max(_rubberBandStart.Y, cy));
            InvalidateCanvas();
            return;
        }

        if (_mouseMode == MouseMode.AddingElement)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { CancelAddingElement(); return; }
            _addingGhostPos = new SKPoint(cx, cy);
            _addingTargetSection = _canvas.HitTestSection(cx, cy, cw);

            // Pro field drag: detekce TextElementu pod kurzorem (pro append)
            _addingTargetElement = null;
            if (_addingFieldName is not null)
            {
                var hit = _canvas.HitTestElement(cx, cy, cw);
                if (hit?.Element is TextElement)
                    _addingTargetElement = hit.Value.Element;
            }

            InvalidateCanvas();
            return;
        }

        var handleHit2 = _canvas.HitTestHandle(cx, cy, cw);
        // Hrana sloupce pevné tabulky má přednost před „posunout prvek" - jinak by o možnosti
        // táhnout šířku uživatel nevěděl, protože kurzor by pořád ukazoval posun.
        if (DividerAt(cx, cy, cw) is { } divider)
            skCanvas.Cursor = divider.Kind == DividerKind.Column ? Cursors.SizeWE : Cursors.SizeNS;
        else if (handleHit2.HasValue)
            skCanvas.Cursor = GetResizeCursor(handleHit2.Value.Handle);
        else if (_canvas.HitTestSectionBottomEdge(cx, cy, cw) is not null)
            // Bez kurzoru by o možnosti táhnout výšku sekce nikdo nevěděl - hrana není nijak
            // vyznačená a klik na ni jinak jen vybere sekci.
            skCanvas.Cursor = Cursors.SizeNS;
        else
        {
            // Táž cesta jako klik, aby kurzor předpovídal, co se stane (i u vybraného elementu pod jiným).
            var elHit = _canvas.HitTestElementForClick(cx, cy, cw);
            skCanvas.Cursor = (elHit.HasValue && !elHit.Value.Element.IsLocked)
                ? Cursors.SizeAll : Cursors.Arrow;
        }
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (InDividerDrag)                          { EndDividerDrag(true); return; }
        if (InCellRangeDrag)                        EndCellRangeDrag();
        else if (_mouseMode == MouseMode.Dragging)  EndDrag(true);
        else if (_mouseMode == MouseMode.Resizing)  EndResize(true);
        else if (_mouseMode == MouseMode.SectionResizing) EndSectionResize(true);
        else if (_mouseMode == MouseMode.RubberBand) EndRubberBand();
        else if (_mouseMode == MouseMode.AddingElement) EndAddingElement();
    }

    private void OnCanvasRightClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || _canvas is null) return;
        var pos  = e.GetPosition(skCanvas);
        float cx = (float)pos.X, cy = (float)pos.Y;
        float cw = (float)skCanvas.ActualWidth;

        var hit = _canvas.HitTestElementForClick(cx, cy, cw);
        var menu = new ContextMenu();

        // Pravý klik na pevnou tabulku nabízí práci s řádky a sloupci - i když uživatel zrovna
        // není „uvnitř". Jinak by se k vkládání řádku dostal jen ten, kdo tuší Alt+klik.
        if (hit?.Element is TableElement { Mode: TableMode.Fixed } rightClickedTable
            && !ReferenceEquals(rightClickedTable, _cellTable))
        {
            _vm.State.SelectedElements.Clear();
            _vm.State.SelectedElements.Add(rightClickedTable);
            _vm.ActivateSection(hit.Value.Section);
            RememberElementClick(rightClickedTable, cx, cy, cw);
            TryEnterTableCells(rightClickedTable, beginEdit: false);
            UpdatePropertiesPanel();
        }

        if (InTableCells && ReferenceEquals(hit?.Element, _cellTable)
            && TryBuildCellContextMenu(menu, cx, cy, cw))
        {
            menu.PlacementTarget = skCanvas;
            menu.IsOpen = true;
            e.Handled = true;
            return;
        }
        ExitTableCellsIfElsewhere(hit?.Element);

        if (hit.HasValue)
        {
            var el = hit.Value.Element;
            if (!_vm.State.SelectedElements.Contains(el))
            {
                _vm.State.SelectedElements.Clear();
                _vm.State.SelectedElements.Add(el);
                InvalidateCanvas();
            }

            menu.Items.Add(new MenuItem { Header = "Vyjmout (Ctrl+X)", Command = _vm.CutCommand });
            menu.Items.Add(new MenuItem { Header = "Kopírovat (Ctrl+C)", Command = _vm.CopyCommand });
            menu.Items.Add(new MenuItem { Header = "Vložit (Ctrl+V)", Command = _vm.PasteCommand });
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Smazat (Del)", Command = _vm.DeleteCommand });
            menu.Items.Add(new Separator());

            var zMenu = new MenuItem { Header = "Vrstva" };
            zMenu.Items.Add(new MenuItem { Header = "Dopředu", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.ChangeZIndex(1, false); InvalidateCanvas(); }) });
            zMenu.Items.Add(new MenuItem { Header = "Dozadu", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.ChangeZIndex(-1, false); InvalidateCanvas(); }) });
            zMenu.Items.Add(new MenuItem { Header = "Úplně dopředu", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.ChangeZIndex(1, true); InvalidateCanvas(); }) });
            zMenu.Items.Add(new MenuItem { Header = "Úplně dozadu", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.ChangeZIndex(-1, true); InvalidateCanvas(); }) });
            menu.Items.Add(zMenu);

            // Seskupení / rozdělení / editace textu - dřív jen na zkratkách, teď viditelné v menu.
            menu.Items.Add(new Separator());
            if (_vm.State.SelectedElements.Count >= 2)
                menu.Items.Add(new MenuItem { Header = "Seskupit (Ctrl+G)", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.GroupSelected(); InvalidateCanvas(); }) });
            if (_vm.State.SelectedElements.Any(x => x.GroupId is not null))
                menu.Items.Add(new MenuItem { Header = "Rozeskupit (Ctrl+Shift+G)", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.UngroupSelected(); InvalidateCanvas(); }) });

            var splitMenu = new MenuItem { Header = "Rozdělit" };
            splitMenu.Items.Add(new MenuItem { Header = "Vodorovně (Ctrl+Shift+H)", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.SplitSelected(horizontal: true); InvalidateCanvas(); }) });
            splitMenu.Items.Add(new MenuItem { Header = "Svisle (Ctrl+Shift+J)", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.SplitSelected(horizontal: false); InvalidateCanvas(); }) });
            menu.Items.Add(splitMenu);

            if (el is TextElement te)
                menu.Items.Add(new MenuItem { Header = "Upravit text (F2)", Command = new EDControls.EDViewModel.RelayCommand(() => EnterInlineEdit(te)) });
            if (el is TableElement { Mode: TableMode.Fixed })
                menu.Items.Add(new MenuItem { Header = "Vyplnit tabulku (Alt+klik, F2)", Command = new EDControls.EDViewModel.RelayCommand(() => TryEnterTableCells(el)) });

            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem
            {
                Header = "Vlastnosti elementu...",
                Command = new EDControls.EDViewModel.RelayCommand(() =>
                {
                    // U2: otevřít dialog (jako dvojklik a „Vlastnosti sekce…") - dřív jen přepínalo
                    // inline panel, což u stejně pojmenované akce se sekcí bylo nekonzistentní.
                    _vm.State.SelectedElements.Clear();
                    _vm.State.SelectedElements.Add(el);
                    UpdatePropertiesPanel();
                    InvalidateCanvas();
                    OpenPropertiesForElement(el);
                })
            });
        }
        else
        {
            // Obecné menu - vložit, vybrat vše
            menu.Items.Add(new MenuItem { Header = "Vložit (Ctrl+V)", Command = _vm.PasteCommand });
            menu.Items.Add(new MenuItem
            {
                Header = "Vybrat vše (Ctrl+A)",
                Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.SelectAll(); InvalidateCanvas(); })
            });
        }

        // Sekční položky - pokud klik je na záhlaví nebo pozadí sekce
        var section = _canvas.HitTestSectionHeader(cx, cy, cw)
                   ?? _canvas.HitTestSection(cx, cy, cw);
        if (section is not null)
        {
            _vm.State.ActiveSection = section;
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());

            menu.Items.Add(new MenuItem
            {
                Header = "Vlastnosti sekce...",
                Command = new EDControls.EDViewModel.RelayCommand(() =>
                {
                    var sectionVm = _vm.Sections.FirstOrDefault(s => s.Section == section);
                    sectionVm?.ShowPropertiesCommand.Execute(null);
                })
            });
            // Akce, ne nastavení - proto v místní nabídce a ne ve formuláři sekce (rozhodnutí
            // Jan 3. 9.). Zbývající místo se nedá spočítat z hlavy, takže tohle je jediná cesta,
            // jak sekci roztáhnout bez počítání výšek ostatních sekcí.
            menu.Items.Add(new MenuItem
            {
                Header = "Vyplnit zbytek stránky",
                Command = new EDControls.EDViewModel.RelayCommand(
                    () => { _vm.VyplnitZbytekStranky(section); InvalidateCanvas(); },
                    () => !Models.Layout.SectionLayoutMath.VyplnujeCeleMisto(_vm.State.CurrentTemplate, section))
            });
            menu.Items.Add(new MenuItem
            {
                Header = "Přesunout nahoru",
                Command = new EDControls.EDViewModel.RelayCommand(
                    () => _vm.MoveSection(section, -1),
                    () => _vm.State.CurrentTemplate.Sections.IndexOf(section) > 0)
            });
            menu.Items.Add(new MenuItem
            {
                Header = "Přesunout dolů",
                Command = new EDControls.EDViewModel.RelayCommand(
                    () => _vm.MoveSection(section, +1),
                    () => _vm.State.CurrentTemplate.Sections.IndexOf(section) < _vm.State.CurrentTemplate.Sections.Count - 1)
            });
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem
            {
                Header = "Smazat sekci",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8B, 0, 0)),
                Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.DeleteSection(section); InvalidateCanvas(); })
            });
            InvalidateCanvas();
        }

        menu.IsOpen = true;
    }

    private void EndAddingElement()
    {
        if (_vm is null || _canvas is null)
        {
            CancelAddingElement();
            return;
        }

        // ── Field drag: append do existujícího TextElementu ──────────────────
        if (_addingFieldName is not null && _addingTargetElement is TextElement textEl)
        {
            _vm.AppendPlaceholderToElement(textEl, _addingFieldName);
            FinishAddingElement();
            return;
        }

        // ── Potřebujeme sekci (pro vytvoření nového elementu) ────────────────
        if (_addingTargetSection is null || (_addingElementType is null && _addingFieldName is null))
        {
            // U3: prostý klik na paletu (bez tažení na sekci) → vlož element do aktivní sekce
            // na výchozí pozici. Bez tohoto by klik jen zrušil přidávání (drag-only = neobjevitelné).
            if (_addingElementType is not null && _vm.State.ActiveSection is not null)
            {
                _vm.AddElementAtPosition(_vm.State.ActiveSection, 5f, 5f, _addingElementType, null);
                FinishAddingElement();
                return;
            }
            CancelAddingElement();
            return;
        }

        float cw = (float)skCanvas.ActualWidth;
        var origin = _canvas.GetSectionOrigins(cw)
            .FirstOrDefault(s => s.Section == _addingTargetSection);
        if (origin.Section is null) { CancelAddingElement(); return; }

        // Přepočet px → mm relativně k sekci
        float mmX = (_addingGhostPos.X - origin.OriginX) / _canvas.MmToPx;
        float mmY = (_addingGhostPos.Y - origin.OriginY) / _canvas.MmToPx;

        _vm.AddElementAtPosition(_addingTargetSection, mmX, mmY, _addingElementType, _addingFieldName);
        FinishAddingElement();
    }

    /// <summary>Společný cleanup po úspěšném dropu (palette i field).</summary>
    private void FinishAddingElement()
    {
        ResetAddingElementState();
        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    private void EndRubberBand()
    {
        if (_rubberBandRect.HasValue && _vm is not null && _canvas is not null)
        {
            float cw = (float)skCanvas.ActualWidth;
            var elements = _canvas.HitTestRect(_rubberBandRect.Value, cw);
            if (_vm.State.ActiveSection is not null)
                elements = elements.Where(el => _vm.State.ActiveSection.Elements.Contains(el)).ToList();
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                _vm.State.SelectedElements.Clear();
            foreach (var el in elements)
                if (!_vm.State.SelectedElements.Contains(el))
                    _vm.State.SelectedElements.Add(el);
        }
        _rubberBandRect = null;
        _mouseMode      = MouseMode.Idle;
        skCanvas.ReleaseMouseCapture();
        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    private void EndDrag(bool commit)
    {
        // Bez pohybu (čistý klik) zachovat init pozice - ne commit, ne soft-snap.
        // Bez tohoto by klik na element s GridSize=5mm „strhl" jeho přesnou 0.1mm pozici na 5mm grid.
        if (commit && !_dragHasMoved) commit = false;
        if (commit && _vm is not null)
        {
            // Při vypnutém grid snapu jen očistíme zlomkové mm z draggu (3.247… → 3.2 mm) na osách,
            // kde se neuplatnil smart-snap. Kvantizace na 0.1 mm je nepostřehnutelná, ale drží čisté
            // souřadnice pro zarovnávání klávesami - shodně s EndResize. NEsnapujeme na celý GridSize,
            // jinak by vypnuté tlačítko mřížky reálně grid snapping nevyplo (jen by ho odložilo na puštění).
            bool applySoftX = !_lastSnapActiveX && !_vm.State.SnapToGrid;
            bool applySoftY = !_lastSnapActiveY && !_vm.State.SnapToGrid;
            if ((applySoftX || applySoftY) && _dragInitPositions.Count > 0)
            {
                foreach (var el in _dragInitPositions.Keys)
                {
                    if (applySoftX) el.X = MmMath.Round(el.X);
                    if (applySoftY) el.Y = MmMath.Round(el.Y);
                }
            }

            var moves = new List<IDesignerCommand>();
            foreach (var kvp in _dragInitPositions)
                if (kvp.Key.X != kvp.Value.X || kvp.Key.Y != kvp.Value.Y)
                    moves.Add(new MoveElementCommand(kvp.Key, kvp.Value.X, kvp.Value.Y, kvp.Key.X, kvp.Key.Y));
            if (moves.Count == 1) _vm.Execute(moves[0]);
            else if (moves.Count > 1) _vm.Execute(new CompositeCommand("Přesun elementů", moves));
        }
        else
        {
            foreach (var kvp in _dragInitPositions)
            { kvp.Key.X = kvp.Value.X; kvp.Key.Y = kvp.Value.Y; }
        }
        _dragInitPositions.Clear();
        _vm?.State.ActiveGapIndicators.Clear();
        _vm?.State.SnapTargetElements.Clear();
        if (_vm is not null) _vm.State.DragBadgeText = null;
        _mouseMode   = MouseMode.Idle;
        _dragElement = null;
        skCanvas.ReleaseMouseCapture();
        skCanvas.Cursor = Cursors.Arrow;
        InvalidateCanvas();
        UpdatePropertiesPanel();
    }

    /// <summary>
    /// Dokončí tažení spodní hrany sekce. Do kroku zpět jde **jedna** změna výšky, ne každý pohyb
    /// myší - proto se během tažení hodnota jen zapisuje a příkaz vzniká teprve tady.
    /// </summary>
    private void EndSectionResize(bool commit)
    {
        var section = _sectionResizeTarget;
        if (section is not null && _vm is not null)
        {
            float konecna = MmMath.Round(section.DesignerHeight ?? _sectionResizeStartH);

            if (commit && MathF.Abs(konecna - _sectionResizeStartH) >= 0.05f)
            {
                // Hodnota už je v modelu z průběhu tažení; příkaz ji jen zapíše znovu, aby
                // krok zpět vrátil původní výšku.
                section.DesignerHeight = _sectionResizeStartH;
                _vm.Execute(new SetPropertyCommand("výška sekce", _sectionResizeStartH, konecna,
                    v => section.DesignerHeight = (float?)v));
            }
            else
            {
                section.DesignerHeight = commit ? konecna : _sectionResizeStartH;
            }
        }

        _sectionResizeTarget = null;
        _mouseMode = MouseMode.Idle;
        if (skCanvas.IsMouseCaptured) skCanvas.ReleaseMouseCapture();
        skCanvas.Cursor = System.Windows.Input.Cursors.Arrow;
        UpdateCanvasSize();
        InvalidateCanvas();
        UpdatePropertiesPanel();
    }

    private void EndResize(bool commit)
    {
        if (_resizeElement is not null && _vm is not null)
        {
            if (commit)
            {
                // Zaokrouhlit na 0.1mm před commitem - eliminuje float artefakty z dragu
                _resizeElement.X      = MmMath.Round(_resizeElement.X);
                _resizeElement.Y      = MmMath.Round(_resizeElement.Y);
                _resizeElement.Width  = MmMath.Round(_resizeElement.Width);
                _resizeElement.Height = MmMath.Round(_resizeElement.Height);

                float? finalH = _resizeElement.Height;
                if (_resizeElement.X != _resizeInitX || _resizeElement.Y != _resizeInitY ||
                    _resizeElement.Width != _resizeInitW || finalH != _resizeInitH)
                {
                    _vm.Execute(new ResizeElementCommand(
                        _resizeElement,
                        _resizeInitX, _resizeInitY, _resizeInitW, _resizeInitH,
                        _resizeElement.X, _resizeElement.Y, _resizeElement.Width, finalH));
                }
            }
            else
            {
                _resizeElement.X = _resizeInitX; _resizeElement.Y = _resizeInitY;
                _resizeElement.Width = _resizeInitW; _resizeElement.Height = _resizeInitH;
            }
        }
        _vm?.State.ActiveGapIndicators.Clear();
        _vm?.State.SnapTargetElements.Clear();
        if (_vm is not null) _vm.State.DragBadgeText = null;
        _mouseMode      = MouseMode.Idle;
        _resizeElement  = null;
        skCanvas.ReleaseMouseCapture();
        skCanvas.Cursor = Cursors.Arrow;
        InvalidateCanvas();
        UpdatePropertiesPanel();
    }

    private (float newX, float newY, float newW, float? newH) ComputeResize(float mouseMmX, float mouseMmY)
    {
        float dx = mouseMmX - _resizeMouseStartX;
        float dy = mouseMmY - _resizeMouseStartY;

        const float minW = 3f, minH = 2f;
        float initH  = _resizeInitHComputed;
        float right  = _resizeInitX + _resizeInitW;
        float bottom = _resizeInitY + initH;

        float newX = _resizeInitX, newY = _resizeInitY, newW = _resizeInitW;
        float? newH = _resizeInitH;
        bool setsH = false;

        switch (_resizeHandle)
        {
            case ResizeHandle.TopLeft:
                newX = Math.Min(_resizeInitX + dx, right - minW);
                newY = Math.Min(_resizeInitY + dy, bottom - minH);
                newW = right - newX; newH = bottom - newY; setsH = true; break;
            case ResizeHandle.TopCenter:
                newY = Math.Min(_resizeInitY + dy, bottom - minH);
                newH = bottom - newY; setsH = true; break;
            case ResizeHandle.TopRight:
                newW = Math.Max(minW, _resizeInitW + dx);
                newY = Math.Min(_resizeInitY + dy, bottom - minH);
                newH = bottom - newY; setsH = true; break;
            case ResizeHandle.MiddleLeft:
                newX = Math.Min(_resizeInitX + dx, right - minW);
                newW = right - newX; break;
            case ResizeHandle.MiddleRight:
                newW = Math.Max(minW, _resizeInitW + dx); break;
            case ResizeHandle.BottomLeft:
                newX = Math.Min(_resizeInitX + dx, right - minW);
                newW = right - newX;
                newH = Math.Max(minH, initH + dy); setsH = true; break;
            case ResizeHandle.BottomCenter:
                newH = Math.Max(minH, initH + dy); setsH = true; break;
            case ResizeHandle.BottomRight:
                newW = Math.Max(minW, _resizeInitW + dx);
                newH = Math.Max(minH, initH + dy); setsH = true; break;
        }

        if (!setsH) newH = _resizeInitH;

        // Clamp na hranice sekce
        newX = Math.Max(0f, newX);
        newY = Math.Max(0f, newY);
        newW = Math.Min(newW, _resizeSectionW - newX);
        if (newH.HasValue)
            newH = Math.Min(newH.Value, _resizeSectionH - newY);
        newW = Math.Max(minW, newW);
        if (newH.HasValue)
            newH = Math.Max(minH, newH.Value);

        return (newX, newY, newW, newH);
    }

    private static Cursor GetResizeCursor(ResizeHandle handle) => handle switch
    {
        ResizeHandle.TopLeft     or ResizeHandle.BottomRight  => Cursors.SizeNWSE,
        ResizeHandle.TopRight    or ResizeHandle.BottomLeft   => Cursors.SizeNESW,
        ResizeHandle.TopCenter   or ResizeHandle.BottomCenter => Cursors.SizeNS,
        ResizeHandle.MiddleLeft  or ResizeHandle.MiddleRight  => Cursors.SizeWE,
        _ => Cursors.Arrow
    };

    private (float deltaX, float deltaY) ClampDragDelta(float rawDeltaX, float rawDeltaY)
    {
        if (_vm is null) return (rawDeltaX, rawDeltaY);
        float sectionW = _vm.State.CurrentTemplate.PageSize.PrintableWidth;

        float minDeltaX = float.MinValue, maxDeltaX = float.MaxValue;
        float minDeltaY = float.MinValue, maxDeltaY = float.MaxValue;

        foreach (var kvp in _dragInitPositions)
        {
            var el      = kvp.Key;
            float initX = kvp.Value.X, initY = kvp.Value.Y;
            float elH   = el.EffectiveDesignerHeight;
            var section = _vm.State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(el));
            float sectionH = section?.EffectiveDesignerHeight ?? float.MaxValue;

            minDeltaX = Math.Max(minDeltaX, -initX);
            maxDeltaX = Math.Min(maxDeltaX, sectionW - el.Width - initX);
            minDeltaY = Math.Max(minDeltaY, -initY);
            maxDeltaY = Math.Min(maxDeltaY, sectionH - elH - initY);
        }

        float clampedX = minDeltaX <= maxDeltaX ? Math.Clamp(rawDeltaX, minDeltaX, maxDeltaX) : 0f;
        float clampedY = minDeltaY <= maxDeltaY ? Math.Clamp(rawDeltaY, minDeltaY, maxDeltaY) : 0f;
        return (clampedX, clampedY);
    }

    // ── Inline text edit (V0 - TextElement only, no rotation, no lock) ─────────

    private TextElement? _inlineEditElement;
    private string? _inlineEditOriginalText;

    private void EnterInlineEdit(TextElement el)
    {
        if (_canvas is null || _vm is null) return;
        var rect = _canvas.GetElementCanvasRect(el, (float)skCanvas.ActualWidth);
        if (rect is null) return;

        _inlineEditElement = el;
        _inlineEditOriginalText = el.Text;

        // Pozice + rozměr v canvas px (Canvas overlay má stejné souřadnice jako SKElement)
        Canvas.SetLeft(InlineEditBox, rect.Value.Left);
        Canvas.SetTop (InlineEditBox, rect.Value.Top);
        InlineEditBox.Width  = rect.Value.Width;
        InlineEditBox.Height = rect.Value.Height;

        // WPF FontSize v DIP; Skia kreslí FontSize_pt * MmToPx_per_inch * zoom px.
        // Pro vizuální shodu při zoom=1, 96dpi: FontSize_DIP = FontSize_pt * 96/72 * Zoom.
        float fontPt = el.Style.FontSize ?? 10f;
        InlineEditBox.FontSize   = fontPt * (96f / 72f) * _vm.State.Zoom;
        InlineEditBox.FontFamily = new FontFamily(el.Style.FontFamily ?? "Arial");
        InlineEditBox.Text       = el.Text ?? "";
        InlineEditBox.Visibility = Visibility.Visible;

        InlineEditBox.Focus();
        InlineEditBox.SelectAll();
    }

    private void ExitInlineEdit(bool commit)
    {
        if (_inlineEditElement is null || _vm is null) return;

        var el = _inlineEditElement;
        var originalText = _inlineEditOriginalText ?? "";
        var newText = InlineEditBox.Text ?? "";

        _inlineEditElement = null;
        _inlineEditOriginalText = null;
        InlineEditBox.Visibility = Visibility.Collapsed;

        if (commit && newText != originalText)
        {
            var cmd = new SetPropertyCommand(
                "Text", originalText, newText,
                v => el.Text = (string)(v ?? ""));
            _vm.Execute(cmd);
        }

        skCanvas.InvalidateVisual();
        skCanvas.Focus(); // vrátit focus na canvas pro klávesové zkratky
    }

    private void InlineEditBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Buňka tabulky má vlastní pravidla (Enter a Tab potvrdí a posunou se dál).
        if (_cellEditing)
        {
            if (HandleCellEditKey(e)) e.Handled = true;
            return;
        }

        // PreviewKeyDown - musí zachytit Enter dřív než TextBox.AcceptsReturn vloží newline.
        if (e.Key == Key.Escape)
        {
            ExitInlineEdit(commit: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            // Enter sám = commit; Shift+Enter = nový řádek (default WPF chování projde)
            ExitInlineEdit(commit: true);
            e.Handled = true;
        }
    }

    private void InlineEditBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_cellEditing)
        {
            // Klik do nabídky polí přebere fokus - to není odchod z buňky, ale výběr položky.
            if (CellFieldSuggestBox.IsMouseOver) return;
            EndCellEdit(commit: true);
            return;
        }
        if (_inlineEditElement is not null)
            ExitInlineEdit(commit: true);
    }
}
