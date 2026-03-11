using System.Windows;
using EDControls.EDViewModel;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;

namespace TemplateDesigner;

public class ElementPropertiesWindowViewModel : EDViewModel
{
    private readonly DesignerState _state;
    private readonly Action        _invalidate;
    private readonly Action        _refreshSections;
    private readonly Action        _updateCanvasSize;
    private readonly Action        _refreshFields;

    private readonly Func<IReadOnlyList<DataField>?>?           _getAvailableFields;
    private readonly Func<IReadOnlyList<DataFieldCollection>?>? _getAvailableGroups;
    private readonly ITableTemplateProvider?                    _tableTemplateProvider;

    private int    _undoDepthAtOpen;
    private Action _currentInvalidate;

    // ── Exponováno do code-behind ─────────────────────────────────────────

    /// <summary>Aktuálně zobrazovaný element — pro detekci nové session v ShowForElement.</summary>
    public TemplateElement? CurrentElement { get; private set; }

    /// <summary>Invalidace odpovídající aktuálnímu kontextu (element / sekce / stránka).</summary>
    public Action CurrentInvalidate => _currentInvalidate;

    /// <summary>Předávané do PropertiesPanel.SetContext jako execute akce.</summary>
    public Action<IDesignerCommand> ExecuteCommand => cmd => _state.Execute(cmd);

    /// <summary>Bindovatelný titulek okna.</summary>
    private string _windowTitle = "Vlastnosti";
    public string WindowTitle { get => _windowTitle; set => Set(ref _windowTitle, value); }

    /// <summary>
    /// Callback: VM říká View "zobraz tyto deskriptory" — stejný pattern jako RequestCanvasRefresh
    /// v MainWindow. Používá se uvnitř EnhanceTableEditor při interní změně panelu.
    /// </summary>
    public Action<IReadOnlyList<Models.PropertyDescriptor>>? RequestShowDescriptors { get; set; }

    /// <summary>Deskriptory připravené posledním voláním PrepareFor* — Window je přečte v konstruktoru.</summary>
    public IReadOnlyList<Models.PropertyDescriptor>? CurrentDescriptors { get; private set; }

    /// <summary>Zruší změny a zavře dialog (DialogResult = false).</summary>
    public RelayCommand CancelCommand { get; }

    // ── Konstruktor ───────────────────────────────────────────────────────

    public ElementPropertiesWindowViewModel(
        DesignerState state,
        Action invalidate,
        Action refreshSections,
        Action updateCanvasSize,
        Action refreshFields,
        Func<IReadOnlyList<DataField>?>? getAvailableFields = null,
        Func<IReadOnlyList<DataFieldCollection>?>? getAvailableGroups = null,
        ITableTemplateProvider? tableTemplateProvider = null)
    {
        _state                 = state;
        _invalidate            = invalidate;
        _refreshSections       = refreshSections;
        _updateCanvasSize      = updateCanvasSize;
        _refreshFields         = refreshFields;
        _getAvailableFields    = getAvailableFields;
        _getAvailableGroups    = getAvailableGroups;
        _tableTemplateProvider = tableTemplateProvider;
        _currentInvalidate     = invalidate;

        // OkCommand je zděděn z EDViewModel — base.OnOk() volá CloseDialog(true), což je správně
        CancelCommand = new RelayCommand(() => { CancelChanges(); CloseDialog(false); });
    }

    // ── Příprava deskriptorů ──────────────────────────────────────────────

    /// <summary>
    /// Připraví deskriptory pro daný element, nastaví titulek a undo hloubku.
    /// isNewSession=true při prvním zobrazení nebo přepnutí na jiný element.
    /// </summary>
    public IReadOnlyList<Models.PropertyDescriptor> PrepareForElement(TemplateElement element, bool isNewSession)
    {
        if (isNewSession)
            _undoDepthAtOpen = _state.UndoStack.Count;

        CurrentElement     = element;
        _currentInvalidate = _invalidate;
        WindowTitle        = "Vlastnosti: " + element.GetType().Name.Replace("Element", "");

        var descriptors = element.GetPropertyDescriptors();

        if (element is TextElement)
        {
            var fields = _getAvailableFields?.Invoke();
            if (fields is { Count: > 0 })
                descriptors = EnhanceWithFieldPicker(descriptors, fields);
        }

        if (element is TableElement tableElement)
            descriptors = EnhanceTableEditor(descriptors, tableElement);

        CurrentDescriptors = descriptors;
        return descriptors;
    }

    /// <summary>Připraví deskriptory pro sekci, nastaví titulek a undo hloubku.</summary>
    public IReadOnlyList<Models.PropertyDescriptor> PrepareForSection(Section section)
    {
        _undoDepthAtOpen   = _state.UndoStack.Count;
        CurrentElement     = null;
        _currentInvalidate = () => { _updateCanvasSize(); _invalidate(); _refreshSections(); };
        WindowTitle        = "Vlastnosti sekce: " + section.Name;

        return CurrentDescriptors = BuildSectionDescriptors(section);
    }

