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
    private ElementPropertiesWindow? _propsWindow;

    private static readonly float[] ZoomLevels = [0.5f, 0.75f, 1.0f, 1.5f, 2.0f];

    // ── Mouse state machine ───────────────────────────────────────────────────
    private enum MouseMode { Idle, Dragging, Resizing, RubberBand }
    private MouseMode _mouseMode = MouseMode.Idle;

    private TemplateElement? _dragElement;
    private float _dragMouseStartCanvasX, _dragMouseStartCanvasY;
    private readonly Dictionary<TemplateElement, (float X, float Y)> _dragInitPositions = new();

    private SKPoint _rubberBandStart;
    private SKRect? _rubberBandRect;

    private TemplateElement? _resizeElement;
    private ResizeHandle     _resizeHandle;
    private float  _resizeInitX, _resizeInitY, _resizeInitW;
    private float? _resizeInitH;
    private float  _resizeInitHComputed;
    private float  _resizeMouseStartX, _resizeMouseStartY;
    private float  _resizeOriginX, _resizeOriginY;

    // ── Section list selection helper ─────────────────────────────────────────
    private bool _suppressSectionSelection;

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
        SectionVM.ShowPropertiesGlobal = s => _propsWindow?.ShowForSection(s);
        SectionVM.SaveTemplateGlobal   = s => SaveSectionAsTemplate(s);

        // Properties window
        _propsWindow = new ElementPropertiesWindow(
            _vm.State,
            () => { skCanvas.InvalidateVisual(); InvalidatePreview(); },
            _vm.RebuildSections,
            UpdateCanvasSize,
            _vm.RefreshFieldsList,
            getAvailableFields:    _vm.GetAvailableFields,
            getAvailableGroups:    _vm.GetAvailableGroups,
            tableTemplateProvider: _vm.TableTemplateProvider);

        this.Loaded += (_, _) => _propsWindow.Owner = this;
        this.Closed += (_, _) => _propsWindow.CloseForReal();

        // Initial state
        _vm.RefreshFieldsList();
        UpdateTitle();
        UpdateCanvasSize();
        UpdatePropertiesPanel();

        if (AppSettings.Current?.AiAssistant.Enabled == false)
            MenuAiAssistant.Visibility = Visibility.Collapsed;

        this.PreviewKeyDown += OnWindowKeyDown;
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
        if (BtnPreview?.IsChecked != true || _previewCanvas is null) return;
        skPreview.Width  = _previewCanvas.ComputePageWidthPx(PreviewFixedZoom);
        skPreview.Height = _previewCanvas.CalculatePageHeightPx(PreviewFixedZoom) + 40f;
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
            _propsWindow?.ShowForSection(svm.Section);
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
        bool show = BtnPreview.IsChecked == true;
        PreviewColumn.Width = show ? new GridLength(660, GridUnitType.Pixel) : new GridLength(0);
        MenuPreview.IsChecked = show;
        if (show) InvalidatePreview();
    }

    private void MenuPreview_Changed(object sender, RoutedEventArgs e)
    {
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

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        if (_vm!.ConfirmUnsavedChanges()) Application.Current.Shutdown();
    }

    private void MenuPageProps_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _propsWindow!.ShowForPage(_vm.State.CurrentTemplate);
    }

    private void MenuUndo_Click(object sender, RoutedEventArgs e)  => _vm!.UndoCommand.Execute(null);
    private void MenuRedo_Click(object sender, RoutedEventArgs e)  => _vm!.RedoCommand.Execute(null);
    private void MenuDelete_Click(object sender, RoutedEventArgs e) => _vm!.DeleteCommand.Execute(null);
    private void MenuHelp_Click(object sender, RoutedEventArgs e)  => ShowHelp();

    private void MenuAiAssistant_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var apiKey = AppSettings.Current?.AiAssistant.AnthropicApiKey;
        var dialog = new AiTemplateAssistantDialog(apiKey, _vm.SchemaProvider) { Owner = this };

        if (dialog.ShowDialog() == true && dialog.GeneratedTemplate is not null)
        {
            if (!_vm.ConfirmUnsavedChanges()) return;
            _vm.LoadGeneratedTemplate(dialog.GeneratedTemplate);
        }
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

        Add("Text",       () => new TextElement      { X = 5, Y = 5, Width = 80, Height = 8, Text = "Text" });
        Add("Obdélník",   () => new RectangleElement { X = 5, Y = 5, Width = 60, Height = 20, StrokeWidth = 0.5f });
        Add("Čára",       () => new LineElement      { X = 0, Y = 10, Width = 190, Height = 1 });
        Add("Obrázek",    () => new ImageElement     { X = 5, Y = 5, Width = 40, Height = 30 });
        Add("Tabulka",    () => new TableElement     { X = 0, Y = 0, Width = 190 });
        Add("QR kód",    () => new BarcodeElement  { X = 5, Y = 5, Width = 30, Height = 30, BarcodeType = BarcodeElementType.QR });
        Add("Čárový kód", () => new BarcodeElement { X = 5, Y = 5, Width = 80, Height = 15, BarcodeType = BarcodeElementType.Code128 });

        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Schema tree double-click
    // ─────────────────────────────────────────────────────────────────────────

    private void LeftTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TabFields?.IsSelected == true)
            _vm?.RefreshFieldsList();
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

    private void UpdatePropertiesPanel()
    {
        if (_vm is null) return;

        if (_vm.State.SelectedElements.Count == 1)
        {
            var el   = _vm.State.SelectedElements[0];
            string type = el.GetType().Name.Replace("Element", "");
            _vm.StatusText = $"{type}  |  X: {el.X:0.#}  Y: {el.Y:0.#}  |  Šířka: {el.Width:0.#} mm" +
                              (el.IsLocked ? "  [ZAMČENO]" : "") +
                              "  |  2× klik nebo Enter = vlastnosti";

            if (_propsWindow!.IsVisible)
                _propsWindow.ShowForElement(el);
            return;
        }

        _propsWindow?.Hide();

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
            _propsWindow!.ShowForPage(_vm.State.CurrentTemplate);
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
            _vm.DuplicateSelected();
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
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down
                 && _vm.State.SelectedElements.Count > 0)
        {
            _vm.MoveByKey(e.Key, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _vm.State.SelectedElements.Count == 1)
        {
            _propsWindow!.ShowForElement(_vm.State.SelectedElements[0]);
            e.Handled = true;
        }
        else if (e.Key == Key.F1)
        {
            ShowHelp();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_propsWindow?.IsVisible == true)
                _propsWindow.Hide();
            else
            {
                _vm.State.SelectedElements.Clear();
                UpdatePropertiesPanel();
                InvalidateCanvas();
            }
            e.Handled = true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mouse state machine
    // ─────────────────────────────────────────────────────────────────────────

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || _canvas is null) return;
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
                _propsWindow!.ShowForElement(dblElement);
                e.Handled = true;
                return;
            }
            var dblSection2 = _canvas.HitTestSectionHeader(cx, cy, cw);
            if (dblSection2 is not null)
            {
                _propsWindow!.ShowForSection(dblSection2);
                e.Handled = true;
                return;
            }
            _propsWindow!.ShowForPage(_vm.State.CurrentTemplate);
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
                if (_vm.State.SelectedElements.Contains(element))
                    _vm.State.SelectedElements.Remove(element);
                else
                    _vm.State.SelectedElements.Add(element);
            }
            else if (!_vm.State.SelectedElements.Contains(element))
            {
                _vm.State.SelectedElements.Clear();
                _vm.State.SelectedElements.Add(element);
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

            if (_vm.State.SnapToGrid && _dragInitPositions.TryGetValue(_dragElement, out var refInit))
            {
                float g = _vm.State.GridSize;
                float snappedX = MathF.Round((refInit.X + deltaMmX) / g) * g;
                float snappedY = MathF.Round((refInit.Y + deltaMmY) / g) * g;
                deltaMmX = snappedX - refInit.X;
                deltaMmY = snappedY - refInit.Y;
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
