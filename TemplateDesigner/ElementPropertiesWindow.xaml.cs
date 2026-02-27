using System.ComponentModel;
using System.Windows;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;

namespace TemplateDesigner;

public partial class ElementPropertiesWindow : Window
{
    private readonly DesignerState _state;
    private readonly Action        _invalidate;

    private TemplateElement? _currentElement;
    private int              _undoDepthAtOpen;
    private bool             _isReallyClosing;

    public ElementPropertiesWindow(DesignerState state, Action invalidate)
    {
        InitializeComponent();
        _state      = state;
        _invalidate = invalidate;
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
        PropertiesPanel.ShowElement(element);

        if (!IsVisible) Show();
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
