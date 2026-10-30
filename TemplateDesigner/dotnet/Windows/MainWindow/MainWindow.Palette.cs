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

/// <summary>Přetahování prvků z palety a polí ze stromu dat na plátno.</summary>
public partial class MainWindow
{
    // ── Adding element (drag from palette / field drag) ────────────────────────
    private string? _addingElementType;
    private string? _addingFieldName;       // e.g. "CISLO_LINKY" - mutually exclusive with _addingElementType
    private SKPoint _addingGhostPos;
    private Section? _addingTargetSection;
    private TemplateElement? _addingTargetElement; // TextElement under cursor (field drag append)

    // ── Field drag initiation from TreeView ─────────────────────────────────────
    private System.Windows.Point _fieldDragStartPoint;
    private SchemaFieldVM? _fieldDragCandidate;

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
}
