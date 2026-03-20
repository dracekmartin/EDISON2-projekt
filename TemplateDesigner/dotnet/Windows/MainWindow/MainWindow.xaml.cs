using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SkiaSharp;
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
    private ElementPropertiesWindowViewModel? _propsVm;

    private static readonly float[] ZoomLevels = [0.5f, 0.75f, 1.0f, 1.5f, 2.0f];

    // ── Mouse state machine ───────────────────────────────────────────────────
    private enum MouseMode { Idle, Dragging, Resizing, RubberBand, AddingElement }
    private MouseMode _mouseMode = MouseMode.Idle;

    private TemplateElement? _dragElement;
    private float _dragMouseStartCanvasX, _dragMouseStartCanvasY;
    private readonly Dictionary<TemplateElement, (float X, float Y)> _dragInitPositions = new();

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
    private float  _resizeSectionW, _resizeSectionH;

    // ── Adding element (drag from palette / field drag) ────────────────────────
    private string? _addingElementType;
    private string? _addingFieldName;       // e.g. "CISLO_LINKY" — mutually exclusive with _addingElementType
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

    /// <summary>WPF designer constructor — do not use at runtime.</summary>
    public MainWindow() { InitializeComponent(); }

    /// <summary>Runtime constructor. mlRest = null for standalone mode.</summary>
    public MainWindow(MLRest? mlRest)
    {
        _vm         = new TemplateDesignerViewModel(mlRest);
        DataContext = _vm;
        InitializeComponent();

        _canvas        = new DesignerCanvas(_vm.State);
        _previewCanvas = new PreviewCanvas(_vm.State);

        // Wire SkiaSharp (must be code-behind — not in XAML for net9+)
        skCanvas.PaintSurface        += OnPaintSurface;
        skPreview.PaintSurface       += OnPreviewPaintSurface;
        skPreview.SizeChanged        += (_, _) => InvalidatePreview();
        skCanvas.MouseLeftButtonDown += OnCanvasMouseDown;
        skCanvas.MouseMove           += OnCanvasMouseMove;
        skCanvas.MouseLeftButtonUp   += OnCanvasMouseUp;
        CanvasScrollViewer.PreviewMouseWheel += OnCanvasScrollWheel;

        // Wire VM callbacks
        _vm.RequestCanvasRefresh         = () => { skCanvas.InvalidateVisual(); InvalidatePreview(); };
        _vm.RequestPreviewRefresh        = InvalidatePreview;
        _vm.RequestTitleUpdate           = UpdateTitle;
        _vm.RequestCanvasSizeUpdate      = () => { UpdateCanvasSize(); UpdateTitle(); };
        _vm.RequestPropertiesPanelUpdate = UpdatePropertiesPanel;
        _vm.RequestExportPdf             = filePath =>
        {
            try
            {
                _previewCanvas!.RenderToPdf(filePath, _vm.BuildPreviewMockData());
                _vm.StatusText = $"PDF uloženo: {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba při exportu PDF:\n{ex.Message}", "Export PDF",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        // Wire section events
        _vm.SectionsRebuilt += RestoreSectionSelection;
        SectionVM.ShowPropertiesGlobal = s => OpenPropertiesForSection(s);
        SectionVM.SaveTemplateGlobal   = s => SaveSectionAsTemplate(s);

        // Properties VM — sdílená instance, dialog se vytvoří pokaždé nový při otevření
        _propsVm = new ElementPropertiesWindowViewModel(
            _vm.State,
            () => { skCanvas.InvalidateVisual(); InvalidatePreview(); },
            _vm.RebuildSections,
            UpdateCanvasSize,
            _vm.RefreshFieldsList,
            getAvailableFields:    _vm.GetAvailableFields,
            getAvailableGroups:    _vm.GetAvailableGroups,
            tableTemplateProvider: _vm.TableTemplateProvider);

        // Initial state
        _vm.RefreshFieldsList();
        UpdateTitle();
        UpdateCanvasSize();
        UpdatePropertiesPanel();

        if (AppSettings.Current?.AiAssistant.Enabled == false)
            MenuAiAssistant.Visibility = Visibility.Collapsed;

        this.PreviewKeyDown += OnWindowKeyDown;

        // Field drag from schema tree — Window-level events for cross-panel drag
        SchemaTree.PreviewMouseLeftButtonDown += SchemaTree_PreviewMouseDown;
        SchemaTree.PreviewMouseMove           += SchemaTree_PreviewMouseMove;
        this.PreviewMouseMove                 += OnWindowPreviewMouseMove;
        this.PreviewMouseLeftButtonUp         += OnWindowPreviewMouseUp;

        // Section drag reorder in left panel
        SectionsList.PreviewMouseLeftButtonDown += SectionsList_PreviewMouseDown;
        SectionsList.PreviewMouseMove           += SectionsList_PreviewMouseMove;
        SectionsList.PreviewMouseLeftButtonUp   += SectionsList_PreviewMouseUp;
        SectionsList.MouseLeave                 += SectionsList_DragCancel;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Render
    // ─────────────────────────────────────────────────────────────────────────

    private void OnPaintSurface(object? sender, object e)
    {
        if (_canvas is null) return;
        var args    = (dynamic)e;
        SKSurface surface = args.Surface;
        _canvas.Render(surface.Canvas, (float)skCanvas.ActualWidth, (float)skCanvas.ActualHeight);

        if (_mouseMode == MouseMode.RubberBand && _rubberBandRect.HasValue)
        {
            var r = _rubberBandRect.Value;
            using var fill   = new SKPaint { Color = new SKColor(0x220066CC) };
            using var border = new SKPaint { Color = new SKColor(0xFF0066CC), IsStroke = true, StrokeWidth = 1f };
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
                    using var highlight = new SKPaint { Color = new SKColor(0x302196F3) };
                    using var border = new SKPaint
                    {
                        Color = new SKColor(0xFF2196F3), IsStroke = true,
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
                    using var sectionHighlight = new SKPaint { Color = new SKColor(0x182196F3) };
                    surface.Canvas.DrawRect(sRect, sectionHighlight);

                    // Ghost element — čárkovaný obdélník
                    var (defW, defH) = TemplateDesignerViewModel.GetDefaultElementSize(_addingElementType ?? "TextElement");
                    float gx = _addingGhostPos.X - (defW * _canvas.MmToPx / 2f);
                    float gy = _addingGhostPos.Y - (defH * _canvas.MmToPx / 2f);
                    var ghostRect = new SKRect(gx, gy, gx + defW * _canvas.MmToPx, gy + defH * _canvas.MmToPx);
                    using var ghostFill = new SKPaint { Color = new SKColor(0x152196F3) };
                    using var ghostBorder = new SKPaint
                    {
                        Color = new SKColor(0xFF2196F3),
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
        float padding = 80f;
        float pageW   = _vm.State.CurrentTemplate.PageSize.Width * _canvas.MmToPx;
        float pageH   = _canvas.CalculatePageHeightPx(_vm.State.CurrentTemplate);
        skCanvas.Width  = pageW + padding;
        skCanvas.Height = pageH + padding;
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
        if (newIdx != CmbZoom.SelectedIndex)
            CmbZoom.SelectedIndex = newIdx;
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    private const float PreviewFixedZoom = 1.0f;

    private void UpdatePreviewSize()
    {
        if (BtnPreview?.IsChecked != true || _previewCanvas is null || _vm is null) return;
        skPreview.Width  = _previewCanvas.ComputePageWidthPx(PreviewFixedZoom);
        skPreview.Height = _previewCanvas.CalculatePageHeightPx(PreviewFixedZoom, _vm.BuildPreviewMockData()) + 40f;
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
        _previewCanvas.Render(surface.Canvas, w, h, _vm.BuildPreviewMockData());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Section list — selection helpers
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
                // ActivateSection nevoláme — ReorderSection už volá RebuildSections
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
        PreviewColumn.Width = show ? new GridLength(660, GridUnitType.Pixel) : new GridLength(0);
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
    // Menu / button click handlers (thin — delegate to ViewModel)
    // ─────────────────────────────────────────────────────────────────────────

    private void MenuNew_Click(object sender, RoutedEventArgs e)       => _vm!.NewCommand.Execute(null);
    private void MenuOpen_Click(object sender, RoutedEventArgs e)      => _vm!.OpenCommand.Execute(null);
    private void MenuSave_Click(object sender, RoutedEventArgs e)      => _vm!.SaveCommand.Execute(null);
    private void MenuSaveAs_Click(object sender, RoutedEventArgs e)    => _vm!.SaveAsCommand.Execute(null);
    private void MenuExportPdf_Click(object sender, RoutedEventArgs e) => _vm!.ExportPdfCommand.Execute(null);

    private void MenuPrint_Click(object sender, RoutedEventArgs e)
    {
        // Callback pro reálná data — pouze pokud je CacheProvider k dispozici
        Func<Dictionary<string, object?>, List<DataContext>?>? buildRealData = null;
        if (_vm!.CacheProvider is not null && !string.IsNullOrEmpty(_vm.State.CurrentTemplate.AgendaCode))
        {
            var provider = _vm.CacheProvider;
            var agendaCode = _vm.State.CurrentTemplate.AgendaCode!;
            buildRealData = paramValues => provider.GetData(agendaCode, paramValues);
        }

        var vm = new PrintDialogViewModel(
            _vm.State.CurrentTemplate,
            _vm.AgendaParameters,
            paramValues => _vm.BuildPreviewMockData(paramValues),
            buildRealData);

        vm.RequestRenderPdf = (filePath, data) =>
            _previewCanvas!.RenderToPdf(filePath, data);

        vm.RequestRenderMultiPdf = (filePath, dataPages) =>
            _previewCanvas!.RenderMultiDataToPdf(filePath, dataPages);

        var dlg = new PrintDialog(vm) { Owner = this };
        dlg.ShowDialog();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        if (_vm!.ConfirmUnsavedChanges()) Application.Current.Shutdown();
    }

    private void MenuPageProps_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) OpenPropertiesForPage(_vm.State.CurrentTemplate);
    }

    private void MenuUndo_Click(object sender, RoutedEventArgs e)  => _vm!.UndoCommand.Execute(null);
    private void MenuRedo_Click(object sender, RoutedEventArgs e)  => _vm!.RedoCommand.Execute(null);
    private void MenuDelete_Click(object sender, RoutedEventArgs e) => _vm!.DeleteCommand.Execute(null);
    private void MenuHelp_Click(object sender, RoutedEventArgs e)  => ShowHelp();

    private void MenuAiAssistant_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var apiKey = AppSettings.Current?.AiAssistant.AnthropicApiKey;
        var aiVm   = new AiTemplateAssistantDialogViewModel(apiKey, _vm.SchemaProvider);
        var dialog = new AiTemplateAssistantDialog(aiVm) { Owner = this };

        if (dialog.ShowDialog() == true && aiVm.GeneratedTemplate is not null)
        {
            if (!_vm.ConfirmUnsavedChanges()) return;
            _vm.LoadGeneratedTemplate(aiVm.GeneratedTemplate);
        }
    }

    private void MenuImportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var vm = new PdfImportDialogViewModel();
        var dialog = new PdfImportDialog(vm) { Owner = this };

        if (dialog.ShowDialog() == true && vm.ImportedTemplate is not null)
        {
            if (!_vm.ConfirmUnsavedChanges()) return;
            _vm.LoadGeneratedTemplate(vm.ImportedTemplate);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Template tree tab — bidirectional selection sync
    // ─────────────────────────────────────────────────────────────────────────

    private bool _suppressTreeSelection;

    private void RebuildTemplateTree()
    {
        if (_vm is null) return;
        _suppressTreeSelection = true;
        try
        {
            TemplateTree.Items.Clear();
            var template = _vm.State.CurrentTemplate;

            var root = new TreeViewItem
            {
                Header = $"Template: {template.Name}",
                FontWeight = FontWeights.Bold,
                IsExpanded = true
            };

            var ps = template.PageSize;
            root.Items.Add(new TreeViewItem
            {
                Header = $"PageSize: {ps.Width}x{ps.Height}mm  (L={ps.MarginLeft} T={ps.MarginTop} R={ps.MarginRight} B={ps.MarginBottom})",
                Foreground = System.Windows.Media.Brushes.Gray
            });

            foreach (var section in template.Sections)
            {
                var sNode = new TreeViewItem
                {
                    Header = $"[{section.PrintBehavior}] {section.Name}  ({section.DesignerHeight?.ToString("0.#") ?? "auto"} mm)",
                    Tag = section,
                    IsExpanded = true,
                    FontWeight = section == _vm.State.ActiveSection ? FontWeights.SemiBold : FontWeights.Normal
                };

                // Seskupit elementy podle GroupId pro hierarchické zobrazení
                var orderedElements = section.Elements.OrderBy(e => e.ZIndex).ToList();
                var processedGroups = new HashSet<string>();

                foreach (var el in orderedElements)
                {
                    if (el.GroupId is not null && !processedGroups.Contains(el.GroupId))
                    {
                        // Sbalitelný uzel pro skupinu
                        processedGroups.Add(el.GroupId);
                        var groupMembers = orderedElements.Where(e2 => e2.GroupId == el.GroupId).ToList();
                        var groupNode = new TreeViewItem
                        {
                            Header = $"[G] {el.GroupId}  [{groupMembers.Count} elem.]",
                            Tag = el.GroupId,
                            IsExpanded = true,
                            FontStyle = FontStyles.Italic
                        };

                        foreach (var member in groupMembers)
                            groupNode.Items.Add(CreateElementTreeNode(member));
                        sNode.Items.Add(groupNode);
                    }
                    else if (el.GroupId is null)
                    {
                        sNode.Items.Add(CreateElementTreeNode(el));
                    }
                    // else: element patří do skupiny, která už byla zpracována → přeskočit
                }

                root.Items.Add(sNode);
            }

            TemplateTree.Items.Add(root);

            // Obnovit výběr a focus na aktuálně vybraný element
            if (_vm.State.SelectedElements.Count > 0)
            {
                var selectedEl = _vm.State.SelectedElements[0];
                var itemToSelect = FindTreeItemByTag(root, selectedEl);
                if (itemToSelect is not null)
                {
                    itemToSelect.IsSelected = true;
                    itemToSelect.Focus();
                }
            }
        }
        finally { _suppressTreeSelection = false; }
    }

    /// <summary>Najde TreeViewItem s daným Tag v celém stromu.</summary>
    private static TreeViewItem? FindTreeItemByTag(TreeViewItem parent, object tag)
    {
        foreach (TreeViewItem child in parent.Items)
        {
            if (child.Tag == tag) return child;
            var found = FindTreeItemByTag(child, tag);
            if (found is not null) return found;
        }
        return null;
    }

    private static string GetTreeElementDetails(TemplateElement el) => TemplateElement.GetSummary(el);

    private TreeViewItem CreateElementTreeNode(TemplateElement el)
    {
        string typeName = el.GetType().Name.Replace("Element", "");
        string details = GetTreeElementDetails(el);
        bool isSelected = _vm!.State.SelectedElements.Contains(el);
        return new TreeViewItem
        {
            Header = $"[Z{el.ZIndex}] {typeName}: {details}",
            Tag = el,
            FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
            Foreground = isSelected
                ? System.Windows.Media.Brushes.Blue
                : System.Windows.Media.Brushes.Black
        };
    }

    private void TemplateTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_suppressTreeSelection || _vm is null) return;
        if (e.NewValue is not TreeViewItem item) return;

        if (item.Tag is TemplateElement element)
        {
            // Najít sekci, které element patří
            var section = _vm.State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(element));
            if (section is not null)
            {
                _vm.ActivateSection(section);
                _vm.State.SelectedElements.Clear();
                _vm.State.SelectedElements.Add(element);
                RestoreSectionSelection();
                UpdatePropertiesPanel();
                InvalidateCanvas();
            }
        }
        else if (item.Tag is string groupId)
        {
            // Klik na uzel skupiny → vyber všechny členy
            var section = _vm.State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Any(el2 => el2.GroupId == groupId));
            if (section is not null)
            {
                _vm.ActivateSection(section);
                _vm.State.SelectedElements.Clear();
                _vm.State.SelectedElements.AddRange(section.Elements.Where(el2 => el2.GroupId == groupId));
                RestoreSectionSelection();
                UpdatePropertiesPanel();
                InvalidateCanvas();
            }
        }
        else if (item.Tag is Section section)
        {
            _vm.ActivateSection(section);
            _vm.State.SelectedElements.Clear();
            RestoreSectionSelection();
            UpdatePropertiesPanel();
            InvalidateCanvas();
        }
    }

    private void TemplateTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Najít TreeViewItem z vizuálního stromu (event bubbluje z TextBlocku)
        var source = e.OriginalSource as System.Windows.DependencyObject;
        while (source is not null and not TreeViewItem)
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);

        if (source is not TreeViewItem item) return;

        if (item.Tag is TemplateElement element)
            OpenPropertiesForElement(element);
        else if (item.Tag is Section section)
            OpenPropertiesForSection(section);

        e.Handled = true;
    }

    private void TemplateTree_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Enter na elementu = otevřít vlastnosti (jako dvojklik)
        if (e.Key == Key.Enter && TemplateTree.SelectedItem is TreeViewItem item)
        {
            if (item.Tag is TemplateElement element)
                OpenPropertiesForElement(element);
            else if (item.Tag is Section section)
                OpenPropertiesForSection(section);
            e.Handled = true;
        }
    }


    /// <summary>Aktualizuje strom pokud je viditelný (po změně výběru na canvasu).</summary>
    private void RefreshTreeIfVisible()
    {
        if (TabTree?.IsSelected == true)
            RebuildTemplateTree();
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

    // ─────────────────────────────────────────────────────────────────────────
    // Add section / element menus
    // ─────────────────────────────────────────────────────────────────────────

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
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 25f
        });
        Add("Patička stránky", () => new Section
        {
            Name = "Patička stránky", PrintBehavior = SectionPrintBehavior.OnEveryPage,
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
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 30f
        });
        Add("Patička sestavy (jen posl. str.)", () => new Section
        {
            Name = "Patička sestavy", PrintBehavior = SectionPrintBehavior.OnLastPageOnly,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 20f
        });
        Add("Hlavička kromě 1. str.", () => new Section
        {
            Name = "Hlavička kromě 1. str.", PrintBehavior = SectionPrintBehavior.OnEveryPageExceptFirst,
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
                        MessageBox.Show($"Šablonu '{name}' se nepodařilo načíst.",
                            "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show("Nejprve vyberte sekci kliknutím v levém panelu nebo na canvasu.",
                "Žádná aktivní sekce", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var menu = new ContextMenu();
        void Add(string label, Func<TemplateElement> factory)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) =>
            {
                var element = factory();
                _vm.Execute(new AddElementCommand(_vm.State.ActiveSection!, element));
                _vm.State.SelectedElements.Clear();
                _vm.State.SelectedElements.Add(element);
                UpdatePropertiesPanel();
                InvalidateCanvas();
            };
            menu.Items.Add(item);
        }

        Add("Text",        () => TemplateDesignerViewModel.CreateElementByType("TextElement", 5, 5));
        Add("Obdélník",   () => TemplateDesignerViewModel.CreateElementByType("RectangleElement", 5, 5));
        Add("Čára",       () => TemplateDesignerViewModel.CreateElementByType("LineElement", 0, 10));
        Add("Obrázek",    () => TemplateDesignerViewModel.CreateElementByType("ImageElement", 5, 5));
        Add("Tabulka",    () => TemplateDesignerViewModel.CreateElementByType("TableElement", 0, 0));
        Add("QR kód",    () => TemplateDesignerViewModel.CreateElementByType("BarcodeQR", 5, 5));
        Add("Čárový kód", () => TemplateDesignerViewModel.CreateElementByType("BarcodeCode128", 5, 5));
        menu.Items.Add(new Separator());
        Add("Seznam zastávek",    () => TemplateDesignerViewModel.CreateElementByType("StopListElement", 0, 0));
        Add("Mřížka odjezdů",     () => TemplateDesignerViewModel.CreateElementByType("DepartureGridElement", 0, 0));
        Add("Legenda poznámek",   () => TemplateDesignerViewModel.CreateElementByType("NotesLegendElement", 0, 0));

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
            // FindVisualParent místo ContainerFromElement — ten vrací rodičovskou skupinu u vnořených TreeView položek
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

        // Threshold překročen — zahájit drag
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
    /// Window-level PreviewMouseMove — field drag přes celé okno.
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
    /// Window-level PreviewMouseLeftButtonUp — dokončí field drag.
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
            RebuildTemplateTree();
    }

    private void SchemaTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null) return;
        if (SchemaTree.SelectedItem is not SchemaFieldVM field) return;

        if (_vm.State.SelectedElements.Count == 1 && _vm.State.SelectedElements[0] is TextElement textEl)
        {
            string oldText = textEl.Text;
            string newText = oldText + field.Placeholder;
            _vm.Execute(new SetPropertyCommand(
                $"Vložit {field.Placeholder}",
                oldText, newText,
                v => textEl.Text = (string)v!));
            InvalidateCanvas();
            UpdatePropertiesPanel();
        }
        else
        {
            System.Windows.Clipboard.SetText(field.Placeholder);
            _vm.StatusText = $"Zkopírováno do schránky: {field.Placeholder}";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Save section as template (UI dialog — stays in code-behind)
    // ─────────────────────────────────────────────────────────────────────────

    private void SaveSectionAsTemplate(Section section)
        => _vm?.SaveSectionAsTemplate(section, owner: this);

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    // ── Properties dialog helpers ────────────────────────────────────────────

    private void OpenPropertiesForElement(TemplateElement element)
    {
        _propsVm!.PrepareForElement(element, isNewSession: true);
        new ElementPropertiesWindow(_propsVm) { Owner = this }.ShowDialog();
        InvalidateCanvas();
        InvalidatePreview();
    }

    private void OpenPropertiesForSection(Section section)
    {
        _propsVm!.PrepareForSection(section);
        new ElementPropertiesWindow(_propsVm) { Owner = this }.ShowDialog();
        InvalidateCanvas();
        InvalidatePreview();
        _vm!.RebuildSections();
    }

    private void OpenPropertiesForPage(Template template)
    {
        _propsVm!.PrepareForPage(template);
        new ElementPropertiesWindow(_propsVm) { Owner = this }.ShowDialog();
        UpdateCanvasSize();
        InvalidateCanvas();
        InvalidatePreview();
    }

    private void UpdatePropertiesPanel()
    {
        if (_vm is null) return;

        // Aktualizovat strom pokud je viditelný
        RefreshTreeIfVisible();

        if (_vm.State.SelectedElements.Count == 1)
        {
            var el   = _vm.State.SelectedElements[0];
            string type = el.GetType().Name.Replace("Element", "");
            _vm.StatusText = $"{type}  |  X: {el.X:0.#}  Y: {el.Y:0.#}  |  Šířka: {el.Width:0.#} mm" +
                              (el.IsLocked ? "  [ZAMČENO]" : "") +
                              "  |  2× klik nebo Enter = vlastnosti";

            return;
        }

        _vm.StatusText = _vm.State.SelectedElements.Count > 1
            ? $"Vybráno {_vm.State.SelectedElements.Count} elementů  |  Ctrl+klik pro odebrání z výběru"
            : _vm.State.ActiveSection is not null
                ? $"Sekce: {_vm.State.ActiveSection.Name}  |  " +
                  $"Výška: {_vm.State.ActiveSection.DesignerHeight?.ToString("0.#") ?? "dynamická"} mm  |  " +
                  $"Elementy: {_vm.State.ActiveSection.Elements.Count}"
                : "Vyberte element nebo sekci.";
    }

    private void UpdateTitle()
    {
        if (_vm is null) return;
        string name = _vm.State.CurrentTemplate.Name;
        string file = _vm.CurrentFilePath is not null ? $" — {Path.GetFileName(_vm.CurrentFilePath)}" : "";
        Title = $"{name}{file} — Template Designer";
    }

    private void ShowHelp()
    {
        var helpDialog = new HelpDialog { Owner = this };
        helpDialog.ShowDialog();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Keyboard handler
    // ─────────────────────────────────────────────────────────────────────────

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm is null) return;
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

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
        else if (e.Key == Key.P && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            OpenPropertiesForPage(_vm.State.CurrentTemplate);
            e.Handled = true;
        }
        else if (e.Key == Key.P && ctrl)
        {
            MenuPrint_Click(this, new RoutedEventArgs());
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
        else if (e.Key == Key.C && ctrl)
        {
            _vm.CopySelected();
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
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down
                 && _vm.State.SelectedElements.Count > 0)
        {
            _vm.MoveByKey(e.Key, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _vm.State.SelectedElements.Count == 1)
        {
            OpenPropertiesForElement(_vm.State.SelectedElements[0]);
            e.Handled = true;
        }
        else if (e.Key == Key.F1)
        {
            ShowHelp();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_mouseMode == MouseMode.AddingElement)
            {
                CancelAddingElement();
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
            var dblHit = _canvas.HitTestElement(cx, cy, cw);
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
            OpenPropertiesForPage(_vm.State.CurrentTemplate);
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

        var hit = _canvas.HitTestElement(cx, cy, cw);
        if (hit is not null)
        {
            var (section, element) = hit.Value;

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // Ctrl+klik: toggle element (nebo celou skupinu)
                if (element.GroupId is not null && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    var groupMembers = section.Elements.Where(e2 => e2.GroupId == element.GroupId).ToList();
                    if (_vm.State.SelectedElements.Contains(element))
                        foreach (var m in groupMembers) _vm.State.SelectedElements.Remove(m);
                    else
                        foreach (var m in groupMembers.Where(m2 => !_vm.State.SelectedElements.Contains(m2)))
                            _vm.State.SelectedElements.Add(m);
                }
                else
                {
                    if (_vm.State.SelectedElements.Contains(element))
                        _vm.State.SelectedElements.Remove(element);
                    else
                        _vm.State.SelectedElements.Add(element);
                }
            }
            else if (!_vm.State.SelectedElements.Contains(element))
            {
                _vm.State.SelectedElements.Clear();
                // Klik na člen skupiny → vyber celou skupinu (Alt = jen jeden element)
                if (element.GroupId is not null && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    var groupMembers = section.Elements.Where(e2 => e2.GroupId == element.GroupId).ToList();
                    _vm.State.SelectedElements.AddRange(groupMembers);
                }
                else
                {
                    _vm.State.SelectedElements.Add(element);
                }
            }

            _vm.ActivateSection(section);

            _dragElement           = element;
            _dragMouseStartCanvasX = cx;
            _dragMouseStartCanvasY = cy;
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
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            _vm.State.SelectedElements.Clear();
        _vm.ActivateSection(section2);
        UpdatePropertiesPanel();

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

        if (_mouseMode == MouseMode.Dragging)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndDrag(false); return; }
            if (_dragElement is null || _dragInitPositions.Count == 0) return;

            float deltaMmX = (cx - _dragMouseStartCanvasX) / _canvas.MmToPx;
            float deltaMmY = (cy - _dragMouseStartCanvasY) / _canvas.MmToPx;

            _vm.State.ActiveGapIndicators.Clear();
            _vm.State.SnapTargetElements.Clear();
            bool snappedX = false, snappedY = false;
            if (_dragInitPositions.TryGetValue(_dragElement!, out var refInit))
            {
                if (_vm.State.SnapToElements && _vm.State.ActiveSection is { } section)
                {
                    var others = section.Elements.Where(el2 => !_dragInitPositions.ContainsKey(el2));
                    float threshold = 8f / _canvas.MmToPx; // ~8px
                    var snap = _snapEngine.Calculate(
                        _dragElement!, (refInit.X, refInit.Y),
                        deltaMmX, deltaMmY, others, threshold);

                    if (snap.X.HasSnap) { deltaMmX = snap.X.AdjustedDelta; snappedX = true; }
                    if (snap.Y.HasSnap) { deltaMmY = snap.Y.AdjustedDelta; snappedY = true; }
                    if (snap.X.Gap is { } gx) _vm.State.ActiveGapIndicators.Add(gx);
                    if (snap.Y.Gap is { } gy) _vm.State.ActiveGapIndicators.Add(gy);

                    // Zvýrazni cílové elementy
                    var targets = (snap.X.MatchedElements ?? []).Concat(snap.Y.MatchedElements ?? []).Distinct();
                    _vm.State.SnapTargetElements.AddRange(targets);
                }

                if (_vm.State.SnapToGrid)
                {
                    float g = _vm.State.GridSize;
                    if (!snappedX)
                    {
                        float sx = MathF.Round((refInit.X + deltaMmX) / g) * g;
                        deltaMmX = sx - refInit.X;
                    }
                    if (!snappedY)
                    {
                        float sy = MathF.Round((refInit.Y + deltaMmY) / g) * g;
                        deltaMmY = sy - refInit.Y;
                    }
                }
            }

            var (clampedX, clampedY) = ClampDragDelta(deltaMmX, deltaMmY);
            foreach (var kvp in _dragInitPositions)
            { kvp.Key.X = kvp.Value.X + clampedX; kvp.Key.Y = kvp.Value.Y + clampedY; }
            InvalidateCanvas();
            return;
        }

        if (_mouseMode == MouseMode.Resizing)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndResize(false); return; }
            if (_resizeElement is null) return;

            float mouseMmX = (cx - _resizeOriginX) / _canvas.MmToPx;
            float mouseMmY = (cy - _resizeOriginY) / _canvas.MmToPx;

            if (_vm.State.SnapToGrid)
            {
                float g = _vm.State.GridSize;
                mouseMmX = MathF.Round(mouseMmX / g) * g;
                mouseMmY = MathF.Round(mouseMmY / g) * g;
            }

            var (newX, newY, newW, newH) = ComputeResize(mouseMmX, mouseMmY);
            _resizeElement.X = newX; _resizeElement.Y = newY;
            _resizeElement.Width = newW; _resizeElement.Height = newH;
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
        if (handleHit2.HasValue)
            skCanvas.Cursor = GetResizeCursor(handleHit2.Value.Handle);
        else
        {
            var elHit = _canvas.HitTestElement(cx, cy, cw);
            skCanvas.Cursor = (elHit.HasValue && !elHit.Value.Element.IsLocked)
                ? Cursors.SizeAll : Cursors.Arrow;
        }
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_mouseMode == MouseMode.Dragging)       EndDrag(true);
        else if (_mouseMode == MouseMode.Resizing)  EndResize(true);
        else if (_mouseMode == MouseMode.RubberBand) EndRubberBand();
        else if (_mouseMode == MouseMode.AddingElement) EndAddingElement();
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
            string placeholder = $"{{{_addingFieldName}}}";
            string oldText = textEl.Text;
            string newText = oldText + placeholder;
            _vm.Execute(new SetPropertyCommand(
                $"Vložit {placeholder}",
                oldText, newText,
                v => textEl.Text = (string)v!));

            var section = _vm.State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(textEl));
            if (section is not null) _vm.ActivateSection(section);
            _vm.State.SelectedElements.Clear();
            _vm.State.SelectedElements.Add(textEl);
            FinishAddingElement();
            return;
        }

        // ── Potřebujeme sekci (pro vytvoření nového elementu) ────────────────
        if (_addingTargetSection is null || (_addingElementType is null && _addingFieldName is null))
        {
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

        // Centrovat element na pozici kurzoru
        var (defW, defH) = TemplateDesignerViewModel.GetDefaultElementSize(_addingElementType ?? "TextElement");
        mmX -= defW / 2f;
        mmY -= defH / 2f;

        // Clamp do hranic sekce
        float sectionW = _vm.State.CurrentTemplate.PageSize.PrintableWidth;
        float sectionH = _addingTargetSection.EffectiveDesignerHeight;
        mmX = Math.Clamp(mmX, 0, Math.Max(0, sectionW - defW));
        mmY = Math.Clamp(mmY, 0, Math.Max(0, sectionH - defH));

        // Snap na mřížku
        if (_vm.State.SnapToGrid)
        {
            float g = _vm.State.GridSize;
            mmX = MathF.Round(mmX / g) * g;
            mmY = MathF.Round(mmY / g) * g;
        }

        // Vytvoření elementu — palette nebo field
        TemplateElement element;
        if (_addingFieldName is not null)
        {
            string placeholder = $"{{{_addingFieldName}}}";
            element = new TextElement { X = mmX, Y = mmY, Width = defW, Height = defH, Text = placeholder };
        }
        else
        {
            element = TemplateDesignerViewModel.CreateElementByType(_addingElementType!, mmX, mmY);
        }

        _vm.Execute(new AddElementCommand(_addingTargetSection, element));
        _vm.ActivateSection(_addingTargetSection);
        _vm.State.SelectedElements.Clear();
        _vm.State.SelectedElements.Add(element);
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
        if (commit && _vm is not null)
        {
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
        _mouseMode   = MouseMode.Idle;
        _dragElement = null;
        skCanvas.ReleaseMouseCapture();
        skCanvas.Cursor = Cursors.Arrow;
        InvalidateCanvas();
        UpdatePropertiesPanel();
    }

    private void EndResize(bool commit)
    {
        if (_resizeElement is not null && _vm is not null)
        {
            if (commit)
            {
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
        _mouseMode     = MouseMode.Idle;
        _resizeElement = null;
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

        // Clamp na hranice sekce — resize nesmí vyjet mimo
        newX = Math.Max(0, newX);
        newY = Math.Max(0, newY);
        if (newX + newW > _resizeSectionW)
            newW = _resizeSectionW - newX;
        if (newH is float h && newY + h > _resizeSectionH)
            newH = _resizeSectionH - newY;

        newW = Math.Max(minW, newW);
        if (newH is float h2) newH = Math.Max(minH, h2);

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
}
