using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using EDControls;
using EDControls.EDViewModel;
using Microsoft.Win32;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Config;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;
using ALWA.SYS;

namespace TemplateDesigner;

// ── Align mode enum (public so code-behind can reference it) ─────────────────
public enum AlignMode { Left, Right, CenterH, Top, Bottom, MiddleV, DistributeH, DistributeV }
public enum DuplicateDirection { Up, Down, Left, Right }

// ─────────────────────────────────────────────────────────────────────────────
// Main ViewModel
// ─────────────────────────────────────────────────────────────────────────────

public class TemplateDesignerViewModel : EDViewModel
{
    // ── Designer state (public — code-behind accesses canvas/resize directly) ─
    public DesignerState State { get; } = new();

    // ── File & path ───────────────────────────────────────────────────────────
    public string? CurrentFilePath { get; private set; }

    // ── Providers ─────────────────────────────────────────────────────────────
    public  IDataSchemaProvider    SchemaProvider        { get; }
    public  CacheAgendaProvider?   CacheProvider         { get; }
    public  ITableTemplateProvider? TableTemplateProvider => CacheProvider;
    private readonly SectionTemplateManager _sectionTemplates;

    // ── Preview params ────────────────────────────────────────────────────────
    private List<TemplateParameter>             _agendaParameters    = [];
    private readonly Dictionary<string,object?> _previewParams       = new();
    private Dictionary<string,object?>?         _cachedParamValues;
    private string?                             _lastParamsAgendaCode;

    // ── Real preview data ────────────────────────────────────────────────────
    private List<DataContext>?                  _realPreviewData;
    private int                                _realPreviewIndex;

    /// <summary>Počet dostupných stránek reálných dat (0 = mock mód).</summary>
    public int RealPreviewPageCount => _realPreviewData?.Count ?? 0;

    /// <summary>Index aktuálně zobrazené stránky reálných dat (0-based).</summary>
    public int RealPreviewIndex
    {
        get => _realPreviewIndex;
        set
        {
            if (_realPreviewData is null || _realPreviewData.Count == 0) return;
            var clamped = Math.Clamp(value, 0, _realPreviewData.Count - 1);
            if (Set(ref _realPreviewIndex, clamped))
                RequestPreviewRefresh?.Invoke();
        }
    }

    /// <summary>Parametry agendy pro PrintDialog.</summary>
    public IReadOnlyList<TemplateParameter> AgendaParameters => _agendaParameters;

    // ── Clipboard ─────────────────────────────────────────────────────────────
    private List<TemplateElement> _clipboard = [];

    // ── Observable collections (drive XAML data-binding) ──────────────────────
    public ObservableCollection<SectionVM>          Sections          { get; } = [];
    public ObservableCollection<SchemaGroupVM>      SchemaGroups      { get; } = [];
    public ObservableCollection<ParameterControlVM> PreviewParameters { get; } = [];

    // ── Bindable properties ───────────────────────────────────────────────────
    public bool CanUndo => State.CanUndo;
    public bool CanRedo => State.CanRedo;

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    private string _agendaNameText = "Agenda: —";
    public string AgendaNameText
    {
        get => _agendaNameText;
        set => Set(ref _agendaNameText, value);
    }

    private bool _hasParameters;
    public bool HasParameters
    {
        get => _hasParameters;
        private set => Set(ref _hasParameters, value);
    }

    public bool ShowGrid
    {
        get => State.ShowGrid;
        set { State.ShowGrid = value; OnPropertyChanged(); RequestCanvasRefresh?.Invoke(); }
    }

    public bool SnapToGrid
    {
        get => State.SnapToGrid;
        set { State.SnapToGrid = value; OnPropertyChanged(); }
    }

    public bool SnapToElements
    {
        get => State.SnapToElements;
        set { State.SnapToElements = value; OnPropertyChanged(); }
    }

    // ── Callbacks wired by code-behind ────────────────────────────────────────
    public Action? RequestCanvasRefresh      { get; set; }
    public Action? RequestTitleUpdate        { get; set; }
    public Action? RequestCanvasSizeUpdate   { get; set; }
    public Action? RequestPropertiesPanelUpdate { get; set; }
    public Action? RequestPreviewRefresh     { get; set; }

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fired after Sections is rebuilt so code-behind can restore ListBox selection.</summary>
    public event Action? SectionsRebuilt;

    // ── Commands ──────────────────────────────────────────────────────────────
    private RelayCommand? _undo, _redo, _save, _saveAs, _new, _open, _exportPdf, _delete, _copy, _paste;

    public RelayCommand UndoCommand     => _undo      ??= new(Undo,          () => CanUndo);
    public RelayCommand RedoCommand     => _redo      ??= new(Redo,          () => CanRedo);
    public RelayCommand SaveCommand     => _save      ??= new(SaveTemplate);
    public RelayCommand SaveAsCommand   => _saveAs    ??= new(SaveAsTemplate);
    public RelayCommand NewCommand      => _new       ??= new(NewTemplate);
    public RelayCommand OpenCommand     => _open      ??= new(OpenTemplateAsync);
    public RelayCommand ExportPdfCommand=> _exportPdf ??= new(ExportPreviewPdf);
    public RelayCommand DeleteCommand   => _delete    ??= new(DeleteSelected);
    public RelayCommand CopyCommand     => _copy      ??= new(CopySelected);
    public RelayCommand PasteCommand    => _paste     ??= new(PasteClipboard);

    // ── Constructor ───────────────────────────────────────────────────────────
    public TemplateDesignerViewModel(MLRest? mlRest = null)
    {
        if (mlRest is not null) _services.MLRest = new MLRestAdapter(mlRest);

        var settings   = AppSettings.Load();
        var schemasDir = Path.Combine(AppContext.BaseDirectory, "schemas");
        _sectionTemplates = new SectionTemplateManager();

        CacheProvider = mlRest is not null
            ? CacheSchemaWiring.TryCreate(mlRest, settings)
            : CacheSchemaWiring.TryCreate(settings);

        SchemaProvider = CacheProvider is not null
            ? new CompositeDataSchemaProvider(CacheProvider, new JsonFileDataSchemaProvider(schemasDir))
            : (IDataSchemaProvider)new JsonFileDataSchemaProvider(schemasDir);

        State.CurrentTemplate = CreateSampleTemplate();
        RebuildSections();
    }

