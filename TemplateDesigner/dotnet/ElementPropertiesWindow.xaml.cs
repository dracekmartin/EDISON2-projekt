using System.ComponentModel;
using System.Windows;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;

namespace TemplateDesigner;

public partial class ElementPropertiesWindow : Window
{
    private readonly DesignerState _state;
    private readonly Action        _invalidate;
    private readonly Action        _refreshSections;
    private readonly Action        _updateCanvasSize;
    private readonly Action        _refreshFields;

    /// <summary>
    /// Vrátí aktuálně dostupná pole schématu (podle AgendaCode šablony).
    /// Null = schéma není k dispozici, picker se nezobrazí.
    /// </summary>
    private readonly Func<IReadOnlyList<DataField>?>?                _getAvailableFields;
    private readonly Func<IReadOnlyList<DataFieldCollection>?>?      _getAvailableGroups;
    private readonly ITableTemplateProvider?                         _tableTemplateProvider;

    private TemplateElement? _currentElement;
    private int              _undoDepthAtOpen;
    private bool             _isReallyClosing;

    // Aktuální "plná" invalidace (závisí na kontextu — element / sekce / stránka)
    private Action _currentInvalidate;

    public ElementPropertiesWindow(
        DesignerState state,
        Action invalidate,
        Action refreshSections,
        Action updateCanvasSize,
        Action refreshFields,
        Func<IReadOnlyList<DataField>?>? getAvailableFields = null,
        Func<IReadOnlyList<DataFieldCollection>?>? getAvailableGroups = null,
        ITableTemplateProvider? tableTemplateProvider = null)
    {
        InitializeComponent();
        _state                 = state;
        _invalidate            = invalidate;
        _refreshSections       = refreshSections;
        _updateCanvasSize      = updateCanvasSize;
        _refreshFields         = refreshFields;
        _getAvailableFields    = getAvailableFields;
        _getAvailableGroups    = getAvailableGroups;
        _tableTemplateProvider = tableTemplateProvider;
        _currentInvalidate     = invalidate;
        PropertiesPanel.SetContext(cmd => state.Execute(cmd), invalidate);
    }

    /// <summary>
    /// Zobrazí dialog s vlastnostmi daného elementu.
    /// Pokud je dialog již viditelný a zobrazuje stejný element, pouze obnoví hodnoty.
    /// Pokud se element změnil nebo dialog byl skrytý, resetuje se hloubka undo zásobníku
    /// (Cancel pak vrátí pouze změny provedené od tohoto otevření).
    /// </summary>
    public void ShowForElement(TemplateElement element)
    {
        bool isNewSession = !IsVisible || !ReferenceEquals(_currentElement, element);
        if (isNewSession)
            _undoDepthAtOpen = _state.UndoStack.Count;

        _currentElement    = element;
        _currentInvalidate = _invalidate;
        Title = "Vlastnosti: " + element.GetType().Name.Replace("Element", "");

        PropertiesPanel.SetContext(cmd => _state.Execute(cmd), _currentInvalidate);

        // Pro TextElement: nahraď Text descriptor variantou s picker tlačítkem a validací
        var descriptors = element.GetPropertyDescriptors();
        if (element is TextElement)
        {
            var fields = _getAvailableFields?.Invoke();
            if (fields is { Count: > 0 })
                descriptors = EnhanceWithFieldPicker(descriptors, fields);
        }

        // Pro TableElement: napoj tlačítko "Upravit sloupce / řádky..." na TableEditorDialog
        if (element is TableElement tableElement)
            descriptors = EnhanceTableEditor(descriptors, tableElement);

        PropertiesPanel.ShowDescriptors(descriptors);

        if (!IsVisible) Show();
    }

