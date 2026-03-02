using System.ComponentModel;
using System.Windows;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Models.Sections;

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
    private readonly Func<IReadOnlyList<DataField>?>? _getAvailableFields;

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
        Func<IReadOnlyList<DataField>?>? getAvailableFields = null)
    {
        InitializeComponent();
        _state               = state;
        _invalidate          = invalidate;
        _refreshSections     = refreshSections;
        _updateCanvasSize    = updateCanvasSize;
        _refreshFields       = refreshFields;
        _getAvailableFields  = getAvailableFields;
        _currentInvalidate   = invalidate;
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
        PropertiesPanel.ShowDescriptors(descriptors);

        if (!IsVisible) Show();
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
        _currentInvalidate = () => { _invalidate(); _refreshSections(); };
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

        return list;
    }

    /// <summary>Schová dialog bez rušení změn.</summary>
    public new void Hide()
    {
        _currentElement = null;
        base.Hide();
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