    /// <summary>Připraví deskriptory pro stránku, nastaví titulek a undo hloubku.</summary>
    public IReadOnlyList<Models.PropertyDescriptor> PrepareForPage(Template template)
    {
        _undoDepthAtOpen   = _state.UndoStack.Count;
        CurrentElement     = null;
        _currentInvalidate = () => { _invalidate(); _updateCanvasSize(); _refreshFields(); };
        WindowTitle        = "Vlastnosti stránky";

        return CurrentDescriptors = BuildPageDescriptors(template);
    }

    /// <summary>Vrátí veškeré změny provedené od posledního otevření a překreslí.</summary>
    public void CancelChanges()
    {
        while (_state.UndoStack.Count > _undoDepthAtOpen)
            _state.Undo();
        _currentInvalidate();
    }

    // ── Privátní buildery ─────────────────────────────────────────────────

    private IReadOnlyList<Models.PropertyDescriptor> EnhanceTableEditor(
        IReadOnlyList<Models.PropertyDescriptor> descriptors,
        TableElement element)
    {
        var collections = _getAvailableGroups?.Invoke() ?? [];

        var list = new List<Models.PropertyDescriptor>(descriptors.Count);
        foreach (var d in descriptors)
        {
            if (d is Models.EnumPropertyDescriptor enumD && enumD.Label == "Režim")
            {
                // Obalit Set — po změně módu obnovit panel (Zdroj dat se zobrazí/skryje)
                list.Add(new Models.EnumPropertyDescriptor
                {
                    Label   = enumD.Label,
                    Group   = enumD.Group,
                    Get     = enumD.Get,
                    Set     = v =>
                    {
                        enumD.Set(v);
                        RequestShowDescriptors?.Invoke(
                            EnhanceTableEditor(element.GetPropertyDescriptors(), element));
                    },
                    Options = enumD.Options
                });
            }
            else if (d is Models.StringPropertyDescriptor strD && strD.Label == "Zdroj dat" && collections.Count > 0)
            {
                list.Add(new Models.SuggestStringPropertyDescriptor
                {
                    Label          = strD.Label,
                    Group          = strD.Group,
                    Get            = strD.Get,
                    Set            = strD.Set,
                    GetSuggestions = () => collections.Select(c => (c.CollectionKey, c.Name))
                });
            }
            else if (d is Models.ButtonPropertyDescriptor btn && btn.Label == "Editor")
            {
                list.Add(new Models.ButtonPropertyDescriptor
                {
                    Label       = btn.Label,
                    Group       = btn.Group,
                    ButtonLabel = btn.ButtonLabel,
                    OnClick     = () =>
                    {
                        var collectionFields = collections.Count > 0
                            ? (collections.FirstOrDefault(c => c.CollectionKey == element.DataSource)?.Fields
                                   as IReadOnlyList<DataField>
                               ?? collections.SelectMany(c => c.Fields).ToList())
                            : null;

                        var dlg = new TableEditorDialog(element, collectionFields, _tableTemplateProvider)
                            { Owner = Application.Current.MainWindow };
                        if (dlg.ShowDialog() != true) return;

                        var cmds = new List<IDesignerCommand>();

                        cmds.Add(new SetPropertyCommand(
                            "Sloupce tabulky", element.Columns, dlg.ResultColumns,
                            v => element.Columns = (List<TableColumn>)v!));

                        if (element.Mode == TableMode.Fixed)
                        {
                            cmds.Add(new SetPropertyCommand(
                                "Řádky tabulky", element.Rows, dlg.ResultRows,
                                v => element.Rows = (List<TableRow>?)v));
                        }

                        _state.Execute(new CompositeCommand("Upravit tabulku", cmds));
                        _currentInvalidate();

                        RequestShowDescriptors?.Invoke(
                            EnhanceTableEditor(element.GetPropertyDescriptors(), element));
                    }
                });
            }
            else
            {
                list.Add(d);
            }
        }
        return list;
    }

    private static IReadOnlyList<Models.PropertyDescriptor> EnhanceWithFieldPicker(
        IReadOnlyList<Models.PropertyDescriptor> descriptors,
        IReadOnlyList<DataField> fields)
    {
        var list = new List<Models.PropertyDescriptor>(descriptors.Count);
        foreach (var d in descriptors)
        {
            if (d is StringPropertyDescriptor sd && sd.Label == "Text" && sd.MultiLine)
            {
                list.Add(new TextWithPickerPropertyDescriptor
                {
                    Label           = sd.Label,
                    Group           = sd.Group,
                    Get             = sd.Get,
                    Set             = sd.Set,
                    MultiLine       = sd.MultiLine,
                    AvailableFields = fields
                });
            }
            else
            {
                list.Add(d);
            }
        }
        return list;
    }