    /// <summary>
    /// Nahradí ButtonPropertyDescriptor "Editor" na TableElement funkčním OnClick
    /// otevírajícím TableEditorDialog. Změny se ukládají jako undoable příkaz.
    /// </summary>
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
                        PropertiesPanel.ShowDescriptors(
                            EnhanceTableEditor(element.GetPropertyDescriptors(), element));
                    },
                    Options = enumD.Options
                });
            }
            else if (d is Models.StringPropertyDescriptor strD && strD.Label == "Zdroj dat" && collections.Count > 0)
            {
                // Nahradit ručním textovým polem filtrovatelným dropdownem kolekcí
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
                        // Pole z konkrétní kolekce (DataSource), fallback = pole ze všech kolekcí
                        // collections je zachyceno z vnějšího scopu — stejná instance jako pro SuggestString
                        var collectionFields = collections.Count > 0
                            ? (collections.FirstOrDefault(c => c.CollectionKey == element.DataSource)?.Fields
                                   as IReadOnlyList<DataField>
                               ?? collections.SelectMany(c => c.Fields).ToList())
                            : null;
                        // Owner = hlavní okno (ne ToolWindow) — jinak WPF po zavření dialogu
                        // aktivuje ToolWindow a způsobuje minimalizaci hlavního okna.
                        var dlg = new TableEditorDialog(element, collectionFields, _tableTemplateProvider)
                            { Owner = Application.Current.MainWindow };
                        if (dlg.ShowDialog() != true) return;

                        var cmds = new List<Commands.IDesignerCommand>();

                        var oldCols = element.Columns;
                        var newCols = dlg.ResultColumns;
                        cmds.Add(new Commands.Implementations.SetPropertyCommand(
                            "Sloupce tabulky", oldCols, newCols,
                            v => element.Columns = (List<Models.Elements.TableColumn>)v!));

                        if (element.Mode == Models.Elements.TableMode.Fixed)
                        {
                            var oldRows = element.Rows;
                            var newRows = dlg.ResultRows;
                            cmds.Add(new Commands.Implementations.SetPropertyCommand(
                                "Řádky tabulky", oldRows, newRows,
                                v => element.Rows = (List<Models.Elements.TableRow>?)v));
                        }

                        _state.Execute(new Commands.Implementations.CompositeCommand(
                            "Upravit tabulku", cmds));
                        _currentInvalidate();

                        // Obnoví panel vlastností (výška se mohla změnit)
                        PropertiesPanel.ShowDescriptors(
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

    /// <summary>
    /// Nahradí StringPropertyDescriptor s Label=="Text" a MultiLine==true
    /// variantou TextWithPickerPropertyDescriptor s dostupnými poli schématu.
    /// </summary>
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

    /// <summary>
    /// Zobrazí dialog s vlastnostmi dané sekce.
    /// Cancel vrátí změny sekce provedené od otevření.
    /// </summary>
    public void ShowForSection(Section section)
    {
        _undoDepthAtOpen   = _state.UndoStack.Count;
        _currentElement    = null;
        _currentInvalidate = () => { _updateCanvasSize(); _invalidate(); _refreshSections(); };
        Title = "Vlastnosti sekce: " + section.Name;

        PropertiesPanel.SetContext(cmd => _state.Execute(cmd), _currentInvalidate);
        PropertiesPanel.ShowDescriptors(BuildSectionDescriptors(section));

        if (!IsVisible) Show();
    }

    /// <summary>
    /// Zobrazí dialog pro editaci rozměrů stránky a okrajů.
    /// Cancel vrátí veškeré změny provedené od otevření.
    /// </summary>
    public void ShowForPage(Template template)
    {
        _undoDepthAtOpen   = _state.UndoStack.Count;
        _currentElement    = null;
        // Při každé změně: překresli canvas, přepočítej velikost, obnov seznam polí agendy
        _currentInvalidate = () => { _invalidate(); _updateCanvasSize(); _refreshFields(); };
        Title = "Vlastnosti stránky";

        PropertiesPanel.SetContext(cmd => _state.Execute(cmd), _currentInvalidate);
        PropertiesPanel.ShowDescriptors(BuildPageDescriptors(template));

        if (!IsVisible) Show();
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

        // Výška — jen pro sekce s pevnou výškou (dynamické mají null)
        if (section.DesignerHeight.HasValue)
        {
            list.Add(new FloatPropertyDescriptor
            {
                Label = "Výška", Group = "Sekce",
                Get   = () => section.DesignerHeight ?? 30f,
                Set   = v  => section.DesignerHeight = v,
                Min = 5f, Max = 500f, Unit = "mm"
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

        // Vlastnosti specifické pro typ sekce (nebo PrintBehavior)
        // DEPRECATED: Properties DataSource a RowHeight jsou nyní přímo v base třídě Section
        // Zobrazují se, pokud PrintBehavior = PerDataRow
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

    /// <summary>Schová dialog bez rušení změn.</summary>
    public new void Hide()
    {
        _currentElement = null;
        base.Hide();
        // Po skrytí ToolWindow WPF nekonzistentně přesměruje focus a může minimalizovat
        // MainWindow. Explicitní Activate() na owneru to opraví.
        if (Owner?.IsLoaded == true)
            Owner.Activate();
    }

    /// <summary>Skutečně zavře okno — volat pouze při ukončení aplikace.</summary>
    public void CloseForReal()
    {
        _isReallyClosing = true;
        Close();
    }

    // ── Tlačítka ─────────────────────────────────────────────────────────

    private void BtnOK_Click(object sender, RoutedEventArgs e) => Hide();

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        // Vrátit veškeré změny provedené od otevření tohoto dialogu
        while (_state.UndoStack.Count > _undoDepthAtOpen)
            _state.Undo();

        _currentInvalidate();
        Hide();
    }

    // ── Zavírání ──────────────────────────────────────────────────────────

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isReallyClosing)
        {
            // Tlačítko X = schovat okno (ne zavřít), změny se zachovají
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}
