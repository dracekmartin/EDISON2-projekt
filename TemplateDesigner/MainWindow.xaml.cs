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
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Models.Sections;
using TemplateDesigner.Config;
using TemplateDesigner.Serialization;

namespace TemplateDesigner;

public partial class MainWindow : Window
{
    private readonly DesignerState          _state;
    private readonly DesignerCanvas         _canvas;
    private          ElementPropertiesWindow? _propsWindow;
    private string? _currentFilePath;

    private readonly IDataSchemaProvider _schemaProvider;

    private static readonly float[] ZoomLevels = [0.5f, 0.75f, 1.0f, 1.5f, 2.0f];

    // ── Mouse state machine ──
    private enum MouseMode { Idle, Dragging, Resizing, RubberBand }
    private MouseMode      _mouseMode    = MouseMode.Idle;

    // Drag state
    private TemplateElement? _dragElement; // element, na který bylo kliknuto (referenční pro snap)
    private float _dragMouseStartCanvasX, _dragMouseStartCanvasY; // canvas px při zahájení dragu
    private readonly Dictionary<TemplateElement, (float X, float Y)> _dragInitPositions = new();

    // Rubber band state
    private SKPoint _rubberBandStart;
    private SKRect? _rubberBandRect;

    // Clipboard
    private List<TemplateElement> _clipboard = [];

    // Resize state
    private TemplateElement? _resizeElement;
    private ResizeHandle     _resizeHandle;
    private float  _resizeInitX, _resizeInitY, _resizeInitW;
    private float? _resizeInitH;
    private float  _resizeMouseStartX, _resizeMouseStartY; // mm relativně k sekci
    private float  _resizeOriginX, _resizeOriginY;         // canvas px origin sekce

    public MainWindow()
    {
        InitializeComponent();

        _state  = new DesignerState();
        _canvas = new DesignerCanvas(_state);

        // Schema provider — Caché (primární) + JSON soubory (záloha)
        string schemasDir = Path.Combine(AppContext.BaseDirectory, "schemas");
        var settings      = AppSettings.Load();
        var cacheProvider = CacheSchemaWiring.TryCreate(settings);

        _schemaProvider = cacheProvider is not null
            ? new CompositeDataSchemaProvider(
                primary:  cacheProvider,
                fallback: new JsonFileDataSchemaProvider(schemasDir))
            : new JsonFileDataSchemaProvider(schemasDir);

        // PaintSurface se registruje výhradně v code-behind (SkiaSharp.Views.WPF + net9)
        skCanvas.PaintSurface        += OnPaintSurface;
        skCanvas.MouseLeftButtonDown += OnCanvasMouseDown;
        skCanvas.MouseMove           += OnCanvasMouseMove;
        skCanvas.MouseLeftButtonUp   += OnCanvasMouseUp;

        CanvasScrollViewer.PreviewMouseWheel += OnCanvasScrollWheel;

        _state.CurrentTemplate = CreateSampleTemplate();

        UpdateTitle();
        UpdateCanvasSize();
        RefreshSectionsList();
        RefreshFieldsList();

        _propsWindow = new ElementPropertiesWindow(
            _state, InvalidateCanvas, RefreshSectionsList, UpdateCanvasSize, RefreshFieldsList,
            getAvailableFields: () => _schemaProvider.GetSchema(_state.CurrentTemplate.AgendaCode)?.Fields);
        this.Loaded += (_, _) => _propsWindow.Owner = this;
        this.Closed += (_, _) => _propsWindow.CloseForReal();

        UpdatePropertiesPanel();

        this.PreviewKeyDown += OnWindowKeyDown;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Render

    private void OnPaintSurface(object? sender, object e)
    {
        var args    = (dynamic)e;
        SKSurface surface = args.Surface;
        _canvas.Render(surface.Canvas, (float)skCanvas.ActualWidth, (float)skCanvas.ActualHeight);

        // Rubber band selection overlay
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
        float padding = 80f;
        float pageW   = _state.CurrentTemplate.PageSize.Width * _canvas.MmToPx;
        float pageH   = _canvas.CalculatePageHeightPx(_state.CurrentTemplate);
        skCanvas.Width  = pageW + padding;
        skCanvas.Height = pageH + padding;
        skCanvas.InvalidateVisual();
    }

    private void InvalidateCanvas() => skCanvas.InvalidateVisual();

    private void OnCanvasScrollWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        e.Handled = true; // zabraňuje scrollování ScrollVieweru

        int newIdx = Math.Clamp(CmbZoom.SelectedIndex + (e.Delta > 0 ? 1 : -1), 0, ZoomLevels.Length - 1);
        if (newIdx != CmbZoom.SelectedIndex)
            CmbZoom.SelectedIndex = newIdx; // spustí CmbZoom_SelectionChanged
    }

    // ─────────────────────────────────────────────────────────────────────
    // Sekce — seznam vlevo