    private IReadOnlyList<Models.PropertyDescriptor> BuildPageDescriptors(Template template)
    {
        var ps = template.PageSize;
        return
        [
            new StringPropertyDescriptor
            {
                Label = "Kód agendy", Group = "Šablona",
                Get   = () => template.AgendaCode ?? "",
                Set   = v  => template.AgendaCode = string.IsNullOrWhiteSpace(v) ? null : v.Trim().ToUpperInvariant()
            },
            new FloatPropertyDescriptor
            {
                Label = "Šířka",  Group = "Rozměry stránky",
                Get   = () => ps.Width,
                Set   = v  => ps.Width = v,
                Min   = 50f, Max = 1200f, Unit = "mm"
            },
            new FloatPropertyDescriptor
            {
                Label = "Výška",  Group = "Rozměry stránky",
                Get   = () => ps.Height,
                Set   = v  => ps.Height = v,
                Min   = 50f, Max = 1200f, Unit = "mm"
            },
            new FloatPropertyDescriptor
            {
                Label = "Nahoře",  Group = "Okraje",
                Get   = () => ps.MarginTop,
                Set   = v  => ps.MarginTop = v,
                Min   = 0f, Max = 100f, Unit = "mm"
            },
            new FloatPropertyDescriptor
            {
                Label = "Dole",    Group = "Okraje",
                Get   = () => ps.MarginBottom,
                Set   = v  => ps.MarginBottom = v,
                Min   = 0f, Max = 100f, Unit = "mm"
            },
            new FloatPropertyDescriptor
            {
                Label = "Vlevo",   Group = "Okraje",
                Get   = () => ps.MarginLeft,
                Set   = v  => ps.MarginLeft = v,
                Min   = 0f, Max = 100f, Unit = "mm"
            },
            new FloatPropertyDescriptor
            {
                Label = "Vpravo",  Group = "Okraje",
                Get   = () => ps.MarginRight,
                Set   = v  => ps.MarginRight = v,
                Min   = 0f, Max = 100f, Unit = "mm"
            },
        ];
    }

    private IReadOnlyList<Models.PropertyDescriptor> BuildSectionDescriptors(Section section)
    {
        var list = new List<Models.PropertyDescriptor>
        {
            new StringPropertyDescriptor
            {
                Label = "Název", Group = "Sekce",
                Get   = () => section.Name,
                Set   = v  => section.Name = v
            }
        };

        if (section.DesignerHeight.HasValue)
        {
            list.Add(new FloatPropertyDescriptor
            {
                Label = "Výška", Group = "Sekce",
                Get   = () => section.DesignerHeight ?? 30f,
                Set   = v  => section.DesignerHeight = v,
                Min   = 5f, Max = 500f, Unit = "mm"
            });
        }

        list.Add(new EnumPropertyDescriptor
        {
            Label   = "Tisk",    Group = "Sekce",
            Get     = () => section.PrintBehavior,
            Set     = v  => section.PrintBehavior = (SectionPrintBehavior)v,
            Options =
            [
                ("Jednou",          (object)SectionPrintBehavior.Once),
                ("Každá stránka",   (object)SectionPrintBehavior.OnEveryPage),
                ("Jen první",       (object)SectionPrintBehavior.OnFirstPageOnly),
                ("Jen poslední",    (object)SectionPrintBehavior.OnLastPageOnly),
                ("Kromě první",     (object)SectionPrintBehavior.OnEveryPageExceptFirst),
            ]
        });

        list.Add(new EnumPropertyDescriptor
        {
            Label   = "Přetečení", Group = "Sekce",
            Get     = () => section.OverflowBehavior,
            Set     = v  => section.OverflowBehavior = (SectionOverflowBehavior)v,
            Options =
            [
                ("Nepřerušovat",  (object)SectionOverflowBehavior.KeepTogether),
                ("Přes stránky",  (object)SectionOverflowBehavior.SplitAcrossPages),
                ("Povolit split", (object)SectionOverflowBehavior.AllowSplit),
            ]
        });

        list.Add(new StringPropertyDescriptor
        {
            Label = "Viditelnost", Group = "Podmínky",
            Get   = () => section.VisibilityExpression ?? "",
            Set   = v  => section.VisibilityExpression = string.IsNullOrWhiteSpace(v) ? null : v.Trim()
        });

        if (section.PrintBehavior == SectionPrintBehavior.PerDataRow)
        {
            list.Add(new StringPropertyDescriptor
            {
                Label = "Datový zdroj", Group = "Tabulka",
                Get   = () => section.DataSource ?? "",
                Set   = v  => section.DataSource = v
            });
            list.Add(new FloatPropertyDescriptor
            {
                Label = "Výška řádku", Group = "Tabulka",
                Get   = () => section.RowHeight ?? 8f,
                Set   = v  => section.RowHeight = v,
                Min   = 3f, Max = 100f, Unit = "mm"
            });
        }

        return list;
    }
}