    // ── Execute wrapper ───────────────────────────────────────────────────────
    public void Execute(IDesignerCommand cmd)
    {
        State.Execute(cmd);
        NotifyUndoRedo();
    }

    private void Undo()
    {
        State.Undo();
        CleanupSelection();
        NotifyUndoRedo();
        RebuildSections();
        RequestCanvasRefresh?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
    }

    private void Redo()
    {
        State.Redo();
        CleanupSelection();
        NotifyUndoRedo();
        RebuildSections();
        RequestCanvasRefresh?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
    }

    /// <summary>
    /// Odebere z výběru elementy, které už nejsou v žádné sekci šablony
    /// (např. po undo AddElement nebo undo Split).
    /// </summary>
    private void CleanupSelection()
    {
        var allElements = State.CurrentTemplate.Sections.SelectMany(s => s.Elements).ToHashSet();
        State.SelectedElements.RemoveAll(e => !allElements.Contains(e));
    }

    private void NotifyUndoRedo()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    // ── Sections ──────────────────────────────────────────────────────────────
    public void RebuildSections()
    {
        Sections.Clear();
        foreach (var s in State.CurrentTemplate.Sections)
            Sections.Add(new SectionVM(s, this));
        SectionsRebuilt?.Invoke();
    }

    public void ActivateSection(Section? section)
    {
        State.ActiveSection = section;
        RebuildSections();
        RequestCanvasRefresh?.Invoke();
    }

    public void AddSection(Section section)
    {
        State.CurrentTemplate.Sections.Add(section);
        ActivateSection(section);
        RequestCanvasSizeUpdate?.Invoke();
    }

