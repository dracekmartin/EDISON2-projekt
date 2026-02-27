using System.ComponentModel;
using System.Windows;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Sections;

namespace TemplateDesigner;

public partial class ElementPropertiesWindow : Window
{
    private readonly DesignerState _state;
    private readonly Action        _invalidate;
    private readonly Action        _refreshSections;

    private TemplateElement? _currentElement;
    private int              _undoDepthAtOpen;
    private bool             _isReallyClosing;

    public ElementPropertiesWindow(DesignerState state, Action invalidate, Action refreshSections)
    {
        InitializeComponent();
        _state           = state;
        _invalidate      = invalidate;
        _refreshSections = refreshSections;
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

        _currentElement = element;
        Title = "Vlastnosti: " + element.GetType().Name.Replace("Element", "");

        // Pro elementy: invalidace canvasu
        PropertiesPanel.SetContext(cmd => _state.Execute(cmd), _invalidate);
        PropertiesPanel.ShowElement(element);

        if (!IsVisible) Show();
    }

    /// <summary>
    /// Zobrazí dialog s vlastnostmi dané sekce.
    /// Cancel vrátí změny sekce provedené od otevření.
    /// </summary>
    public void ShowForSection(Section section)
    {
        _undoDepthAtOpen = _state.UndoStack.Count;
        _currentElement  = null;
        Title = "Vlastnosti sekce: " + section.Name;

        // Pro sekce: invalidace canvasu + obnovení levého panelu
        PropertiesPanel.SetContext(cmd => _state.Execute(cmd),
            () => { _invalidate(); _refreshSections(); });
        PropertiesPanel.ShowDescriptors(BuildSectionDescriptors(section));

        if (!IsVisible) Show();
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

        _invalidate();
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