    private void RefreshSectionsList()
    {
        SectionsList.Children.Clear();
        var sections = _state.CurrentTemplate.Sections;

        for (int i = 0; i < sections.Count; i++)
        {
            var section   = sections[i];
            bool isDynamic = section.DesignerHeight is null;
            bool isActive  = section == _state.ActiveSection;
            int  idx       = i; // closure capture

            var label = new TextBlock
            {
                Text       = section.Name,
                FontSize   = 12,
                ToolTip    = section.GetType().Name,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            // ▲▼ tlačítka vpravo
            var btnUp = new Button
            {
                Content   = "▲", Width = 18, Height = 16, FontSize = 8,
                Padding   = new Thickness(0), Margin = new Thickness(2, 0, 0, 0),
                ToolTip   = "Přesunout nahoru (Ctrl+Shift+↑)",
                IsEnabled = idx > 0,
                VerticalAlignment = VerticalAlignment.Center
            };
            var btnDown = new Button
            {
                Content   = "▼", Width = 18, Height = 16, FontSize = 8,
                Padding   = new Thickness(0), Margin = new Thickness(2, 0, 0, 0),
                ToolTip   = "Přesunout dolů (Ctrl+Shift+↓)",
                IsEnabled = idx < sections.Count - 1,
                VerticalAlignment = VerticalAlignment.Center
            };

            btnUp.Click   += (_, e2) => { e2.Handled = true; MoveSection(section, -1); };
            btnDown.Click += (_, e2) => { e2.Handled = true; MoveSection(section, +1); };

            var rowPanel = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(btnDown, Dock.Right);
            DockPanel.SetDock(btnUp,   Dock.Right);
            rowPanel.Children.Add(btnDown);
            rowPanel.Children.Add(btnUp);
            rowPanel.Children.Add(label);

            // Context menu
            var ctx        = new ContextMenu();
            var miProps    = new MenuItem { Header = "Vlastnosti sekce…" };
            var miMoveUp   = new MenuItem { Header = "Přesunout nahoru", IsEnabled = idx > 0 };
            var miMoveDown = new MenuItem { Header = "Přesunout dolů",   IsEnabled = idx < sections.Count - 1 };
            var miSep      = new Separator();
            var miDelete   = new MenuItem { Header = "Smazat sekci",     Foreground = System.Windows.Media.Brushes.DarkRed };

            miProps.Click    += (_, _) => _propsWindow!.ShowForSection(section);
            miMoveUp.Click   += (_, _) => MoveSection(section, -1);
            miMoveDown.Click += (_, _) => MoveSection(section, +1);
            miDelete.Click   += (_, _) => DeleteSection(section);

            ctx.Items.Add(miProps);
            ctx.Items.Add(miMoveUp);
            ctx.Items.Add(miMoveDown);
            ctx.Items.Add(miSep);
            ctx.Items.Add(miDelete);

            var item = new Border
            {
                Margin          = new Thickness(0, 2, 0, 0),
                Padding         = new Thickness(8, 5, 4, 5),
                Background      = new SolidColorBrush(isActive
                    ? Color.FromRgb(210, 230, 255)
                    : isDynamic ? Color.FromRgb(234, 243, 255) : Color.FromRgb(245, 245, 245)),
                BorderBrush     = new SolidColorBrush(isActive
                    ? Color.FromRgb(33, 150, 243)
                    : isDynamic ? Color.FromRgb(180, 210, 245) : Color.FromRgb(210, 210, 210)),
                BorderThickness = new Thickness(isActive ? 2 : 1),
                CornerRadius    = new CornerRadius(3),
                Cursor          = Cursors.Hand,
                Tag             = section,
                Child           = rowPanel,
                ContextMenu     = ctx
            };

            item.MouseLeftButtonDown += (_, e2) =>
            {
                if (e2.ClickCount == 2)
                    _propsWindow!.ShowForSection(section);
                else
                    ActivateSection(section);
            };

            SectionsList.Children.Add(item);
        }
    }

    private void ActivateSection(Section? section)
    {
        _state.ActiveSection = section;
        RefreshSectionsList();
        InvalidateCanvas();
    }

    private void MoveSection(Section section, int direction)
    {
        int idx    = _state.CurrentTemplate.Sections.IndexOf(section);
        int newIdx = idx + direction;
        if (newIdx < 0 || newIdx >= _state.CurrentTemplate.Sections.Count) return;

        _state.Execute(new MoveSectionCommand(_state.CurrentTemplate, section, direction));
        RefreshSectionsList();
        UpdateCanvasSize();
        InvalidateCanvas();
    }

    private void DeleteSection(Section section)
    {
        if (_state.CurrentTemplate.Sections.Count <= 1)
        {
            MessageBox.Show("Šablona musí mít alespoň jednu sekci.", "Smazat sekci",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Odznač elementy z mazané sekce
        _state.SelectedElements.RemoveAll(el => section.Elements.Contains(el));

        // Přepni aktivní sekci, pokud se maže ta aktivní
        if (_state.ActiveSection == section)
            _state.ActiveSection = _state.CurrentTemplate.Sections
                .FirstOrDefault(s => s != section);

        _propsWindow?.Hide();
        _state.Execute(new DeleteSectionCommand(_state.CurrentTemplate, section));
        RefreshSectionsList();
        UpdateCanvasSize();
        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Toolbar — zoom, grid, snap

    private void CmbZoom_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_state is null) return;
        int idx = CmbZoom.SelectedIndex;
        if (idx >= 0 && idx < ZoomLevels.Length)
        {
            _state.Zoom = ZoomLevels[idx];
            UpdateCanvasSize();
        }
    }

    private void BtnGrid_Changed(object sender, RoutedEventArgs e)
    {
        if (_state is null) return;
        _state.ShowGrid = BtnGrid.IsChecked == true;
        MenuGrid.IsChecked = _state.ShowGrid;
        InvalidateCanvas();
    }

    private void BtnSnap_Changed(object sender, RoutedEventArgs e)
    {
        if (_state is null) return;
        _state.SnapToGrid = BtnSnap.IsChecked == true;
        MenuSnap.IsChecked = _state.SnapToGrid;
    }

    private void MenuGrid_Changed(object sender, RoutedEventArgs e)
    {
        if (_state is null) return;
        _state.ShowGrid    = MenuGrid.IsChecked;
        BtnGrid.IsChecked  = _state.ShowGrid;
        InvalidateCanvas();
    }

    private void MenuSnap_Changed(object sender, RoutedEventArgs e)
    {
        if (_state is null) return;
        _state.SnapToGrid = MenuSnap.IsChecked;
        BtnSnap.IsChecked = _state.SnapToGrid;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Panel viditelnost

    private void MenuPanelSection_Changed(object sender, RoutedEventArgs e)
    {
        if (SectionsColumn is null) return;
        SectionsColumn.Width = MenuSectionsPanel.IsChecked
            ? new GridLength(200, GridUnitType.Pixel)
            : new GridLength(0);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Soubor

    private void MenuNew_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmUnsavedChanges()) return;
        _state.CurrentTemplate = new Template();
        _currentFilePath = null;
        UpdateTitle();
        UpdateCanvasSize();
        RefreshSectionsList();
        RefreshFieldsList();
    }

    private async void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmUnsavedChanges()) return;
        var dlg = new OpenFileDialog { Filter = TemplateSerializer.FileFilter, Title = "Otevřít šablonu" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _state.CurrentTemplate = await TemplateSerializer.LoadAsync(dlg.FileName);
            _currentFilePath = dlg.FileName;
            UpdateTitle();
            UpdateCanvasSize();
            RefreshSectionsList();
            RefreshFieldsList();
        }
        catch (TemplateDeserializationException ex)
        {
            MessageBox.Show(ex.Message, "Chyba při načítání", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void MenuSave_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFilePath is null) { MenuSaveAs_Click(sender, e); return; }
        await TemplateSerializer.SaveAsync(_state.CurrentTemplate, _currentFilePath);
        UpdateTitle();
    }

    private async void MenuSaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter   = TemplateSerializer.FileFilter,
            Title    = "Uložit šablonu",
            FileName = _state.CurrentTemplate.Name
        };
        if (dlg.ShowDialog() != true) return;
        _currentFilePath = dlg.FileName;
        await TemplateSerializer.SaveAsync(_state.CurrentTemplate, _currentFilePath);
        UpdateTitle();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        if (ConfirmUnsavedChanges()) Application.Current.Shutdown();
    }

    private void MenuPageProps_Click(object sender, RoutedEventArgs e)
    {
        _propsWindow!.ShowForPage(_state.CurrentTemplate);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Undo / Redo / Delete (stub)

    private void MenuUndo_Click(object sender, RoutedEventArgs e)
    {
        _state.Undo();
        InvalidateCanvas();
        RefreshSectionsList();
        UpdatePropertiesPanel();
    }

    private void MenuRedo_Click(object sender, RoutedEventArgs e)
    {
        _state.Redo();
        InvalidateCanvas();
        RefreshSectionsList();
        UpdatePropertiesPanel();
    }

    private void MenuDelete_Click(object sender, RoutedEventArgs e) => DeleteSelected();

    // ─────────────────────────────────────────────────────────────────────
    // ZIndex — pořadí vykreslování

    private void BtnZForward_Click(object sender, RoutedEventArgs e)  => ChangeZIndex(+1, toExtreme: false);
    private void BtnZBackward_Click(object sender, RoutedEventArgs e) => ChangeZIndex(-1, toExtreme: false);
    private void BtnZFront_Click(object sender, RoutedEventArgs e)    => ChangeZIndex(+1, toExtreme: true);
    private void BtnZBack_Click(object sender, RoutedEventArgs e)     => ChangeZIndex(-1, toExtreme: true);

    /// <summary>
    /// Změní ZIndex vybraných elementů.
    /// <paramref name="sign"/> +1 = dopředu, -1 = dozadu.
    /// <paramref name="toExtreme"/> true = zcela dopředu/dozadu, false = o jeden krok.
    /// </summary>
    private void ChangeZIndex(int sign, bool toExtreme)
    {
        if (_state.SelectedElements.Count == 0 || _state.ActiveSection is null) return;

        var section  = _state.ActiveSection;
        var selected = _state.SelectedElements
            .Where(el => section.Elements.Contains(el))
            .ToList();
        if (selected.Count == 0) return;

        var commands = new List<IDesignerCommand>();

        if (toExtreme && sign > 0)
        {
            // Přenést zcela dopředu: každý vybraný dostane ZIndex = max+1, max+2 …
            int baseZ = section.Elements
                .Where(el => !selected.Contains(el))
                .Select(el => el.ZIndex)
                .DefaultIfEmpty(-1)
                .Max() + 1;
            foreach (var el in selected.OrderBy(e => e.ZIndex))
            {
                int oldZ = el.ZIndex;
                int newZ = baseZ++;
                if (oldZ == newZ) continue;
                commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v));
            }
        }
        else if (toExtreme && sign < 0)
        {
            // Odeslat zcela dozadu: vybrané dostanou ZIndex 0, 1 … ostatní se posunou nahoru
            int shift = selected.Count;
            // Ostatní elementy posunou ZIndex o shift nahoru
            foreach (var el in section.Elements.Where(e => !selected.Contains(e)))
            {
                int oldZ = el.ZIndex;
                int newZ = oldZ + shift;
                commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v));
            }
            // Vybrané dostanou 0, 1, 2 …
            int z = 0;
            foreach (var el in selected.OrderBy(e => e.ZIndex))
            {
                int oldZ = el.ZIndex;
                int newZ = z++;
                if (oldZ == newZ) continue;
                commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v));
            }
        }
        else
        {
            // Posun o 1 krok (dopředu nebo dozadu)
            foreach (var el in selected)
            {
                int oldZ = el.ZIndex;
                int newZ = Math.Max(0, oldZ + sign);
                if (oldZ == newZ) continue;
                commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v));
            }
        }

        if (commands.Count == 0) return;

        string desc = (sign > 0, toExtreme) switch
        {
            (true,  true)  => "Přenést zcela dopředu",
            (true,  false) => "Přenést dopředu",
            (false, true)  => "Odeslat zcela dozadu",
            _              => "Odeslat dozadu"
        };

        _state.Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(desc, commands));
        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Zarovnání elementů

    private void BtnAlignLeft_Click(object sender, RoutedEventArgs e)    => AlignElements(AlignMode.Left);
    private void BtnAlignRight_Click(object sender, RoutedEventArgs e)   => AlignElements(AlignMode.Right);
    private void BtnAlignCenterH_Click(object sender, RoutedEventArgs e) => AlignElements(AlignMode.CenterH);
    private void BtnAlignTop_Click(object sender, RoutedEventArgs e)     => AlignElements(AlignMode.Top);
    private void BtnAlignBottom_Click(object sender, RoutedEventArgs e)  => AlignElements(AlignMode.Bottom);
    private void BtnAlignMiddleV_Click(object sender, RoutedEventArgs e) => AlignElements(AlignMode.MiddleV);
    private void BtnDistributeH_Click(object sender, RoutedEventArgs e)  => AlignElements(AlignMode.DistributeH);
    private void BtnDistributeV_Click(object sender, RoutedEventArgs e)  => AlignElements(AlignMode.DistributeV);

    private enum AlignMode { Left, Right, CenterH, Top, Bottom, MiddleV, DistributeH, DistributeV }

    private void AlignElements(AlignMode mode)
    {
        if (_state.ActiveSection is null) return;

        var inSection = _state.SelectedElements
            .Where(el => _state.ActiveSection.Elements.Contains(el))
            .ToList();

        int minCount = mode is AlignMode.DistributeH or AlignMode.DistributeV ? 3 : 2;
        if (inSection.Count < minCount) return;

        var commands = new List<IDesignerCommand>();

        switch (mode)
        {
            case AlignMode.Left:
            {
                float target = inSection.Min(e => e.X);
                foreach (var el in inSection.Where(e => e.X != target))
                    commands.Add(MakeXCmd(el, target));
                break;
            }
            case AlignMode.Right:
            {
                float target = inSection.Max(e => e.X + e.Width);
                foreach (var el in inSection)
                {
                    float newX = target - el.Width;
                    if (Math.Abs(el.X - newX) > 0.01f) commands.Add(MakeXCmd(el, newX));
                }
                break;
            }
            case AlignMode.CenterH:
            {
                float cx = (inSection.Min(e => e.X) + inSection.Max(e => e.X + e.Width)) / 2f;
                foreach (var el in inSection)
                {
                    float newX = cx - el.Width / 2f;
                    if (Math.Abs(el.X - newX) > 0.01f) commands.Add(MakeXCmd(el, newX));
                }
                break;
            }
            case AlignMode.Top:
            {
                float target = inSection.Min(e => e.Y);
                foreach (var el in inSection.Where(e => e.Y != target))
                    commands.Add(MakeYCmd(el, target));
                break;
            }
            case AlignMode.Bottom:
            {
                float target = inSection.Max(e => e.Y + e.EffectiveDesignerHeight);
                foreach (var el in inSection)
                {
                    float newY = target - el.EffectiveDesignerHeight;
                    if (Math.Abs(el.Y - newY) > 0.01f) commands.Add(MakeYCmd(el, newY));
                }
                break;
            }
            case AlignMode.MiddleV:
            {
                float cy = (inSection.Min(e => e.Y) + inSection.Max(e => e.Y + e.EffectiveDesignerHeight)) / 2f;
                foreach (var el in inSection)
                {
                    float newY = cy - el.EffectiveDesignerHeight / 2f;
                    if (Math.Abs(el.Y - newY) > 0.01f) commands.Add(MakeYCmd(el, newY));
                }
                break;
            }
            case AlignMode.DistributeH:
            {
                var sorted = inSection.OrderBy(e => e.X).ToList();
                float totalW = sorted.Sum(e => e.Width);
                float span   = sorted.Last().X + sorted.Last().Width - sorted.First().X;
                float gap    = (span - totalW) / (sorted.Count - 1);
                float cur    = sorted.First().X;
                foreach (var el in sorted)
                {
                    if (Math.Abs(el.X - cur) > 0.01f) commands.Add(MakeXCmd(el, cur));
                    cur += el.Width + gap;
                }
                break;
            }
            case AlignMode.DistributeV:
            {
                var sorted = inSection.OrderBy(e => e.Y).ToList();
                float totalH = sorted.Sum(e => e.EffectiveDesignerHeight);
                float span   = sorted.Last().Y + sorted.Last().EffectiveDesignerHeight - sorted.First().Y;
                float gap    = (span - totalH) / (sorted.Count - 1);
                float cur    = sorted.First().Y;
                foreach (var el in sorted)
                {
                    if (Math.Abs(el.Y - cur) > 0.01f) commands.Add(MakeYCmd(el, cur));
                    cur += el.EffectiveDesignerHeight + gap;
                }
                break;
            }
        }

        if (commands.Count == 0) return;

        string desc = mode switch
        {
            AlignMode.Left        => "Zarovnat vlevo",
            AlignMode.Right       => "Zarovnat vpravo",
            AlignMode.CenterH     => "Zarovnat na střed (vodorovně)",
            AlignMode.Top         => "Zarovnat nahoru",
            AlignMode.Bottom      => "Zarovnat dolů",
            AlignMode.MiddleV     => "Zarovnat na střed (svisle)",
            AlignMode.DistributeH => "Rozložit vodorovně",
            AlignMode.DistributeV => "Rozložit svisle",
            _                     => "Zarovnat"
        };

        _state.Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(desc, commands));
        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    private static IDesignerCommand MakeXCmd(TemplateElement el, float newX)
        => new SetPropertyCommand("X", el.X, newX, v => el.X = (float)v);

    private static IDesignerCommand MakeYCmd(TemplateElement el, float newY)
        => new SetPropertyCommand("Y", el.Y, newY, v => el.Y = (float)v);

    private void DeleteSelected()
    {
        if (_state.SelectedElements.Count == 0) return;

        // Najdi sekce pro každý element
        var commands = new List<IDesignerCommand>();
        foreach (var el in _state.SelectedElements.ToList())
        {
            var section = _state.CurrentTemplate.Sections
                              .FirstOrDefault(s => s.Elements.Contains(el));
            if (section is null || el.IsLocked) continue;
            commands.Add(new DeleteElementCommand(section, el));
        }

        if (commands.Count == 0) return;

        _propsWindow?.Hide();
        _state.SelectedElements.Clear();

        if (commands.Count == 1)
            _state.Execute(commands[0]);
        else
            _state.Execute(new CompositeCommand("Smazat elementy", commands));

        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Pole agendy — browser databázových polí

    private void RefreshFieldsList()
    {
        var agendaCode = _state.CurrentTemplate.AgendaCode;

        if (string.IsNullOrWhiteSpace(agendaCode))
        {
            AgendaNameText.Text  = "Agenda: — (šablona bez agendy)";
            FieldsList.ItemsSource = null;
            return;
        }

        var schema = _schemaProvider.GetSchema(agendaCode);

        if (schema is null)
        {
            AgendaNameText.Text  = $"Agenda: {agendaCode} (schéma nenalezeno)";
            FieldsList.ItemsSource = null;
            return;
        }

        AgendaNameText.Text    = $"Agenda: {schema.AgendaName}";
        FieldsList.ItemsSource = schema.Fields;
    }

    private void LeftTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Při přepnutí na záložku Pole agendy aktualizuj seznam
        if (TabFields?.IsSelected == true)
            RefreshFieldsList();
    }

    private void FieldsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FieldsList.SelectedItem is not DataField field) return;

        string placeholder = $"{{{field.FieldName}}}";

        // Pokud je vybrán TextElement → vlož placeholder do textu (undo-able)
        if (_state.SelectedElements.Count == 1 && _state.SelectedElements[0] is TextElement textEl)
        {
            string oldText = textEl.Text;
            string newText = oldText + placeholder;
            _state.Execute(new SetPropertyCommand(
                $"Vložit {placeholder}",
                oldText, newText,
                v => textEl.Text = (string)v));
            InvalidateCanvas();
            UpdatePropertiesPanel();
        }
        else
        {
            // Není vybrán TextElement → zkopíruj do schránky
            System.Windows.Clipboard.SetText(placeholder);
            StatusText.Text = $"Zkopírováno do schránky: {placeholder}";
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Přidat sekci / element (stub)

    private void BtnAddSection_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        void Add(string label, Func<Section> factory)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) =>
            {
                var s = factory();
                _state.CurrentTemplate.Sections.Add(s);
                ActivateSection(s);
                UpdateCanvasSize();
            };
            menu.Items.Add(item);
        }

        Add("Záhlaví",                  () => new SectionHeader());
        Add("Pevná výška",              () => new SectionFixedHeight { Name = $"Sekce {_state.CurrentTemplate.Sections.Count + 1}" });
        Add("Tabulka",                  () => new SectionTable());
        Add("Tabulka (vlastní design)", () => new SectionTableDesigned());
        Add("Nekonečný text",           () => new SectionInfiniteText());
        Add("Oddělovač",                () => new SectionSeparator());
        Add("Zápatí",                   () => new SectionFooter());

        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    private void BtnAddElement_Click(object sender, RoutedEventArgs e)
    {
        if (_state.ActiveSection is null)
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
                _state.Execute(new AddElementCommand(_state.ActiveSection, element));
                _state.SelectedElements.Clear();
                _state.SelectedElements.Add(element);
                UpdatePropertiesPanel();
                InvalidateCanvas();
            };
            menu.Items.Add(item);
        }

        Add("Text",       () => new TextElement      { X = 5, Y = 5, Width = 80, Height = 8, Text = "Text" });
        Add("Obdélník",   () => new RectangleElement { X = 5, Y = 5, Width = 60, Height = 20, StrokeWidth = 0.5f });
        Add("Čára",       () => new LineElement      { X = 0, Y = 10, Width = 190, Height = 1 });
        Add("Obrázek",    () => new ImageElement     { X = 5, Y = 5, Width = 40, Height = 30 });
        Add("Tabulka",    () => new TableElement     { X = 0, Y = 0, Width = 190,
            Columns = [ new TableColumn { Header = "Sloupec 1", Width = 95, Field = "POLE1" },
                        new TableColumn { Header = "Sloupec 2", Width = 95, Field = "POLE2" } ] });

        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(skCanvas);
        float cx = (float)pos.X, cy = (float)pos.Y, cw = (float)skCanvas.ActualWidth;

        // 0. Dvojklik na element → otevřít dialog vlastností
        if (e.ClickCount == 2)
        {
            var dblHit = _canvas.HitTestElement(cx, cy, cw);
            if (dblHit is not null)
            {
                var (dblSection, dblElement) = dblHit.Value;
                if (!_state.SelectedElements.Contains(dblElement))
                {
                    _state.SelectedElements.Clear();
                    _state.SelectedElements.Add(dblElement);
                    ActivateSection(dblSection);
                }
                _propsWindow!.ShowForElement(dblElement);
                e.Handled = true;
                return;
            }
        }

        // 1. Klik na resize handle vybraného elementu?
        var handleHit = _canvas.HitTestHandle(cx, cy, cw);
        if (handleHit.HasValue)
        {
            var (el, handle) = handleHit.Value;
            _resizeElement     = el;
            _resizeHandle      = handle;
            _resizeInitX       = el.X;
            _resizeInitY       = el.Y;
            _resizeInitW       = el.Width;
            _resizeInitH       = el.Height;
            var origin         = _canvas.GetSectionOrigins(cw)
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

        // 2. Klik na element?
        var hit = _canvas.HitTestElement(cx, cy, cw);
        if (hit is not null)
        {
            var (section, element) = hit.Value;

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // Ctrl+klik: přidá / odebere z výběru
                if (_state.SelectedElements.Contains(element))
                    _state.SelectedElements.Remove(element);
                else
                    _state.SelectedElements.Add(element);
            }
            else if (!_state.SelectedElements.Contains(element))
            {
                // Klik na dosud nevybraný element: vybrat jen tento
                _state.SelectedElements.Clear();
                _state.SelectedElements.Add(element);
            }
            // Klik na již vybraný element bez Ctrl → zachovat celý výběr (drag všech)

            ActivateSection(section);

            // Připrav drag pro všechny vybrané elementy
            _dragElement           = element;
            _dragMouseStartCanvasX = cx;
            _dragMouseStartCanvasY = cy;
            _dragInitPositions.Clear();
            foreach (var el in _state.SelectedElements)
                _dragInitPositions[el] = (el.X, el.Y);

            _mouseMode = MouseMode.Dragging;
            skCanvas.CaptureMouse();

            UpdatePropertiesPanel();
            InvalidateCanvas();
            e.Handled = true;
            return;
        }

        // 3. Klik na prázdné místo → rubber band selection
        var section2 = _canvas.HitTestSection(cx, cy, cw);
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            _state.SelectedElements.Clear();
        ActivateSection(section2);
        UpdatePropertiesPanel();

        _rubberBandStart = new SKPoint(cx, cy);
        _rubberBandRect  = null;
        _mouseMode       = MouseMode.RubberBand;
        skCanvas.CaptureMouse();
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(skCanvas);
        float cx = (float)pos.X, cy = (float)pos.Y, cw = (float)skCanvas.ActualWidth;

        // ── Dragging ──
        if (_mouseMode == MouseMode.Dragging)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndDrag(commit: false); return; }
            if (_dragElement is null || _dragInitPositions.Count == 0) return;

            float deltaMmX = (cx - _dragMouseStartCanvasX) / _canvas.MmToPx;
            float deltaMmY = (cy - _dragMouseStartCanvasY) / _canvas.MmToPx;

            // Snap — zaokrouhlíme finální pozici referenčního elementu, z toho dopočítáme delta
            if (_state.SnapToGrid && _dragInitPositions.TryGetValue(_dragElement, out var refInit))
            {
                float g = _state.GridSize;
                float snappedX = MathF.Round((refInit.X + deltaMmX) / g) * g;
                float snappedY = MathF.Round((refInit.Y + deltaMmY) / g) * g;
                deltaMmX = snappedX - refInit.X;
                deltaMmY = snappedY - refInit.Y;
            }

            // Group clamping — celá skupina se zastaví, jakmile první element narazí na okraj sekce
            var (clampedX, clampedY) = ClampDragDelta(deltaMmX, deltaMmY);

            foreach (var kvp in _dragInitPositions)
            {
                kvp.Key.X = kvp.Value.X + clampedX;
                kvp.Key.Y = kvp.Value.Y + clampedY;
            }

            InvalidateCanvas();
            return;
        }

        // ── Resizing ──
        if (_mouseMode == MouseMode.Resizing)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndResize(commit: false); return; }
            if (_resizeElement is null) return;

            float mouseMmX = (cx - _resizeOriginX) / _canvas.MmToPx;
            float mouseMmY = (cy - _resizeOriginY) / _canvas.MmToPx;

            if (_state.SnapToGrid)
            {
                float g = _state.GridSize;
                mouseMmX = MathF.Round(mouseMmX / g) * g;
                mouseMmY = MathF.Round(mouseMmY / g) * g;
            }

            var (newX, newY, newW, newH) = ComputeResize(mouseMmX, mouseMmY);
            _resizeElement.X      = newX;
            _resizeElement.Y      = newY;
            _resizeElement.Width  = newW;
            _resizeElement.Height = newH;

            InvalidateCanvas();
            return;
        }

        // ── RubberBand ──
        if (_mouseMode == MouseMode.RubberBand)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndRubberBand(); return; }
            _rubberBandRect = new SKRect(
                Math.Min(_rubberBandStart.X, cx), Math.Min(_rubberBandStart.Y, cy),
                Math.Max(_rubberBandStart.X, cx), Math.Max(_rubberBandStart.Y, cy));
            InvalidateCanvas();
            return;
        }

        // ── Idle — aktualizuj kurzor ──
        var handleHit = _canvas.HitTestHandle(cx, cy, cw);
        if (handleHit.HasValue)
            skCanvas.Cursor = GetResizeCursor(handleHit.Value.Handle);
        else if (_canvas.HitTestElement(cx, cy, cw).HasValue)
            skCanvas.Cursor = Cursors.SizeAll;
        else
            skCanvas.Cursor = Cursors.Arrow;
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_mouseMode == MouseMode.Dragging)
            EndDrag(commit: true);
        else if (_mouseMode == MouseMode.Resizing)
            EndResize(commit: true);
        else if (_mouseMode == MouseMode.RubberBand)
            EndRubberBand();
    }

    private void EndRubberBand()
    {
        if (_rubberBandRect.HasValue)
        {
            float cw       = (float)skCanvas.ActualWidth;
            var   elements = _canvas.HitTestRect(_rubberBandRect.Value, cw);

            // Omez výběr na aktivní sekci
            if (_state.ActiveSection is not null)
                elements = elements.Where(e => _state.ActiveSection.Elements.Contains(e)).ToList();

            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                _state.SelectedElements.Clear();

            foreach (var el in elements)
                if (!_state.SelectedElements.Contains(el))
                    _state.SelectedElements.Add(el);
        }

        _rubberBandRect = null;
        _mouseMode      = MouseMode.Idle;
        skCanvas.ReleaseMouseCapture();
        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    private void EndDrag(bool commit)
    {
        if (commit)
        {
            var moves = new List<IDesignerCommand>();
            foreach (var kvp in _dragInitPositions)
            {
                if (kvp.Key.X != kvp.Value.X || kvp.Key.Y != kvp.Value.Y)
                    moves.Add(new MoveElementCommand(kvp.Key, kvp.Value.X, kvp.Value.Y, kvp.Key.X, kvp.Key.Y));
            }
            if (moves.Count == 1)
                _state.Execute(moves[0]);
            else if (moves.Count > 1)
                _state.Execute(new CompositeCommand("Přesun elementů", moves));
        }
        else
        {
            // Zrušení dragu — obnov původní pozice
            foreach (var kvp in _dragInitPositions)
            {
                kvp.Key.X = kvp.Value.X;
                kvp.Key.Y = kvp.Value.Y;
            }
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
        if (_resizeElement is not null)
        {
            if (commit)
            {
                float? finalH = _resizeElement.Height;
                if (_resizeElement.X     != _resizeInitX || _resizeElement.Y    != _resizeInitY ||
                    _resizeElement.Width != _resizeInitW  || finalH             != _resizeInitH)
                {
                    _state.Execute(new ResizeElementCommand(
                        _resizeElement,
                        _resizeInitX, _resizeInitY, _resizeInitW, _resizeInitH,
                        _resizeElement.X, _resizeElement.Y, _resizeElement.Width, finalH));
                }
            }
            else
            {
                // Zrušení resize — obnov původní hodnoty
                _resizeElement.X      = _resizeInitX;
                _resizeElement.Y      = _resizeInitY;
                _resizeElement.Width  = _resizeInitW;
                _resizeElement.Height = _resizeInitH;
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
        float initH  = _resizeInitH ?? _resizeElement!.EffectiveDesignerHeight;
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
                newW = right - newX; newH = bottom - newY; setsH = true;
                break;
            case ResizeHandle.TopCenter:
                newY = Math.Min(_resizeInitY + dy, bottom - minH);
                newH = bottom - newY; setsH = true;
                break;
            case ResizeHandle.TopRight:
                newW = Math.Max(minW, _resizeInitW + dx);
                newY = Math.Min(_resizeInitY + dy, bottom - minH);
                newH = bottom - newY; setsH = true;
                break;
            case ResizeHandle.MiddleLeft:
                newX = Math.Min(_resizeInitX + dx, right - minW);
                newW = right - newX;
                break;
            case ResizeHandle.MiddleRight:
                newW = Math.Max(minW, _resizeInitW + dx);
                break;
            case ResizeHandle.BottomLeft:
                newX = Math.Min(_resizeInitX + dx, right - minW);
                newW = right - newX;
                newH = Math.Max(minH, initH + dy); setsH = true;
                break;
            case ResizeHandle.BottomCenter:
                newH = Math.Max(minH, initH + dy); setsH = true;
                break;
            case ResizeHandle.BottomRight:
                newW = Math.Max(minW, _resizeInitW + dx);
                newH = Math.Max(minH, initH + dy); setsH = true;
                break;
        }

        // Handle nemění výšku → zachovat původní (null = dynamická)
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

    /// <summary>
    /// Omezí drag delta tak, aby žádný z přesouvaných elementů nevyjel mimo svou sekci.
    /// Celá skupina se zastaví, jakmile první element narazí na okraj — zachovají se
    /// relativní vzdálenosti mezi elementy.
    /// </summary>
    private (float deltaX, float deltaY) ClampDragDelta(float rawDeltaX, float rawDeltaY)
    {
        float sectionW = _state.CurrentTemplate.PageSize.PrintableWidth;

        float minDeltaX = float.MinValue, maxDeltaX = float.MaxValue;
        float minDeltaY = float.MinValue, maxDeltaY = float.MaxValue;

        foreach (var kvp in _dragInitPositions)
        {
            var el    = kvp.Key;
            float initX = kvp.Value.X;
            float initY = kvp.Value.Y;
            float elH   = el.EffectiveDesignerHeight;

            var section  = _state.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(el));
            float sectionH = section?.EffectiveDesignerHeight ?? float.MaxValue;

            // Levý / pravý okraj
            minDeltaX = Math.Max(minDeltaX, -initX);
            maxDeltaX = Math.Min(maxDeltaX, sectionW - el.Width - initX);

            // Horní / dolní okraj
            minDeltaY = Math.Max(minDeltaY, -initY);
            maxDeltaY = Math.Min(maxDeltaY, sectionH - elH - initY);
        }

        float clampedX = minDeltaX <= maxDeltaX ? Math.Clamp(rawDeltaX, minDeltaX, maxDeltaX) : 0f;
        float clampedY = minDeltaY <= maxDeltaY ? Math.Clamp(rawDeltaY, minDeltaY, maxDeltaY) : 0f;

        return (clampedX, clampedY);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Klávesnice

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        if (e.Key == Key.Z && ctrl)
        {
            _state.Undo(); InvalidateCanvas(); RefreshSectionsList(); UpdatePropertiesPanel();
            e.Handled = true;
        }
        else if (e.Key == Key.Y && ctrl)
        {
            _state.Redo(); InvalidateCanvas(); RefreshSectionsList(); UpdatePropertiesPanel();
            e.Handled = true;
        }
        else if (e.Key == Key.S && ctrl)
        {
            MenuSave_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.P && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            MenuPageProps_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.PageUp && ctrl)
        {
            // Ctrl+PgUp / Ctrl+Shift+PgUp — ZIndex dopředu
            ChangeZIndex(+1, toExtreme: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
        else if (e.Key == Key.PageDown && ctrl)
        {
            // Ctrl+PgDn / Ctrl+Shift+PgDn — ZIndex dozadu
            ChangeZIndex(-1, toExtreme: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
        else if (e.Key == Key.A && ctrl && _state.ActiveSection is not null)
        {
            // Ctrl+A — vybrat všechny elementy aktivní sekce
            _state.SelectedElements.Clear();
            _state.SelectedElements.AddRange(_state.ActiveSection.Elements);
            UpdatePropertiesPanel();
            InvalidateCanvas();
            e.Handled = true;
        }
        else if (e.Key == Key.C && ctrl)
        {
            CopySelected();
            e.Handled = true;
        }
        else if (e.Key == Key.V && ctrl)
        {
            PasteClipboard();
            e.Handled = true;
        }
        else if (e.Key == Key.D && ctrl)
        {
            DuplicateSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            DeleteSelected();
            e.Handled = true;
        }
        else if ((e.Key == Key.Up || e.Key == Key.Down)
                 && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                 && _state.ActiveSection is not null)
        {
            // Ctrl+Shift+↑↓ — přesunout aktivní sekci nahoru / dolů
            MoveSection(_state.ActiveSection, e.Key == Key.Up ? -1 : +1);
            e.Handled = true;
        }
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down
                 && _state.SelectedElements.Count > 0)
        {
            MoveByKey(e.Key, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _state.SelectedElements.Count == 1)
        {
            // Enter — otevřít dialog vlastností pro vybraný element
            _propsWindow!.ShowForElement(_state.SelectedElements[0]);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // Escape — zrušit výběr (nebo zavřít dialog)
            if (_propsWindow?.IsVisible == true)
            {
                _propsWindow.Hide();
            }
            else
            {
                _state.SelectedElements.Clear();
                UpdatePropertiesPanel();
                InvalidateCanvas();
            }
            e.Handled = true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Pomocné metody

    private void CopySelected()
    {
        if (_state.SelectedElements.Count == 0) return;
        _clipboard = _state.SelectedElements.Select(el => el.Clone()).ToList();
    }

    private void PasteClipboard()
    {
        if (_clipboard.Count == 0) return;
        if (_state.ActiveSection is null)
        {
            MessageBox.Show("Nejprve vyberte sekci.", "Vložit", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var newElements = _clipboard.Select(el =>
        {
            var clone = el.Clone();
            clone.X = Math.Min(clone.X + 5f,
                _state.CurrentTemplate.PageSize.PrintableWidth - clone.Width);
            clone.Y = Math.Min(clone.Y + 5f,
                _state.ActiveSection.EffectiveDesignerHeight - clone.EffectiveDesignerHeight);
            return clone;
        }).ToList();

        var commands = newElements
            .Select(el => (IDesignerCommand)new AddElementCommand(_state.ActiveSection, el))
            .ToList();

        if (commands.Count == 1) _state.Execute(commands[0]);
        else _state.Execute(new CompositeCommand("Vložit elementy", commands));

        _state.SelectedElements.Clear();
        _state.SelectedElements.AddRange(newElements);
        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    private void DuplicateSelected()
    {
        CopySelected();
        PasteClipboard();
    }

    /// <summary>
    /// Posune vybrané elementy o krok šipkou.
    /// Shift = krok o velikost mřížky, bez Shift = 1 mm.
    /// </summary>
    private void MoveByKey(Key key, bool largeStep)
    {
        float step = largeStep ? _state.GridSize : 1f;
        float dx = key switch { Key.Left => -step, Key.Right => step, _ => 0f };
        float dy = key switch { Key.Up   => -step, Key.Down  => step, _ => 0f };

        // Ulož počáteční pozice
        var initPositions = _state.SelectedElements
            .ToDictionary(el => el, el => (el.X, el.Y));

        // Clamp delta stejnou logikou jako u dragu myší
        float sectionW = _state.CurrentTemplate.PageSize.PrintableWidth;
        float clampedDx = dx, clampedDy = dy;

        foreach (var kvp in initPositions)
        {
            var el    = kvp.Key;
            float elH = el.EffectiveDesignerHeight;
            var section = _state.CurrentTemplate.Sections
                              .FirstOrDefault(s => s.Elements.Contains(el));
            float sectionH = section?.EffectiveDesignerHeight ?? float.MaxValue;

            clampedDx = Math.Clamp(clampedDx, -kvp.Value.X, sectionW - el.Width - kvp.Value.X);
            clampedDy = Math.Clamp(clampedDy, -kvp.Value.Y, sectionH - elH - kvp.Value.Y);
        }

        if (clampedDx == 0f && clampedDy == 0f) return;

        // Aplikuj posun
        foreach (var kvp in initPositions)
        {
            kvp.Key.X = kvp.Value.X + clampedDx;
            kvp.Key.Y = kvp.Value.Y + clampedDy;
        }

        // Zapiš do undo stacku
        var moves = initPositions
            .Where(kvp => kvp.Key.X != kvp.Value.X || kvp.Key.Y != kvp.Value.Y)
            .Select(kvp => (IDesignerCommand)new MoveElementCommand(
                kvp.Key, kvp.Value.X, kvp.Value.Y, kvp.Key.X, kvp.Key.Y))
            .ToList();

        if (moves.Count == 1)
            _state.Execute(moves[0]);
        else if (moves.Count > 1)
            _state.Execute(new CompositeCommand("Přesun šipkou", moves));

        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    private void UpdatePropertiesPanel()
    {
        if (_state.SelectedElements.Count == 1)
        {
            var el = _state.SelectedElements[0];
            string type = el.GetType().Name.Replace("Element", "");
            StatusText.Text = $"{type}  |  X: {el.X:0.#}  Y: {el.Y:0.#}  |  Šířka: {el.Width:0.#} mm" +
                              (el.IsLocked ? "  [ZAMČENO]" : "") +
                              "  |  2× klik nebo Enter = vlastnosti";

            // Pokud je dialog již viditelný, aktualizuj ho pro aktuálně vybraný element
            if (_propsWindow!.IsVisible)
                _propsWindow.ShowForElement(el);
            return;
        }

        _propsWindow?.Hide();

        StatusText.Text = _state.SelectedElements.Count > 1
            ? $"Vybráno {_state.SelectedElements.Count} elementů  |  Ctrl+klik pro odebrání z výběru"
            : _state.ActiveSection is not null
                ? $"Sekce: {_state.ActiveSection.Name}  |  " +
                  $"Výška: {_state.ActiveSection.DesignerHeight?.ToString("0.#") ?? "dynamická"} mm  |  " +
                  $"Elementy: {_state.ActiveSection.Elements.Count}"
                : "Vyberte element nebo sekci.";
    }

    private void UpdateTitle()
    {
        string name = _state.CurrentTemplate.Name;
        string file = _currentFilePath is not null ? $" — {Path.GetFileName(_currentFilePath)}" : "";
        Title = $"{name}{file} — Template Designer";
    }

    private bool ConfirmUnsavedChanges()
    {
        // TODO: sledovat dirty flag — prozatím vždy povolíme
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Ukázková šablona pro první spuštění

    private static Template CreateSampleTemplate()
    {
        var t = new Template { Name = "Ukázková šablona — Jízdní řád", AgendaCode = "JR" };

        var header = new SectionHeader();
        header.Elements.Add(new RectangleElement { X = 0, Y = 0, Width = 190, Height = 20, FillColor = "#FF1A5276" });
        header.Elements.Add(new TextElement      { X = 5, Y = 4, Width = 180, Height = 12,
            Text = "Jízdní řád — {NAZEV}", FontSize = 14,
            FontWeight = TextFontWeight.Bold, TextColor = "#FFFFFFFF", ZIndex = 1 });
        t.Sections.Add(header);

        var info = new SectionFixedHeight { Name = "Info o lince", DesignerHeight = 18 };
        info.Elements.Add(new TextElement { X = 0,  Y = 3, Width = 90,  Height = 9, Text = "Linka: {LINKA}",  FontSize = 10 });
        info.Elements.Add(new TextElement { X = 95, Y = 3, Width = 95,  Height = 9, Text = "Datum: {DATUM}",  FontSize = 10 });
        info.Elements.Add(new LineElement { X = 0, Y = 16, Width = 190, Height = 1, StrokeWidth = 0.3f });
        t.Sections.Add(info);

        var table = new SectionTable { Name = "Jízdní řád", DataSource = "JIZDNI_RAD" };
        table.Elements.Add(new TableElement
        {
            X = 0, Y = 0, Width = 190,
            DataSource = "JIZDNI_RAD",
            RowHeight  = 6,
            Columns    =
            [
                new TableColumn { Header = "Čas",       Width = 30,  Field = "CAS" },
                new TableColumn { Header = "Zastávka",  Width = 100, Field = "ZASTAVKA" },
                new TableColumn { Header = "Nástup",    Width = 30,  Field = "NASTUP" },
                new TableColumn { Header = "Výstup",    Width = 30,  Field = "VYSTUP" }
            ]
        });
        t.Sections.Add(table);

        var separator = new SectionSeparator();
        t.Sections.Add(separator);

        var footer = new SectionFooter();
        footer.Elements.Add(new TextElement
        {
            X = 0, Y = 3, Width = 190, Height = 8,
            Text = "Strana {STRANA} / {STRAN}",
            FontSize = 9, HorizontalAlignment = TextHorizontalAlignment.Right,
            TextColor = "#FF666666"
        });
        t.Sections.Add(footer);

        return t;
    }
}