    public void MoveSection(Section section, int direction)
    {
        int idx    = State.CurrentTemplate.Sections.IndexOf(section);
        int newIdx = idx + direction;
        if (newIdx < 0 || newIdx >= State.CurrentTemplate.Sections.Count) return;

        Execute(new MoveSectionCommand(State.CurrentTemplate, section, direction));
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    public void ReorderSection(Section section, int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || toIndex < 0) return;
        if (toIndex >= State.CurrentTemplate.Sections.Count) return;

        Execute(new ReorderSectionCommand(State.CurrentTemplate, section, fromIndex, toIndex));
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    public void DeleteSection(Section section)
    {
        if (State.CurrentTemplate.Sections.Count <= 1)
        {
            MessageBox.Show("Šablona musí mít alespoň jednu sekci.", "Smazat sekci",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        State.SelectedElements.RemoveAll(el => section.Elements.Contains(el));

        if (State.ActiveSection == section)
            State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault(s => s != section);

        Execute(new DeleteSectionCommand(State.CurrentTemplate, section));
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    public void SaveSectionAsTemplate(Section section, Window? owner = null)
    {
        var dialog = new Window
        {
            Title                 = "Uložit sekci jako šablonu",
            Width                 = 400,
            Height                = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner                 = owner,
            ResizeMode            = ResizeMode.NoResize
        };

        var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(15) };
        stack.Children.Add(new System.Windows.Controls.TextBlock
            { Text = "Název šablony:", Margin = new Thickness(0, 0, 0, 5) });

        var nameBox = new System.Windows.Controls.TextBox
            { Text = section.Name, Margin = new Thickness(0, 0, 0, 15) };
        stack.Children.Add(nameBox);

        var buttonsPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        var btnCancel = new System.Windows.Controls.Button
            { Content = "Zrušit", Width = 80, Height = 25, Margin = new Thickness(0, 0, 10, 0), IsCancel = true };
        var btnSave   = new System.Windows.Controls.Button
            { Content = "Uložit", Width = 80, Height = 25, IsDefault = true };

        bool? result = null;
        btnSave.Click   += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text)) { MessageBox.Show("Zadejte název šablony."); return; }
            result = true;
            dialog.Close();
        };
        btnCancel.Click += (_, _) => { result = false; dialog.Close(); };

        buttonsPanel.Children.Add(btnCancel);
        buttonsPanel.Children.Add(btnSave);
        stack.Children.Add(buttonsPanel);
        dialog.Content = stack;
        nameBox.Focus();
        nameBox.SelectAll();
        dialog.ShowDialog();

        if (result == true)
        {
            try
            {
                _sectionTemplates.Save(section, nameBox.Text.Trim());
                MessageBox.Show($"Šablona '{nameBox.Text.Trim()}' byla uložena.",
                    "Uložit šablonu", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba při ukládání šablony: {ex.Message}",
                    "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public IReadOnlyList<string> GetCustomSectionTemplateNames() =>
        _sectionTemplates.GetAvailableTemplates();

    public Section? LoadCustomSectionTemplate(string name) =>
        _sectionTemplates.Load(name)?.Clone();

    // ── File operations ───────────────────────────────────────────────────────
    private void NewTemplate()
    {
        if (!ConfirmUnsavedChanges()) return;
        State.CurrentTemplate = new Template();
        CurrentFilePath = null;
        State.SelectedElements.Clear();
        State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault();
        RebuildSections();
        RefreshFieldsList();
        RequestTitleUpdate?.Invoke();
        RequestCanvasSizeUpdate?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
    }

    private async void OpenTemplateAsync()
    {
        if (!ConfirmUnsavedChanges()) return;
        var dlg = new OpenFileDialog { Filter = TemplateSerializer.FileFilter, Title = "Otevřít šablonu" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            State.CurrentTemplate = await TemplateSerializer.LoadAsync(dlg.FileName);
            CurrentFilePath = dlg.FileName;
            State.SelectedElements.Clear();
            State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault();
            RebuildSections();
            RefreshFieldsList();
            RequestTitleUpdate?.Invoke();
            RequestCanvasSizeUpdate?.Invoke();
            RequestPropertiesPanelUpdate?.Invoke();
        }
        catch (TemplateDeserializationException ex)
        {
            MessageBox.Show(ex.Message, "Chyba při načítání", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Loads a template that was generated externally (e.g. AI assistant).</summary>
    public void LoadGeneratedTemplate(Template template)
    {
        State.CurrentTemplate = template;
        State.SelectedElements.Clear();
        State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault();
        CurrentFilePath = null;

        RebuildSections();
        InvalidateAll();
        RefreshFieldsList();
    }

    private void InvalidateAll()
    {
        RequestTitleUpdate?.Invoke();
        RequestCanvasSizeUpdate?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
        RequestPreviewRefresh?.Invoke();
    }

    private async void SaveTemplate()
    {
        if (CurrentFilePath is null) { SaveAsTemplate(); return; }
        SyncParametersToTemplate();
        await TemplateSerializer.SaveAsync(State.CurrentTemplate, CurrentFilePath);
        RequestTitleUpdate?.Invoke();
    }

    private async void SaveAsTemplate()
    {
        var dlg = new SaveFileDialog
        {
            Filter   = TemplateSerializer.FileFilter,
            Title    = "Uložit šablonu",
            FileName = State.CurrentTemplate.Name
        };
        if (dlg.ShowDialog() != true) return;
        CurrentFilePath = dlg.FileName;
        SyncParametersToTemplate();
        await TemplateSerializer.SaveAsync(State.CurrentTemplate, CurrentFilePath);
        RequestTitleUpdate?.Invoke();
    }

    private void SyncParametersToTemplate()
    {
        State.CurrentTemplate.Parameters = _agendaParameters.Count > 0 ? [.. _agendaParameters] : null;
    }

    private void ExportPreviewPdf()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Exportovat náhled jako PDF",
            Filter     = "PDF soubory (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            FileName   = (Path.GetFileNameWithoutExtension(CurrentFilePath)
                         ?? (string.IsNullOrWhiteSpace(State.CurrentTemplate.Name)
                             ? "sablona" : State.CurrentTemplate.Name)) + ".pdf"
        };
        if (dlg.ShowDialog() != true) return;
        RequestExportPdf?.Invoke(dlg.FileName);
    }

    /// <summary>Code-behind wires this to actually call PreviewCanvas.RenderToPdf.</summary>
    public Action<string>? RequestExportPdf { get; set; }

    public bool ConfirmUnsavedChanges() => true; // TODO: dirty flag

    // ── Clipboard ─────────────────────────────────────────────────────────────
    public void CopySelected()
    {
        if (State.SelectedElements.Count == 0) return;
        _clipboard = State.SelectedElements.Select(el => el.Clone()).ToList();
    }

    public void PasteClipboard()
    {
        if (_clipboard.Count == 0) return;
        if (State.ActiveSection is null)
        {
            MessageBox.Show("Nejprve vyberte sekci.", "Vložit",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Mapování starý GroupId → nový GroupId (klony nedědí skupinu originálu)
        var groupIdMap = new Dictionary<string, string>();
        var newElements = _clipboard.Select(el =>
        {
            var clone = el.Clone();
            clone.X = Math.Min(clone.X + 5f,
                State.CurrentTemplate.PageSize.PrintableWidth - clone.Width);
            clone.Y = Math.Min(clone.Y + 5f,
                State.ActiveSection.EffectiveDesignerHeight - clone.EffectiveDesignerHeight);
            RemapGroupId(clone, groupIdMap);
            return clone;
        }).ToList();

        var commands = newElements
            .Select(el => (IDesignerCommand)new AddElementCommand(State.ActiveSection, el))
            .ToList();

        if (commands.Count == 1) Execute(commands[0]);
        else Execute(new CompositeCommand("Vložit elementy", commands));

        State.SelectedElements.Clear();
        State.SelectedElements.AddRange(newElements);
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    public void DuplicateSelected() => DuplicateSelected(direction: null);

    public void DuplicateSelected(DuplicateDirection? direction)
    {
        if (State.SelectedElements.Count == 0 || State.ActiveSection is null) return;

        float sectionW = State.CurrentTemplate.PageSize.PrintableWidth;
        float sectionH = State.ActiveSection.EffectiveDesignerHeight;
        float gap = SnapEngine.DefaultGapMm;

        var newElements = new List<TemplateElement>();
        var commands = new List<IDesignerCommand>();
        var groupIdMap = new Dictionary<string, string>();

        foreach (var orig in State.SelectedElements)
        {
            var clone = orig.Clone();
            float origH = orig.Height ?? orig.EffectiveDesignerHeight;
            float cloneH = clone.Height ?? clone.EffectiveDesignerHeight;

            if (direction is null)
            {
                clone.X = Math.Clamp(orig.X + 5f, 0f, Math.Max(0f, sectionW - clone.Width));
                clone.Y = Math.Clamp(orig.Y + 5f, 0f, Math.Max(0f, sectionH - cloneH));
            }
            else
            {
                (clone.X, clone.Y) = direction.Value switch
                {
                    DuplicateDirection.Down  => (orig.X, orig.Y + origH + gap),
                    DuplicateDirection.Up    => (orig.X, orig.Y - cloneH - gap),
                    DuplicateDirection.Right => (orig.X + orig.Width + gap, orig.Y),
                    DuplicateDirection.Left  => (orig.X - clone.Width - gap, orig.Y),
                    _ => (orig.X + 5f, orig.Y + 5f)
                };
                clone.X = Math.Clamp(clone.X, 0f, Math.Max(0f, sectionW - clone.Width));
                clone.Y = Math.Clamp(clone.Y, 0f, Math.Max(0f, sectionH - cloneH));
            }

            // Přemapovat GroupId — klony dostanou nový společný GroupId
            RemapGroupId(clone, groupIdMap);

            newElements.Add(clone);
            commands.Add(new AddElementCommand(State.ActiveSection, clone));
        }

        if (commands.Count == 1) Execute(commands[0]);
        else Execute(new CompositeCommand("Duplikovat elementy", commands));

        State.SelectedElements.Clear();
        State.SelectedElements.AddRange(newElements);
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Split element ─────────────────────────────────────────────────────────

    /// <summary>
    /// Rozdělí vybrané elementy na poloviny. Horizontal = nad/pod, Vertical = vedle sebe.
    /// Originál se zmenší na polovinu, klon se umístí do druhé poloviny.
    /// </summary>
    public void SplitSelected(bool horizontal)
    {
        if (State.SelectedElements.Count == 0 || State.ActiveSection is null) return;

        var commands = new List<IDesignerCommand>();
        var newSelection = new List<TemplateElement>();

        foreach (var orig in State.SelectedElements.ToList())
        {
            float origH = orig.Height ?? orig.EffectiveDesignerHeight;
            var clone = orig.Clone();

            if (horizontal)
            {
                // Rozdělit výšku na dvě poloviny
                float halfH = MathF.Round(origH / 2f, 2);
                commands.Add(new ResizeElementCommand(orig,
                    orig.X, orig.Y, orig.Width, orig.Height,
                    orig.X, orig.Y, orig.Width, halfH));
                clone.Y = orig.Y + halfH;
                clone.Height = MathF.Round(origH - halfH, 2);
            }
            else
            {
                // Rozdělit šířku na dvě poloviny
                float halfW = MathF.Round(orig.Width / 2f, 2);
                commands.Add(new ResizeElementCommand(orig,
                    orig.X, orig.Y, orig.Width, orig.Height,
                    orig.X, orig.Y, halfW, orig.Height));
                clone.X = orig.X + halfW;
                clone.Width = MathF.Round(orig.Width - halfW, 2);
            }

            commands.Add(new AddElementCommand(State.ActiveSection, clone));
            newSelection.Add(orig);
            newSelection.Add(clone);
        }

        if (commands.Count > 0)
        {
            Execute(new CompositeCommand(
                horizontal ? "Rozdělit horizontálně" : "Rozdělit vertikálně", commands));
            State.SelectedElements.Clear();
            State.SelectedElements.AddRange(newSelection);
            RequestPropertiesPanelUpdate?.Invoke();
            RequestCanvasRefresh?.Invoke();
        }
    }

    // ── Element factory (shared by palette drag + toolbar menu) ────────────────

    public static (float Width, float Height) GetDefaultElementSize(string type) => type switch
    {
        "TextElement"      => (80f, 8f),
        "RectangleElement" => (60f, 20f),
        "LineElement"      => (190f, 1f),
        "ImageElement"     => (40f, 30f),
        "TableElement"     => (190f, 30f),
        "BarcodeQR"        => (30f, 30f),
        "BarcodeCode128"   => (80f, 15f),
        "StopListElement"      => (60f, 60f),
        "DepartureGridElement" => (120f, 60f),
        "NotesLegendElement"   => (190f, 25f),
        _                  => (80f, 8f)
    };

    public static TemplateElement CreateElementByType(string type, float x, float y)
    {
        var (w, h) = GetDefaultElementSize(type);
        return type switch
        {
            "TextElement"      => new TextElement      { X = x, Y = y, Width = w, Height = h, Text = "Text" },
            "RectangleElement" => new RectangleElement { X = x, Y = y, Width = w, Height = h, StrokeWidth = 0.5f },
            "LineElement"      => new LineElement      { X = x, Y = y, Width = w, Height = h },
            "ImageElement"     => new ImageElement     { X = x, Y = y, Width = w, Height = h },
            "TableElement"     => new TableElement     { X = x, Y = y, Width = w },
            "BarcodeQR"        => new BarcodeElement   { X = x, Y = y, Width = w, Height = h, BarcodeType = BarcodeElementType.QR },
            "BarcodeCode128"   => new BarcodeElement   { X = x, Y = y, Width = w, Height = h, BarcodeType = BarcodeElementType.Code128 },
            "StopListElement"      => new StopListElement      { X = x, Y = y, Width = w, Height = h },
            "DepartureGridElement" => new DepartureGridElement { X = x, Y = y, Width = w, Height = h },
            "NotesLegendElement"   => new NotesLegendElement   { X = x, Y = y, Width = w, Height = h },
            _                  => new TextElement      { X = x, Y = y, Width = w, Height = h, Text = type }
        };
    }

    // ── Delete ──────────────────────────────────────────────────────────────────

    public void DeleteSelected()
    {
        if (State.SelectedElements.Count == 0) return;

        var commands = new List<IDesignerCommand>();
        foreach (var el in State.SelectedElements.ToList())
        {
            var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(el));
            if (section is null || el.IsLocked) continue;
            commands.Add(new DeleteElementCommand(section, el));
        }

        if (commands.Count == 0) return;

        State.SelectedElements.Clear();

        if (commands.Count == 1) Execute(commands[0]);
        else Execute(new CompositeCommand("Smazat elementy", commands));

        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Group / Ungroup ──────────────────────────────────────────────────────

    /// <summary>Přemapuje GroupId klonu na nový sdílený GroupId (pro paste/duplicate).</summary>
    private static void RemapGroupId(TemplateElement clone, Dictionary<string, string> groupIdMap)
    {
        if (clone.GroupId is null) return;
        if (!groupIdMap.TryGetValue(clone.GroupId, out var newGid))
            groupIdMap[clone.GroupId] = newGid = ElementGroupHelper.NewGroupId();
        clone.GroupId = newGid;
    }

    public void GroupSelected()
    {
        if (State.SelectedElements.Count < 2) return;
        var groupId = ElementGroupHelper.NewGroupId();
        Execute(new GroupCommand(State.SelectedElements, groupId));
        RequestCanvasRefresh?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
    }

    public void UngroupSelected()
    {
        var grouped = State.SelectedElements.Where(e => e.GroupId is not null).ToList();
        if (grouped.Count == 0) return;
        Execute(new UngroupCommand(grouped));
        RequestCanvasRefresh?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
    }

    // ── ZIndex ────────────────────────────────────────────────────────────────
    public void ChangeZIndex(int sign, bool toExtreme)
    {
        if (State.SelectedElements.Count == 0 || State.ActiveSection is null) return;

        var section  = State.ActiveSection;
        var selected = State.SelectedElements
            .Where(el => section.Elements.Contains(el))
            .ToList();
        if (selected.Count == 0) return;

        var commands = new List<IDesignerCommand>();

        if (toExtreme && sign > 0)
        {
            int baseZ = section.Elements
                .Where(el => !selected.Contains(el))
                .Select(el => el.ZIndex)
                .DefaultIfEmpty(-1).Max() + 1;
            foreach (var el in selected.OrderBy(e => e.ZIndex))
            {
                int oldZ = el.ZIndex, newZ = baseZ++;
                if (oldZ != newZ) commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
        }
        else if (toExtreme && sign < 0)
        {
            int shift = selected.Count;
            foreach (var el in section.Elements.Where(e => !selected.Contains(e)))
            {
                int oldZ = el.ZIndex, newZ = oldZ + shift;
                commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
            int z = 0;
            foreach (var el in selected.OrderBy(e => e.ZIndex))
            {
                int oldZ = el.ZIndex, newZ = z++;
                if (oldZ != newZ) commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
        }
        else
        {
            foreach (var el in selected)
            {
                int oldZ = el.ZIndex, newZ = Math.Max(0, oldZ + sign);
                if (oldZ != newZ) commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v!));
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

        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(desc, commands));
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Alignment ─────────────────────────────────────────────────────────────
    public void AlignElements(AlignMode mode)
    {
        if (State.ActiveSection is null) return;

        var inSection = State.SelectedElements
            .Where(el => State.ActiveSection.Elements.Contains(el))
            .ToList();

        int minCount = mode is AlignMode.DistributeH or AlignMode.DistributeV ? 3 : 2;
        if (inSection.Count < minCount) return;

        var commands = new List<IDesignerCommand>();

        switch (mode)
        {
            case AlignMode.Left:
            {
                float t = inSection.Min(e => e.X);
                foreach (var el in inSection.Where(e => e.X != t))
                    commands.Add(MakeXCmd(el, t));
                break;
            }
            case AlignMode.Right:
            {
                float t = inSection.Max(e => e.X + e.Width);
                foreach (var el in inSection)
                { float nx = t - el.Width; if (Math.Abs(el.X - nx) > 0.01f) commands.Add(MakeXCmd(el, nx)); }
                break;
            }
            case AlignMode.CenterH:
            {
                float cx = (inSection.Min(e => e.X) + inSection.Max(e => e.X + e.Width)) / 2f;
                foreach (var el in inSection)
                { float nx = cx - el.Width / 2f; if (Math.Abs(el.X - nx) > 0.01f) commands.Add(MakeXCmd(el, nx)); }
                break;
            }
            case AlignMode.Top:
            {
                float t = inSection.Min(e => e.Y);
                foreach (var el in inSection.Where(e => e.Y != t))
                    commands.Add(MakeYCmd(el, t));
                break;
            }
            case AlignMode.Bottom:
            {
                float t = inSection.Max(e => e.Y + e.EffectiveDesignerHeight);
                foreach (var el in inSection)
                { float ny = t - el.EffectiveDesignerHeight; if (Math.Abs(el.Y - ny) > 0.01f) commands.Add(MakeYCmd(el, ny)); }
                break;
            }
            case AlignMode.MiddleV:
            {
                float cy = (inSection.Min(e => e.Y) + inSection.Max(e => e.Y + e.EffectiveDesignerHeight)) / 2f;
                foreach (var el in inSection)
                { float ny = cy - el.EffectiveDesignerHeight / 2f; if (Math.Abs(el.Y - ny) > 0.01f) commands.Add(MakeYCmd(el, ny)); }
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
                { if (Math.Abs(el.X - cur) > 0.01f) commands.Add(MakeXCmd(el, cur)); cur += el.Width + gap; }
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
                { if (Math.Abs(el.Y - cur) > 0.01f) commands.Add(MakeYCmd(el, cur)); cur += el.EffectiveDesignerHeight + gap; }
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

        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(desc, commands));
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    private static IDesignerCommand MakeXCmd(TemplateElement el, float nx)
        => new SetPropertyCommand("X", el.X, nx, v => el.X = (float)v!);
    private static IDesignerCommand MakeYCmd(TemplateElement el, float ny)
        => new SetPropertyCommand("Y", el.Y, ny, v => el.Y = (float)v!);

    // ── Key movement ──────────────────────────────────────────────────────────
    public void MoveByKey(Key key, bool largeStep)
    {
        float step = largeStep ? State.GridSize : 1f;
        float dx   = key switch { Key.Left => -step, Key.Right => step, _ => 0f };
        float dy   = key switch { Key.Up   => -step, Key.Down  => step, _ => 0f };

        var initPositions = State.SelectedElements.ToDictionary(el => el, el => (el.X, el.Y));
        float sectionW = State.CurrentTemplate.PageSize.PrintableWidth;
        float clampedDx = dx, clampedDy = dy;

        foreach (var kvp in initPositions)
        {
            var el      = kvp.Key;
            float elH   = el.EffectiveDesignerHeight;
            var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(el));
            float sectionH = section?.EffectiveDesignerHeight ?? float.MaxValue;
            float maxDx = sectionW - el.Width - kvp.Value.X;
            float maxDy = sectionH - elH    - kvp.Value.Y;
            // Element může být širší/vyšší než sekce (např. background image) — přeskočit clamp
            if (maxDx >= -kvp.Value.X)
                clampedDx = Math.Clamp(clampedDx, -kvp.Value.X, maxDx);
            if (maxDy >= -kvp.Value.Y)
                clampedDy = Math.Clamp(clampedDy, -kvp.Value.Y, maxDy);
        }

        if (clampedDx == 0f && clampedDy == 0f) return;

        foreach (var kvp in initPositions)
        { kvp.Key.X = kvp.Value.X + clampedDx; kvp.Key.Y = kvp.Value.Y + clampedDy; }

        var moves = initPositions
            .Where(kvp => kvp.Key.X != kvp.Value.X || kvp.Key.Y != kvp.Value.Y)
            .Select(kvp => (IDesignerCommand)new MoveElementCommand(
                kvp.Key, kvp.Value.X, kvp.Value.Y, kvp.Key.X, kvp.Key.Y))
            .ToList();

        if (moves.Count == 1) Execute(moves[0]);
        else if (moves.Count > 1) Execute(new CompositeCommand("Přesun šipkou", moves));

        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Schema / parameters ───────────────────────────────────────────────────
    public void RefreshFieldsList()
    {
        var agendaCode = State.CurrentTemplate.AgendaCode;

        if (string.IsNullOrWhiteSpace(agendaCode))
        {
            AgendaNameText = "Agenda: — (šablona bez agendy)";
            UpdateCacheStatus(null, null);
            LoadAgendaParameters(agendaCode);
            RebuildSchemaTree(null);
            return;
        }

        var schema = SchemaProvider.GetSchema(agendaCode);

        if (schema is null)
        {
            AgendaNameText = $"Agenda: {agendaCode} (schéma nenalezeno)";
            UpdateCacheStatus(agendaCode, null);
            LoadAgendaParameters(agendaCode);
            RebuildSchemaTree(null);
            return;
        }

        AgendaNameText = $"Agenda: {schema.AgendaName}";
        var cacheSchema = CacheProvider?.GetSchema(agendaCode);
        UpdateCacheStatus(agendaCode, cacheSchema);
        LoadAgendaParameters(agendaCode);
        RebuildSchemaTree(schema);
    }

    public void RebuildSchemaTree(DataSchema? schema)
    {
        SchemaGroups.Clear();

        var fields = schema?.Fields ?? [];
        if (fields.Count > 0)
            SchemaGroups.Add(new SchemaGroupVM(
                $"📋 Skalární pole ({fields.Count})",
                fields.Select(f => new SchemaFieldVM(f.FieldName, f.Label))));

        foreach (var coll in schema?.FieldCollections ?? [])
            SchemaGroups.Add(new SchemaGroupVM(
                $"📊 {coll.Name} [{coll.CollectionKey}]",
                coll.Fields.Select(f => new SchemaFieldVM(f.FieldName, f.Label))));

        if (_agendaParameters.Count > 0)
            SchemaGroups.Add(new SchemaGroupVM(
                $"⚙ Parametry ({_agendaParameters.Count})",
                _agendaParameters.Select(p => new SchemaFieldVM(p.Name, p.Label + " (parametr)"))));
    }

    public void LoadAgendaParameters(string? agendaCode)
    {
        var code = string.IsNullOrEmpty(agendaCode) ? null : agendaCode;
        if (code == _lastParamsAgendaCode) return;
        _lastParamsAgendaCode = code;

        _agendaParameters = code is not null && CacheProvider is not null
            ? [.. CacheProvider.GetParameters(code)]
            : [.. State.CurrentTemplate.Parameters ?? []];

        _previewParams.Clear();
        _cachedParamValues = null;
        foreach (var p in _agendaParameters)
            _previewParams[p.Name] = p.GetDefaultTyped();

        RebuildParametersPanel();
        LoadRealPreviewData();
        RequestPreviewRefresh?.Invoke();
    }

    /// <summary>
    /// Načte reálná data z Caché pro live preview. Pokud GetData selže nebo vrátí
    /// prázdný výsledek, _realPreviewData zůstane null → fallback na mock data.
    /// </summary>
    /// <summary>ID linky pro načítání reálných dat do preview. Zatím hardcoded, v budoucnu z UI.</summary>
    public string PreviewLinkaId { get; set; } = "7372";

    public void LoadRealPreviewData()
    {
        _realPreviewData = null;
        _realPreviewIndex = 0;

        var agendaCode = State.CurrentTemplate.AgendaCode;
        if (string.IsNullOrEmpty(agendaCode) || CacheProvider is null) return;

        try
        {
            // Sestavit parametry z výchozích hodnot + LINKA_ID
            var defaultParams = _previewParams.ToDictionary(
                kv => kv.Key,
                kv => (object?)kv.Value?.ToString(),
                StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(PreviewLinkaId))
                defaultParams["LINKA_ID"] = PreviewLinkaId;

            var data = CacheProvider.GetData(agendaCode, defaultParams);
            if (data is { Count: > 0 })
            {
                _realPreviewData = data;
                StatusText = $"Načteno {data.Count} stránek reálných dat (linka {PreviewLinkaId})";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Preview] LoadRealPreviewData failed: {ex.Message}");
        }
    }

    public void RebuildParametersPanel()
    {
        PreviewParameters.Clear();
        foreach (var p in _agendaParameters)
        {
            PreviewParameters.Add(new ParameterControlVM(p, _previewParams, () =>
            {
                _cachedParamValues = null;
                RequestPreviewRefresh?.Invoke();
            }));
        }
        HasParameters = PreviewParameters.Count > 0;
    }

    private void UpdateCacheStatus(string? agendaCode, DataSchema? cacheSchema)
    {
        if (CacheProvider is null)
        { StatusText = "Caché: není nakonfigurováno — pole načtena z JSON"; return; }

        if (agendaCode is null)
        { StatusText = "Caché: nakonfigurováno"; return; }

        StatusText = cacheSchema is not null
            ? $"● Caché: připojeno — {agendaCode} ({cacheSchema.Fields.Count} polí, {cacheSchema.FieldCollections?.Count ?? 0} kolekcí)"
            : $"○ Caché: nedostupné — záloha JSON ({agendaCode})";
    }

    public CultureInfo GetTemplateCulture()
    {
        try { return CultureInfo.GetCultureInfo(State.CurrentTemplate.CultureCode ?? "cs-CZ"); }
        catch (CultureNotFoundException) { return CultureInfo.GetCultureInfo("cs-CZ"); }
    }

    // ── Preview mock data (called by code-behind preview renderer) ────────────

    /// <summary>
    /// Sestaví DataContext pro preview. Priorita:
    /// 1. Reálná data z Caché (pokud LoadRealPreviewData uspělo)
    /// 2. Mock data z Caché GetMockData
    /// 3. Syntetická mock data ze schématu
    /// Volitelný parametr overrideParams nahrazuje interní _previewParams (pro PrintDialog).
    /// </summary>
    public DataContext? BuildPreviewMockData(Dictionary<string, object?>? overrideParams = null)
    {
        var agendaCode = State.CurrentTemplate.AgendaCode;
        var culture = GetTemplateCulture();
        DataContext? mockData = null;

        // Priorita 1: reálná data (pokud jsou k dispozici)
        if (_realPreviewData is { Count: > 0 })
        {
            var idx = Math.Clamp(_realPreviewIndex, 0, _realPreviewData.Count - 1);
            mockData = _realPreviewData[idx]?.WithCulture(culture);
        }

        // Priorita 2: mock data z Caché
        if (mockData is null && agendaCode is not null && CacheProvider is not null)
            mockData = CacheProvider.GetMockData(agendaCode)?.WithCulture(culture);

        // Priorita 3: syntetická mock data ze schématu
        if (mockData is null)
        {
            var schema = SchemaProvider.GetSchema(agendaCode ?? "");
            if (schema is not null)
            {
                var collections = schema.FieldCollections ?? [];
                var values = schema.Fields
                    .Concat(collections.SelectMany(c => c.Fields))
                    .DistinctBy(f => f.FieldName)
                    .ToDictionary(f => f.FieldName, f => (object?)(f.Example ?? MockDefault(f)));

                var rowsDict = new Dictionary<string, IReadOnlyList<DataContext>>();
                foreach (var collection in collections)
                {
                    var mockRows = Enumerable.Range(1, 3).Select(i =>
                    {
                        var rowData = collection.Fields.ToDictionary(
                            f => f.FieldName,
                            f => (object?)MakeMockValue(f.Example ?? MockDefault(f), f.DataType, i));
                        return new DataContext(rowData, culture: culture);
                    }).ToList();
                    rowsDict[collection.CollectionKey] = mockRows;
                }

                mockData = new DataContext(values, rowsDict, culture);
            }
        }

        // Použij overrideParams (z PrintDialogu) nebo interní _previewParams
        var paramValues = overrideParams;
        if (paramValues is null && _previewParams.Count > 0)
        {
            _cachedParamValues ??= _previewParams.ToDictionary(
                kv => kv.Key,
                kv => (object?)kv.Value?.ToString(),
                StringComparer.OrdinalIgnoreCase);
            paramValues = _cachedParamValues;
        }

        if (paramValues is { Count: > 0 })
        {
            var stringParams = paramValues.ToDictionary(
                kv => kv.Key,
                kv => (object?)kv.Value?.ToString(),
                StringComparer.OrdinalIgnoreCase);

            mockData = mockData is not null
                ? mockData.WithExtraValues(stringParams)
                : new DataContext(stringParams, culture: culture);
        }

        return mockData;
    }

    /// <summary>Výchozí mock hodnota podle typu pole — použije se když Example chybí.</summary>
    private static string MockDefault(DataField f) => f.DataType switch
    {
        FieldDataType.Number   => "100",
        FieldDataType.Date     => "01.01.2024",
        FieldDataType.Time     => "08:00",
        FieldDataType.DateTime => "01.01.2024 08:00",
        FieldDataType.Boolean  => "true",
        _                      => f.Label  // text — fallback na label
    };

    private static string MakeMockValue(string baseValue, FieldDataType type, int rowIndex)
    {
        if (rowIndex <= 1) return baseValue;
        return type switch
        {
            FieldDataType.Number => int.TryParse(baseValue, out var n) ? (n + rowIndex - 1).ToString() : baseValue,
            FieldDataType.Time   => TimeSpan.TryParse(baseValue, out var t)
                ? (t + TimeSpan.FromMinutes(10 * (rowIndex - 1))).ToString(@"hh\:mm") : baseValue,
            FieldDataType.Date   => DateTime.TryParse(baseValue, out var d)
                ? d.AddDays(rowIndex - 1).ToString("dd.MM.yyyy") : baseValue,
            _ => string.IsNullOrEmpty(baseValue) ? baseValue : $"{baseValue} #{rowIndex}"
        };
    }

    // ── Available fields (for ElementPropertiesWindow) ────────────────────────
    public IReadOnlyList<DataField>? GetAvailableFields()
    {
        var schemaFields = SchemaProvider.GetSchema(State.CurrentTemplate.AgendaCode ?? "")?.Fields;
        if (schemaFields is null && _agendaParameters.Count == 0) return null;
        var paramFields  = _agendaParameters.Select(p => new DataField
            { FieldName = p.Name, Label = p.Label + " (parametr)", DataType = FieldDataType.Text });
        return [.. (schemaFields ?? []), .. paramFields];
    }

    public IReadOnlyList<DataFieldCollection>? GetAvailableGroups()
        => SchemaProvider.GetSchema(State.CurrentTemplate.AgendaCode ?? "")?.FieldCollections;

    // ── Sample template ───────────────────────────────────────────────────────
    public static Template CreateSampleTemplate()
    {
        var t = new Template { Name = "Nová šablona", AgendaCode = "MHD" };
        t.Sections.Add(new Section
        {
            Name = "Záhlaví", PrintBehavior = SectionPrintBehavior.OnEveryPage,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 25f
        });
        t.Sections.Add(new Section
        {
            Name = "Obsah", PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages, DesignerHeight = 237f
        });
        t.Sections.Add(new Section
        {
            Name = "Patička", PrintBehavior = SectionPrintBehavior.OnEveryPage,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 15f
        });
        return t;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SectionVM
// ─────────────────────────────────────────────────────────────────────────────

public class SectionVM : INotifyPropertyChanged
{
    private readonly TemplateDesignerViewModel _vm;
    public Section Section { get; }

    /// <summary>Global callback for showing section properties dialog (wired by code-behind).</summary>
    public static Action<Section>? ShowPropertiesGlobal   { get; set; }
    public static Action<Section>? SaveTemplateGlobal     { get; set; }

    public SectionVM(Section section, TemplateDesignerViewModel vm)
    {
        Section = section;
        _vm     = vm;
    }

    public string  Name      => Section.Name;
    public bool    IsDynamic => Section.DesignerHeight is null;
    public bool    IsActive  => _vm.State.ActiveSection == Section;
    public bool    HasSubtitle => Subtitle is not null;

    public string? Subtitle
    {
        get
        {
            string? sub = null;
            if (Section.PrintBehavior != SectionPrintBehavior.Once)
                sub = Section.PrintBehavior switch
                {
                    SectionPrintBehavior.OnEveryPage            => "každá stránka",
                    SectionPrintBehavior.OnFirstPageOnly        => "jen 1. stránka",
                    SectionPrintBehavior.OnLastPageOnly         => "jen posl. stránka",
                    SectionPrintBehavior.OnEveryPageExceptFirst => "kromě 1. stránky",
                    _ => null
                };
            if (Section.PrintBehavior == SectionPrintBehavior.PerDataRow && !string.IsNullOrEmpty(Section.DataSource))
                sub = (sub is null ? "" : sub + " · ") + Section.DataSource;
            return sub;
        }
    }

    public bool CanMoveUp   => _vm.State.CurrentTemplate.Sections.IndexOf(Section) > 0;
    public bool CanMoveDown => _vm.State.CurrentTemplate.Sections.IndexOf(Section)
                               < _vm.State.CurrentTemplate.Sections.Count - 1;

    private RelayCommand? _moveUp, _moveDown, _delete, _saveTemplate, _activate, _showProps;

    public RelayCommand MoveUpCommand       => _moveUp       ??= new(() => _vm.MoveSection(Section, -1),   () => CanMoveUp);
    public RelayCommand MoveDownCommand     => _moveDown     ??= new(() => _vm.MoveSection(Section, +1),   () => CanMoveDown);
    public RelayCommand DeleteCommand       => _delete       ??= new(() => _vm.DeleteSection(Section));
    public RelayCommand SaveTemplateCommand => _saveTemplate ??= new(() => SaveTemplateGlobal?.Invoke(Section));
    public RelayCommand ActivateCommand     => _activate     ??= new(() => _vm.ActivateSection(Section));
    public RelayCommand ShowPropertiesCommand => _showProps  ??= new(() => ShowPropertiesGlobal?.Invoke(Section));

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ─────────────────────────────────────────────────────────────────────────────
// Schema ViewModels
// ─────────────────────────────────────────────────────────────────────────────

public class SchemaGroupVM
{
    public string                     Header { get; }
    public IReadOnlyList<SchemaFieldVM> Fields { get; }

    public SchemaGroupVM(string header, IEnumerable<SchemaFieldVM> fields)
    {
        Header = header;
        Fields = [.. fields];
    }
}

public class SchemaFieldVM
{
    public string FieldName   { get; }
    public string Label       { get; }
    public string Placeholder => $"{{{FieldName}}}";
    public string LabelDisplay => string.IsNullOrEmpty(Label) ? "" : "  " + Label;

    public SchemaFieldVM(string fieldName, string label)
    {
        FieldName = fieldName;
        Label     = label;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ParameterControlVM
// ─────────────────────────────────────────────────────────────────────────────

public class ParameterControlVM : INotifyPropertyChanged
{
    private readonly TemplateParameter           _param;
    private readonly Dictionary<string, object?> _values;
    private readonly Action?                     _onChanged;

    public ParameterControlVM(TemplateParameter param, Dictionary<string,object?> values, Action? onChanged)
    {
        _param     = param;
        _values    = values;
        _onChanged = onChanged;
    }

    public string                    Label         => _param.Label;
    public string                    ParameterType => _param.Type;
    public IReadOnlyList<string>     Options       => _param.Options ?? [];

    public object? Value
    {
        get => _values.TryGetValue(_param.Name, out var v) ? v : _param.GetDefaultTyped();
        set { _values[_param.Name] = value; OnPropertyChanged(); _onChanged?.Invoke(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ─────────────────────────────────────────────────────────────────────────────
// ParameterTemplateSelector
// ─────────────────────────────────────────────────────────────────────────────

public class ParameterTemplateSelector : System.Windows.Controls.DataTemplateSelector
{
    public System.Windows.DataTemplate? BoolTemplate    { get; set; }
    public System.Windows.DataTemplate? EnumTemplate    { get; set; }
    public System.Windows.DataTemplate? DefaultTemplate { get; set; }

    public override System.Windows.DataTemplate? SelectTemplate(object item,
        System.Windows.DependencyObject container)
        => item is ParameterControlVM vm
            ? vm.ParameterType switch
              { "bool" => BoolTemplate, "enum" => EnumTemplate, _ => DefaultTemplate }
              ?? DefaultTemplate
            : DefaultTemplate;
}
